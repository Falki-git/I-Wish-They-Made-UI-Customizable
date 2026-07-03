using ReduxLib.Configuration;
using UnityEngine;

namespace CustomizableUI.Utilities
{
    public static class Settings
    {
        private static CustomizableUIPlugin Plugin => CustomizableUIPlugin.Instance;

        public static ConfigValue<bool> EnableKeybinding;
        public static ConfigValue<KeyCode> Keybind1;
        public static ConfigValue<KeyCode> Keybind2;

        public static void Initialize()
        {
            EnableKeybinding = new ConfigValue<bool>(Plugin.SWConfiguration.Bind(
                "Keybinding",
                "Enable keybinding",
                true,
                "Enables or disables the keyboard shortcut to show or hide the Customizable UI editor window."));

            Keybind1 = new ConfigValue<KeyCode>(Plugin.SWConfiguration.Bind(
                "Keybinding",
                "Keycode 1",
                KeyCode.LeftControl,
                "First keycode."));

            Keybind2 = new ConfigValue<KeyCode>(Plugin.SWConfiguration.Bind(
                "Keybinding",
                "Keycode 2",
                KeyCode.I,
                "Second keycode."));
        }
    }
}
