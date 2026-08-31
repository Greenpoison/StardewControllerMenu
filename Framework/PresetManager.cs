using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;

namespace StardewControllerMenu.Framework
{
    /// <summary>Loads the entry list and presets from the mod's data folder, and applies the active preset's filter.</summary>
    public class PresetManager
    {
        private const string EntriesPath = "data/entries.json";
        private const string PresetFolder = "data/presets";

        private readonly IModHelper Helper;
        private readonly IMonitor Monitor;

        private List<ModListing> AllEntries = new();
        private readonly Dictionary<string, Preset> Presets = new();

        public PresetManager(IModHelper helper, IMonitor monitor)
        {
            this.Helper = helper;
            this.Monitor = monitor;
        }

        /// <summary>Reload entries.json and every preset file from disk. Safe to call again after the player edits a preset in-game.</summary>
        public void LoadAll()
        {
            this.AllEntries = this.Helper.Data.ReadJsonFile<List<ModListing>>(EntriesPath) ?? new List<ModListing>();

            this.Presets.Clear();
            foreach (string relativePath in this.GetPresetFilePaths())
            {
                Preset preset = this.Helper.Data.ReadJsonFile<Preset>(relativePath);
                if (preset != null && !string.IsNullOrWhiteSpace(preset.Name))
                    this.Presets[preset.Name] = preset;
            }

            this.Monitor.Log($"Loaded {this.AllEntries.Count} mod entries and {this.Presets.Count} presets.", LogLevel.Trace);
        }

        /// <summary>Get every mod listing, unfiltered.</summary>
        public IReadOnlyList<ModListing> GetAllEntries() => this.AllEntries;

        /// <summary>Get the mod listings visible under the given preset name ("All" bypasses filtering).</summary>
        public IReadOnlyList<ModListing> GetActivePresetEntries(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName) || presetName == "All" || !this.Presets.TryGetValue(presetName, out Preset preset))
                return this.AllEntries;

            return this.AllEntries
                .Where(entry => preset.IncludedModNames.Contains(entry.ModName))
                .ToList();
        }

        public IEnumerable<string> GetPresetNames() => new[] { "All" }.Concat(this.Presets.Keys);

        /// <summary>Create or overwrite a preset from a set of mod names and save it to disk, so it can be built while playing.</summary>
        public void SavePreset(string name, IEnumerable<string> includedModNames)
        {
            var preset = new Preset { Name = name, IncludedModNames = includedModNames.ToList() };
            this.Presets[name] = preset;
            this.Helper.Data.WriteJsonFile($"{PresetFolder}/{SanitizeFileName(name)}.json", preset);
        }

        private IEnumerable<string> GetPresetFilePaths()
        {
            string fullFolder = System.IO.Path.Combine(this.Helper.DirectoryPath, PresetFolder.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!System.IO.Directory.Exists(fullFolder))
                yield break;

            foreach (string file in System.IO.Directory.EnumerateFiles(fullFolder, "*.json"))
                yield return $"{PresetFolder}/{System.IO.Path.GetFileName(file)}";
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name;
        }
    }
}
