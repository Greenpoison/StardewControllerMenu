using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;

namespace StardewControllerMenu.Framework
{
    /// <summary>Loads the active profile's entry list and presets from the mod's data folder, and applies the active preset's filter.</summary>
    public class PresetManager
    {
        private const string ProfilesFolder = "data/profiles";
        public const string DefaultProfileName = "Default";

        private readonly IModHelper Helper;
        private readonly IMonitor Monitor;

        private List<ModListing> AllEntries = new();
        private readonly Dictionary<string, Preset> Presets = new();
        private string LoadedProfile = DefaultProfileName;

        public PresetManager(IModHelper helper, IMonitor monitor)
        {
            this.Helper = helper;
            this.Monitor = monitor;
        }

        /// <summary>Reload the given profile's entries.json and every preset file from disk. Safe to call again after switching profiles or editing a preset in-game.</summary>
        public void LoadProfile(string profileName)
        {
            this.LoadedProfile = profileName;

            string entriesPath = $"{ProfilesFolder}/{profileName}/entries.json";
            this.AllEntries = this.Helper.Data.ReadJsonFile<List<ModListing>>(entriesPath) ?? new List<ModListing>();

            this.Presets.Clear();
            foreach (string relativePath in this.GetPresetFilePaths(profileName))
            {
                Preset preset = this.Helper.Data.ReadJsonFile<Preset>(relativePath);
                if (preset != null && !string.IsNullOrWhiteSpace(preset.Name))
                    this.Presets[preset.Name] = preset;
            }

            this.Monitor.Log($"Loaded profile '{profileName}': {this.AllEntries.Count} mod entries, {this.Presets.Count} presets.", LogLevel.Trace);
        }

        /// <summary>Get every mod listing in the active profile, unfiltered.</summary>
        public IReadOnlyList<ModListing> GetAllEntries() => this.AllEntries;

        /// <summary>Get the mod listings visible under the given preset name ("All" bypasses filtering), with each mod's Actions list filtered down to just the ones the preset includes.</summary>
        public IReadOnlyList<ModListing> GetActivePresetEntries(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName) || presetName == "All" || !this.Presets.TryGetValue(presetName, out Preset preset))
                return this.AllEntries;

            var included = new HashSet<string>(preset.IncludedActionKeys);
            var result = new List<ModListing>();
            foreach (ModListing mod in this.AllEntries)
            {
                List<ModAction> matchingActions = mod.Actions
                    .Where(action => included.Contains(ActionKey.Of(mod.ModName, action.Name)))
                    .ToList();
                if (matchingActions.Count > 0)
                    result.Add(new ModListing { ModName = mod.ModName, Actions = matchingActions });
            }
            return result;
        }

        public IEnumerable<string> GetPresetNames() => new[] { "All" }.Concat(this.Presets.Keys);

        /// <summary>Create or overwrite a preset in the active profile from a set of action keys (see <see cref="ActionKey"/>) and save it to disk, so it can be built while playing.</summary>
        public void SavePreset(string name, IEnumerable<string> includedActionKeys)
        {
            var preset = new Preset { Name = name, IncludedActionKeys = includedActionKeys.ToList() };
            this.Presets[name] = preset;
            this.Helper.Data.WriteJsonFile($"{ProfilesFolder}/{this.LoadedProfile}/presets/{SanitizeFileName(name)}.json", preset);
        }

        /// <summary>Delete a preset from memory and disk. Returns false if no preset with that name exists.</summary>
        public bool DeletePreset(string name)
        {
            if (!this.Presets.Remove(name))
                return false;

            string relativePath = $"{ProfilesFolder}/{this.LoadedProfile}/presets/{SanitizeFileName(name)}.json";
            string fullPath = Path.Combine(this.Helper.DirectoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            return true;
        }

        private IEnumerable<string> GetPresetFilePaths(string profileName)
        {
            string presetFolder = $"{ProfilesFolder}/{profileName}/presets";
            string fullFolder = Path.Combine(this.Helper.DirectoryPath, presetFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(fullFolder))
                yield break;

            foreach (string file in Directory.EnumerateFiles(fullFolder, "*.json"))
                yield return $"{presetFolder}/{Path.GetFileName(file)}";
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name;
        }
    }
}
