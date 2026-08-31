using StardewModdingAPI.Utilities;

namespace StardewControllerMenu
{
    public class ModConfig
    {
        /// <summary>The button (or button combo) that opens the quick menu. Defaults to a controller shoulder combo so it doesn't collide with keyboard-bound mods.</summary>
        public KeybindList OpenMenuButton { get; set; } = KeybindList.Parse("LeftShoulder + RightShoulder");

        /// <summary>Name of the preset to show by default. "All" always shows every entry.</summary>
        public string ActivePreset { get; set; } = "All";
    }
}
