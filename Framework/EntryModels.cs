using System.Collections.Generic;

namespace StardewControllerMenu.Framework
{
    /// <summary>A single bindable action belonging to a mod, e.g. "Toggle Debug Overlay".</summary>
    public class ModAction
    {
        public string Name { get; set; }
        public string Keybind { get; set; }
        public string Description { get; set; }
    }

    /// <summary>All the actions exposed by one installed mod. Mod installers append one of these to data/entries.json after installing a mod.</summary>
    public class ModListing
    {
        public string ModName { get; set; }
        public List<ModAction> Actions { get; set; } = new();
    }

    /// <summary>A named, saveable filter over the full entry list (e.g. "Favorites"). Presets can be created in-game.</summary>
    public class Preset
    {
        public string Name { get; set; }
        public List<string> IncludedModNames { get; set; } = new();
    }
}
