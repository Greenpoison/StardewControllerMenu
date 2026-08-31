# Controller Quick Menu

A [SMAPI](https://smapi.io/) mod for Stardew Valley that solves a specific problem for controller and Steam Deck players: once you install enough mods, you run out of physical keys/buttons for keybinds.

Instead of binding every mod action to its own key, this mod gives you **one** button (or button combo) that opens a menu listing every installed mod's actions and their keybinds. Navigate the list with your gamepad and select an entry to trigger it.

## How it works

1. After installing a mod, you (or the mod's installer/instructions) add an entry for it to `data/entries.json` — the mod's name, its actions, the keybind for each action, and a short description of what it does.
2. In-game, press the configured menu button (default: `LeftShoulder + RightShoulder`) to open the menu.
3. Navigate with the d-pad/left stick and press A (or click) on an entry to trigger that mod's keybind.

### Entries file format (`data/entries.json`)

```json
[
    {
        "ModName": "Example Mod",
        "Actions": [
            {
                "Name": "Toggle Debug Overlay",
                "Keybind": "LeftControl + G",
                "Description": "Shows or hides the debug overlay."
            }
        ]
    }
]
```

### Presets

Every player has a default "All" view showing every entry. You can also define presets — named, filtered subsets of the full list (e.g. your most-used entries) — stored as individual files under `data/presets/`. Presets are designed to be creatable while playing, not just by hand-editing JSON before launch.

```json
{
    "Name": "Favorites",
    "IncludedModNames": ["Example Mod"]
}
```

Set `ActivePreset` in `config.json` to switch which preset is shown by default; the goal is to make switching between presets fast enough to do mid-session.

## Status

Early scaffold. Menu UI, entry/preset loading, and gamepad navigation are in place. **Not yet implemented:** actually simulating the target keypress when an entry is selected (`Framework/KeySender.cs`) — this needs OS-level input injection, which is straightforward on Windows but needs a separate approach under Proton on Steam Deck. This is the next milestone.

## Roadmap

- [ ] Implement real keypress injection (`KeySender`)
- [ ] In-game preset editor (add/remove entries from a preset without leaving the menu)
- [ ] Radial menu mode: hold a button to pop up a radial menu of a preset's entries for quick execution, release to select

## Building

Requires the game installed with SMAPI, and the [`Pathoschild.Stardew.ModBuildConfig`](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig) NuGet package (already referenced in the `.csproj`), which auto-detects your game path and copies the build output into your `Mods` folder.

```sh
dotnet build
```

## License

MIT — see [LICENSE](LICENSE).
