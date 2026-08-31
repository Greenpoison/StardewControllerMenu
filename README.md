# Controller Quick Menu

A [SMAPI](https://smapi.io/) mod for Stardew Valley that solves a specific problem for controller and Steam Deck players: once you install enough mods, you run out of physical keys/buttons for keybinds.

Instead of binding every mod action to its own key, this mod gives you **one** button (or button combo) that opens a menu listing every installed mod's actions and their keybinds. Navigate the list with your gamepad and select an entry to trigger it.

## How it works

1. After installing a mod, you (or the mod's installer/instructions) add an entry for it to your active profile's `entries.json` — the mod's name, its actions, the keybind for each action, and a short description of what it does.
2. In-game, press the configured menu button (default: `LeftStick + RightStick`) to open the menu.
3. Navigate with the d-pad/left stick and press A (or click) on an entry. The mod simulates the real keypress/click that entry's keybind maps to, so the target mod reacts exactly as if you'd pressed it yourself.

See [`example-setup/`](example-setup/) for a full, real-world example — a profile generated from an actual 200+ mod Steam Deck install.

## Profiles

Every setting lives under a **profile**: a named folder of `entries.json` + `presets/`. This is meant for players who swap modpacks often — keep one profile per modpack, and switch by editing `ActiveProfile` in `config.json` (or by installing several profiles ahead of time and toggling between them at the start of a session).

```
data/
  profiles/
    Default/
      entries.json
      presets/
        favorites.json
    MyOtherModpack/
      entries.json
      presets/
```

### Entries file format (`data/profiles/<ProfileName>/entries.json`)

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

`Keybind` uses the same syntax as SMAPI's own `KeybindList`: `+` combines buttons into one combo, `,` separates alternative combos (e.g. `"F, ControllerBack"` means either works).

### Presets

Within a profile, every player has a default "All" view showing every entry. You can also define presets — named, filtered subsets of the full list (e.g. your most-used entries) — stored as individual files under that profile's `presets/` folder. Presets are designed to be creatable while playing, not just by hand-editing JSON before launch.

```json
{
    "Name": "Favorites",
    "IncludedModNames": ["Example Mod"]
}
```

Set `ActivePreset` in `config.json` to switch which preset is shown by default; the goal is to make switching between presets fast enough to do mid-session.

## Triggering the actual keybind

When you select an entry, `Framework/KeySender.cs` simulates the real input, using a platform-specific injector:

- **Windows** (including Steam Deck players running the Windows build of the game via Proton): uses the Win32 `SendInput` API, the same mechanism real Windows automation tools use. This works under Wine/Proton because Wine implements the same `user32.dll` entry point for compatibility.
- **Native Linux** (e.g. Steam Deck running the Linux build of the game — this is what my own Deck runs): uses the X11 `XTest` extension, which works when the game's window is on an X11 or XWayland display (the normal case under gamescope).

**Known limitation:** neither backend can simulate a *gamepad* button press — there's no portable equivalent of `SendInput`/`XTest` for joystick input on either platform. When a keybind lists multiple alternatives (e.g. `"F, ControllerBack"`), `KeySender` picks whichever alternative it can actually send; if every alternative requires a controller button, it logs a warning instead of silently doing nothing. Real gamepad-button injection (via a virtual controller driver) is a possible future improvement, not yet implemented.

## Roadmap

- [x] Implement real keypress injection (`KeySender`) for Windows/Proton and native Linux
- [ ] Validate `KeySender` against a real, running game (only reasoned through statically so far — see Testing below)
- [ ] Simulate gamepad-button-only keybinds (needs a virtual controller driver)
- [ ] In-game preset editor (add/remove entries from a preset without leaving the menu)
- [ ] In-game profile switcher
- [ ] Radial menu mode: hold a button to pop up a radial menu of a preset's entries for quick execution, release to select

## Testing

This mod hasn't been built or run against a real Stardew Valley install yet — the `Pathoschild.Stardew.ModBuildConfig` package needs the game installed locally to resolve its assembly references, which isn't available in this dev environment. Build and test in-game before trusting `KeySender` for anything important.

## Building

Requires the game installed with SMAPI, and the [`Pathoschild.Stardew.ModBuildConfig`](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig) NuGet package (already referenced in the `.csproj`), which auto-detects your game path and copies the build output into your `Mods` folder.

```sh
dotnet build
```

## License

MIT — see [LICENSE](LICENSE).
