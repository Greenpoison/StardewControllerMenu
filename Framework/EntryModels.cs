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

    /// <summary>A named, saveable filter over individual actions (e.g. "Favorites" might include a couple of actions from several different mods, not necessarily every action a mod has). Presets can be created in-game.</summary>
    public class Preset
    {
        public string Name { get; set; }
        public List<string> IncludedActionKeys { get; set; } = new();
    }

    /// <summary>Builds/reads the composite key used to identify one specific action across all mods in a preset's <see cref="Preset.IncludedActionKeys"/>.</summary>
    public static class ActionKey
    {
        public static string Of(string modName, string actionName) => $"{modName}|{actionName}";
    }
}
