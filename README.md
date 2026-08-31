# Controller Quick Menu

A [SMAPI](https://smapi.io/) mod for Stardew Valley that solves a specific problem for controller and Steam Deck players: once you install enough mods, you run out of physical keys/buttons for keybinds.

Instead of binding every mod action to its own key, this mod gives you **one** button (or button combo) that opens a menu listing every installed mod's actions and their keybinds. Navigate the list with your gamepad and select an entry to trigger it.

## How it works

1. After installing a mod, you (or the mod's installer/instructions) add an entry for it to your active profile's `entries.json` — the mod's name, its actions, the keybind for each action, and a short description of what it does.
2. In-game, press the configured menu button (default: `LeftStick + RightStick`) to open the menu.
3. Navigate with the d-pad/left stick and press A (or click) on an entry. The mod simulates the real keypress/click that entry's keybind maps to, so the target mod reacts exactly as if you'd pressed it yourself.

See [`example-setup/`](example-setup/) for a full, real-world example — a profile generated from an actual 200+ mod Steam Deck install.

### Menu controls

| Button | Action |
| --- | --- |
| A / click | Trigger the selected entry (or, in edit mode, toggle its mod in/out of the preset being built) |
| X / `E` | Enter or leave preset-build mode |
| Y / `S` | Save the preset being built (opens a naming prompt) - only while in edit mode |
| LB / RB, `[` / `]` | Cycle to the previous/next preset in the current profile |
| LT / RT, `Page Up` / `Page Down` | Cycle to the previous/next profile |

Switching preset or profile from the menu writes the choice straight back to `config.json`, so it's remembered next session too.

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

Set `ActivePreset` in `config.json` to switch which preset is shown by default, or switch it live from the menu itself (see Menu controls above) - building and saving a new preset without leaving the game is the actual goal here, not just editing JSON between sessions.

## Triggering the actual keybind

When you select an entry, `Framework/KeySender.cs` simulates the real input, using a platform-specific injector:

- **Windows** (including Steam Deck players running the Windows build of the game via Proton): uses the Win32 `SendInput` API, the same mechanism real Windows automation tools use. This works under Wine/Proton because Wine implements the same `user32.dll` entry point for compatibility.
- **Native Linux** (e.g. Steam Deck running the Linux build of the game — this is what my own Deck runs): uses the X11 `XTest` extension, which works when the game's window is on an X11 or XWayland display (the normal case under gamescope).

**Known limitation:** neither backend can simulate a *gamepad* button press (an `SButton` like `ControllerA` or `LeftShoulder`, which SMAPI reads from the real `GamePadState`) — there's no portable equivalent of `SendInput`/`XTest` for joystick input on either platform. This only matters if a mod's keybind requires a controller button with no keyboard/mouse alternative. It does *not* apply to a controller button that Steam Input (or similar) remaps to emit a keyboard key - that arrives as a genuine keyboard event before it ever reaches the game, same as anything `KeySender` sends. When a keybind lists multiple alternatives (e.g. `"F, ControllerBack"`), `KeySender` picks whichever it can actually send; if every alternative requires a controller button, it logs a warning instead of silently doing nothing. In the real 27-mod scan under `example-setup/`, only two actions hit this - and both have a working keyboard-bound duplicate anyway.

## Radial menu (experimental)

`Framework/RadialMenu.cs`, wired up in `ModEntry.OnUpdateTicked`, is a first pass at "hold a button to pop up a radial menu of the active preset's entries, tilt the stick toward one, release to trigger it." It's disabled by nothing (there's no on/off switch yet) but effectively opt-in, since its hold-button defaults to `ControllerBack` - a button nothing else in this project uses - so it won't fire unless you rebind it.

**Prior art this borrows from:** [Star Control](https://github.com/focustense/StardewControllers) (`StarControl/Menus/RadialMenuController.cs`, `RadialMenuPainter.cs`) is a mature, actively-maintained mod that does exactly this kind of hold-to-open radial menu for Stardew, and there's an older, simpler one too ([Controller Radial Keybindings](https://www.nexusmods.com/stardewvalley/mods/12801)) that matches this feature's original description almost exactly. Reading Star Control's source saved real debugging time:

- **Wedge selection math**: convert the stick tilt (or cursor-minus-center) to an angle with `atan2`, treating "up" as angle 0 and increasing clockwise, then `round(angle / (2π / itemCount))` picks the nearest wedge. `RadialMenu.UpdateDirection` uses exactly this.
- **A real gotcha with trigger buttons**: Star Control's `MenuToggle` class has to bypass SMAPI's own input helper and read `GamePad.GetState()` directly for `LeftTrigger`/`RightTrigger`, because SMAPI's held-state detection for analog triggers-read-as-buttons has a timing/dead-zone mismatch with the game's own reading of the same trigger - naively suppressing it "leaks" a phantom press. Plain digital buttons (shoulders, face buttons, stick clicks, d-pad) don't have this problem. That's exactly why `RadialMenuButton` defaults to `ControllerBack` rather than a trigger, and why this hasn't been extended to support trigger-based holding at all - it would need the same raw-`GamePadState` workaround Star Control uses.

**What's different/simpler here:** entries are drawn as labels arranged around a circle with a highlight box behind the nearest one, not true colored pie-slice wedges (Star Control renders those with a `BasicEffect` and hand-built triangle geometry - more visual polish than this prototype needs to prove the mechanic out). There's also no paging, quick-slots, or delayed-activation confirmation - just direction-in, keybind-out.

**Status:** compiles against the real assemblies (see Testing), but is unverified at runtime - in particular the stick/mouse-to-screen-space sign conventions in `ModEntry.GetRadialDirection` and `RadialMenu.UpdateDirection` are exactly the kind of thing that's easy to get backwards (highlighting the wedge opposite the one you're pointing at) and only a real controller test would catch.

## Roadmap

- [x] Implement real keypress injection (`KeySender`) for Windows/Proton and native Linux
- [x] Compile against the real game/SMAPI assemblies (see Testing below)
- [x] In-game preset editor (build a preset by toggling mods on/off, then save it, without leaving the menu)
- [x] In-game profile switcher (cycle profiles from the menu; persists to `config.json`)
- [x] Radial menu prototype (see "Radial menu (experimental)" above) - compiles, not yet verified in-game
- [ ] Run in-game on a real Steam Deck/PC and confirm `KeySender`, the preset editor, the profile switcher, and the radial menu's direction math all actually work
- [ ] Decide whether the radial menu needs its own on/off setting and a dedicated small preset (a dozen-mod "All" view makes for very cramped wedges), or should stay a `RadialMenuButton`-gated experiment

## Testing

This project compiles cleanly against the real Stardew Valley 1.6.15 / SMAPI 4.5.2 assemblies (verified by building against copies of those DLLs directly, since this dev environment has no game install for `Pathoschild.Stardew.ModBuildConfig` to auto-detect). That caught two real bugs (missing `using` directives in `QuickMenu.cs`) before they ever reached a player.

What that check does *not* cover: whether the game actually reacts the way `KeySender` expects at runtime — e.g. whether X11 `XTest` events reach the game window through gamescope's compositor on an actual Steam Deck. That needs an in-game test, which hasn't happened yet. Don't trust `KeySender` for anything important until someone's confirmed that in practice.

## Building

Requires the game installed with SMAPI, and the [`Pathoschild.Stardew.ModBuildConfig`](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig) NuGet package (already referenced in the `.csproj`), which auto-detects your game path and copies the build output into your `Mods` folder.

```sh
dotnet build
```

## License

MIT — see [LICENSE](LICENSE).
