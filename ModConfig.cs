using StardewModdingAPI.Utilities;

namespace StardewControllerMenu
{
    public class ModConfig
    {
        /// <summary>The button (or button combo) that opens the quick menu. Check your own entries for conflicts before relying on the default - see the example-setup README for one found in a real modpack.</summary>
        public KeybindList OpenMenuButton { get; set; } = KeybindList.Parse("LeftStick + RightStick");

        /// <summary>Which profile's entries/presets to load. Lets you keep a separate entry list per modpack and switch by editing this value. Matches a folder name under data/profiles/.</summary>
        public string ActiveProfile { get; set; } = "Default";

        /// <summary>Name of the preset to show by default within the active profile. "All" always shows every entry.</summary>
        public string ActivePreset { get; set; } = "All";
    }
}
