using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace AMnesia
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class AMnesiaPlugin : BaseUnityPlugin
    {
        internal const string ModName = "AMnesia";
        internal const string ModVersion = "1.0.1";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource AMnesiaLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            turnOffDayMessage = config("1 - General", "Turn off Day Message", Toggle.On,
                "If on, the mod will disable the day count message when attempting to display it on the player's screen.");


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                AMnesiaLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                AMnesiaLogger.LogError($"There was an issue loading your {ConfigFileName}");
                AMnesiaLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        internal static ConfigEntry<Toggle> turnOffDayMessage = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            //var configEntry = Config.Bind(group, name, value, description);

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description)
        {
            return config(group, name, value, new ConfigDescription(description));
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        #endregion
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Message))]
    static class PlayerMessagePatch
    {
        static bool Prefix(Player __instance, MessageHud.MessageType type, string msg, int amount = 0,
            Sprite icon = null)
        {
            if (__instance.m_nview == null || !__instance.m_nview.IsValid())
                return false;
            if (Player.m_localPlayer == null)
                return false;

            if (msg == Localization.instance.Localize("$msg_newday", EnvMan.instance.GetCurrentDay().ToString()))
            {
                if (AMnesiaPlugin.turnOffDayMessage.Value == AMnesiaPlugin.Toggle.On)
                {
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.ShowMessage))]
        static class MessageHudShowMessagePatch
        {
            static bool Prefix(MessageHud __instance, MessageHud.MessageType type, string text, int amount = 0,
                Sprite icon = null)
            {
                if (Hud.IsUserHidden())
                    return false;

                if (text == Localization.instance.Localize("$msg_newday", EnvMan.instance.GetCurrentDay().ToString()))
                {
                    if (AMnesiaPlugin.turnOffDayMessage.Value == AMnesiaPlugin.Toggle.On)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.AddPin))]
        static class MinimapAddPinPatch
        {
            static void Postfix(Minimap __instance, Vector3 pos,
                Minimap.PinType type,
                string name,
                bool save,
                bool isChecked,
                long ownerID = 0)
            {
                foreach (Minimap.PinData pin in __instance.m_pins)
                {
                    if (pin.m_name == string.Format("$hud_mapday {0}",
                            EnvMan.instance.GetDay(ZNet.instance.GetTimeSeconds())))
                    {
                        pin.m_name = "";
                    }
                    else if (pin.m_name.Contains($"$hud_mapday"))
                    {
                        pin.m_name = "";
                    }
                }
            }
        }
    }
}