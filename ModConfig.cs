using StardewModdingAPI.Utilities;

namespace StardewControllerMenu
{
    public class ModConfig
    {
        /// <summary>
        /// The button (or button combo) that opens the quick menu.
        /// Deliberately not a stick click: pressing RightStick has a hardcoded vanilla behavior
        /// (opens chat on a quick press, the emote wheel on a long hold) that reads the controller
        /// state directly and can't be suppressed through SMAPI - confirmed the hard way. Shoulder
        /// buttons have a much smaller side effect (a one-tick, largely self-cancelling toolbar
        /// shift from vanilla's own LB/RB handling) and any conflict with another mod's own keybind
        /// on the same combo (see the example-setup README) is suppressed via SuppressActiveKeybinds
        /// in ModEntry, since that only works for SMAPI-mediated conflicts, not hardcoded ones.
        /// </summary>
        public KeybindList OpenMenuButton { get; set; } = KeybindList.Parse("LeftShoulder + RightShoulder");

        /// <summary>Which profile's entries/presets to load. Lets you keep a separate entry list per modpack and switch by editing this value. Matches a folder name under data/profiles/.</summary>
        public string ActiveProfile { get; set; } = "Default";

        /// <summary>Name of the preset to show by default within the active profile. "All" always shows every entry.</summary>
        public string ActivePreset { get; set; } = "All";

        /// <summary>EXPERIMENTAL: hold this button to open a radial menu of the active preset's entries; release to trigger whichever one the stick/mouse is pointing at. Avoid binding this to LeftTrigger/RightTrigger - SMAPI's own held-state detection for analog triggers has a known timing quirk (see the README's "Radial menu" section) that a plain digital button doesn't have.</summary>
        public KeybindList RadialMenuButton { get; set; } = KeybindList.Parse("ControllerBack");
    }
}
