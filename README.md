# Controller Quick Menu

A [SMAPI](https://smapi.io/) mod for Stardew Valley that solves a specific problem for controller and Steam Deck players: once you install enough mods, you run out of physical keys/buttons for keybinds.

Instead of binding every mod action to its own key, this mod gives you **one** button (or button combo) that opens a menu listing every installed mod's actions and their keybinds. Navigate the list with your gamepad and select an entry to trigger it.

## How it works

1. After installing a mod, you (or the mod's installer/instructions) add an entry for it to your active profile's `entries.json` — the mod's name, its actions, the keybind for each action, and a short description of what it does.
2. In-game, press the configured menu button (default: `LeftShoulder + RightShoulder`) to open the menu.
3. Navigate with the d-pad/left stick and press A (or click) on an entry. The mod simulates the real keypress/click that entry's keybind maps to, so the target mod reacts exactly as if you'd pressed it yourself.

See [`example-setup/`](example-setup/) for a full, real-world example — a profile generated from an actual 200+ mod Steam Deck install.

### Menu controls

**A design rule that runs through every screen in this mod: B always means "back / cancel without saving," never anything else.** Where a screen also has an on-screen Cancel button (the naming prompt), B does the same thing as clicking it.

**Quick Menu** (the main list):

| Button | Action |
| --- | --- |
| D-pad / left stick | Move the selection (scrolls automatically once the list is longer than one screen) |
| A / click | Trigger the selected entry |
| X / `E` | Open the preset manager (create, edit, duplicate, delete presets) |
| LB / RB, `[` / `]` | Cycle to the previous/next preset in the current profile |
| B / Escape | Close the menu |

Switching preset writes the choice straight back to `config.json`, so it's remembered next session too. Profiles are deliberately **not** switchable from here - see Profiles below.

**Preset manager** (`Framework/PresetManagerMenu.cs`, opened with X from the Quick Menu): lists every saved preset plus a "+ New Preset" entry.

| Button | Action |
| --- | --- |
| A / click | Open the highlighted preset in the action-toggle editor (see below); on "+ New Preset", name a new one first |
| Y | Duplicate the highlighted preset - name the copy, then it opens straight into editing |
| X | Request deletion of the highlighted preset |
| B | Back to the Quick Menu |

Deleting requires a second, *different* button on purpose: pressing X shows "Delete '\<name\>'? A = confirm. Any other button = cancel" - so a habitual double-tap of the same button can't delete something by accident. If the preset you delete was the active one, the Quick Menu falls back to "All".

**Action-toggle editor** (`Framework/PresetEditMenu.cs`, reached by opening a preset from the manager): lists every individual action across every mod in the active profile with a checkbox, regardless of what's currently in the preset. Presets are built at the action level, not the whole-mod level - a preset can include just one specific action from a mod that has a dozen, without dragging the other eleven along.

| Button | Action |
| --- | --- |
| D-pad / left stick | Move the selection (scrolls the same way the Quick Menu does) |
| A / click | Toggle the highlighted action in/out of this preset |
| Y / `P` | Save - writes the current checkboxes back to this preset's name and switches the Quick Menu to it |
| B | Cancel - discards any toggles made this visit; the preset keeps whatever it was before you opened the editor |

## Profiles

Every setting lives under a **profile**: a named folder of `entries.json` + `presets/`. This is meant for players who swap modpacks — keep one profile per modpack, and switch by editing `ActiveProfile` in `config.json`, then relaunching. This is deliberately a config edit, not an in-game control: a profile only needs to change when the actual set of installed mods changes, which happens outside the game anyway (installing/removing mods, then editing that profile's `entries.json` to match) - there's no scenario where switching profiles mid-session makes sense the way switching presets does.

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

Within a profile, every player has a default "All" view showing every entry. You can also define presets — named, filtered subsets of individual actions (not whole mods; a preset can cherry-pick one action out of a mod with a dozen) — stored as individual files under that profile's `presets/` folder.

```json
{
    "Name": "Favorites",
    "IncludedActionKeys": ["Example Mod|Toggle Debug Overlay"]
}
```

Each entry in `IncludedActionKeys` is `"<ModName>|<Action Name>"`, matching `ActionKey.Of` in `Framework/EntryModels.cs`. Presets are built entirely in-game, through the preset manager and action-toggle editor described under Menu controls above: name it, then toggle individual actions into it from the full list, rather than hand-editing this JSON between sessions. Set `ActivePreset` in `config.json` to change the default at launch, or switch/cycle it live from the Quick Menu (unlike profiles, switching presets mid-session is exactly what they're for).

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
- [x] ~~In-game profile switcher~~ - removed by request: profiles only change when the modpack does, which happens outside the game, so this is a `config.json` edit rather than an in-menu control
- [x] Radial menu prototype (see "Radial menu (experimental)" above) - compiles, not yet verified in-game
- [x] First real in-game test - found and fixed the chat-opening conflict, broken D-pad navigation, the uncancelable naming prompt, and text/scrolling overflow (see Testing below)
- [x] Root-caused navigation properly (two independent systems both moving the cursor per press) and switched presets to per-action rather than per-mod inclusion (see Testing below)
- [x] Full code review pass plus an independent agent tracing concrete user journeys through the actual code - found and fixed a real data-loss bug, a "cancel doesn't actually cancel" bug, silent trigger failures, and a Windows-only mouse-button injection gap (see Testing below)
- [ ] Confirm navigation, preset editing, and the fixes above actually work on a real playtest of this latest round, and specifically verify `KeySender` triggers other mods and the radial menu's direction math is correct
- [ ] Decide whether the radial menu needs its own on/off setting and a dedicated small preset (a dozen-mod "All" view makes for very cramped wedges), or should stay a `RadialMenuButton`-gated experiment

## Testing

This project compiles cleanly against the real Stardew Valley 1.6.15 / SMAPI 4.5.2 assemblies (verified by building against copies of those DLLs directly, since this dev environment has no game install for `Pathoschild.Stardew.ModBuildConfig` to auto-detect). That caught two real bugs (missing `using` directives in `QuickMenu.cs`) before they ever reached a player.

A compile check can't catch everything a real playtest does, though. The first actual in-game run surfaced several real bugs, found by decompiling the game's own `IClickableMenu`/`Game1`/`NamingMenu` classes rather than guessing twice:

- **Opening the menu also opened chat.** Turned out `RightStick` click has a hardcoded vanilla behavior (open chat on a quick press, the emote wheel on a long hold) that reads `GamePadState` directly and bypasses SMAPI's input suppression entirely - not fixable by suppressing the button. Fixed by moving `OpenMenuButton`'s default off any stick click and back to `LeftShoulder + RightShoulder`, now paired with `SuppressActiveKeybinds` so it doesn't also fire another mod bound to the same combo (which *is* a suppressible, SMAPI-mediated conflict, unlike the stick one).
- **D-pad/stick navigation didn't move the selection at all.** `IClickableMenu.receiveGamePadButton` and `gamePadButtonHeld` are both no-op stubs in the base class - every vanilla menu wires its own snap navigation by calling `applyMovementKey`, and `QuickMenu` never did. Fixed by handling D-pad and left-thumbstick directions explicitly, with throttled auto-repeat while held. Separately, the keyboard shortcut for "save preset" was bound to `S` - the default vanilla move-down key - which unconditionally swallowed that keypress before the base class's own keyboard navigation could see it; moved to `P`.
- **Couldn't cancel out of naming a new preset.** The game's own `NamingMenu` has no cancel path at all by design (it's built for mandatory naming, like naming a pet) - confirmed by decompiling it. Replaced with `Framework/PresetNamePrompt.cs`, a minimal from-scratch prompt with working Escape/B/Cancel-button handling.
- **Text spilled outside the menu, and long lists overlapped the control hints.** There was no scrolling and no width limit on labels - fine for a handful of entries, but a real ~27-mod profile produces 60-90 rows. Added a scrolling window that follows the selection, an "X-Y of Z" counter, and label truncation with an ellipsis so nothing draws past the menu's edge.

A second pass after that turned up two more: the header itself (`"Quick Menu - Profile: SteamDeck - Preset: All"`) was drawn in Stardew's big decorative `SpriteText` font, which is fine for a short fixed title but overflowed the box once it included variable-length profile/preset names, clipping into the row list below it. Split into a short fixed `SpriteText` title plus a compact `smallFont` status line underneath, with content's vertical start computed from `SpriteText.getHeightOfString` instead of a guessed pixel offset. Separately, gamepad B did nothing at all in the Quick Menu (it only closes vanilla menus by way of a keyboard-only shortcut check in the base class, which doesn't cover controllers) - added explicit B/Escape-to-close handling.

While rebuilding the preset workflow (see Presets above), settled on one rule that now holds across every screen this mod adds: **B always means "back / cancel without saving," and never anything else** - answering a real point of confusion once B was doing different, undocumented things in different screens.

Navigation still wasn't reliable after all of the above, on a further round of testing - the actual root cause took a third pass to pin down properly instead of guessing again. The real mechanism: the game runs *two independent* systems that both react to the same physical D-pad/stick press when a custom menu is open - the raw button event this mod's `receiveGamePadButton` handles directly, and a completely separate per-tick poll in `Game1` (`directionKeyPolling`) that, whenever `Game1.options.snappyMenus && gamepadControls` are both true, translates the same press into a *synthetic* WASD keypress delivered to `receiveKeyPress`. Every previous attempt either handled both (double-moving the cursor on every press) or relied solely on the synthetic-keypress path (which does nothing when `gamepadControls` isn't set the way this project assumed it would be - matching "still not working"). The fix that's actually deterministic regardless of those option flags: handle navigation *exclusively* via the raw button/key events (`receiveGamePadButton` for D-pad/thumbstick, `receiveKeyPress` for literal arrow keys), and never call `base.receiveGamePadButton`/`base.receiveKeyPress` for anything, in any of the four menu classes - severing the synthetic-keypress path entirely rather than trying to coexist with it. Typing in `PresetNamePrompt` is unaffected, since it goes through `Game1.keyboardDispatcher.Subscriber`, a wholly separate pipeline from `receiveKeyPress`.

Also switched presets from whole-mod inclusion to individual-action inclusion (`Preset.IncludedActionKeys` instead of `IncludedModNames` - see Presets above) - picking a specific action out of a mod that exposes several wasn't possible before. This is a breaking change to the preset file format; existing preset JSON files need rebuilding (or manually converting `"ModName"` entries to `"ModName|Action Name"` per action).

Between rounds of in-game testing, did a full review pass without waiting for another bug report: read every file end-to-end myself, and separately had a fresh agent (no memory of writing any of this code) desk-check concrete user journeys - create a preset, duplicate one, cancel out of each screen, delete, cycle presets/profiles, open the radial menu with zero items - by tracing the actual method calls, the same way a human would step through a debugger. It found two real bugs a live playtest likely wouldn't have caught immediately:

- **Creating or duplicating a preset with a name that already exists silently overwrote it, with no warning, before the edit screen even opened.** `PresetManager.SavePreset` does an unconditional dictionary/file overwrite, and `PresetManagerMenu` never checked for a collision first. Fixed: `CreateAndEdit` now checks `GetPresetNames()` first and, on a collision, shows `Game1.showRedMessage` and re-opens the naming prompt with what was typed still filled in, instead of touching anything.
- **"B: cancel (discards changes)" was a lie for a freshly-created preset.** Because the preset is saved to disk as soon as its name is submitted (so `PresetEditMenu` has something to write back to), cancelling out of editing a brand-new preset left an empty (or duplicated) stub behind permanently - `PresetEditMenu.Cancel()` never rolled that back. Fixed by threading an `isNewPreset` flag through from `PresetManagerMenu`: cancelling now actually deletes the just-created stub when it's new, while correctly leaving a pre-existing preset untouched when you were only ever editing one that was already there. The hint text now says which one applies.

It also caught two things I'd already half-suspected but hadn't fixed: `KeySender`/`RadialMenu` closed the menu and played the same "select" sound whether or not the keybind was actually sent, so a failure (unparseable keybind, no working injector, every alternative needing a button the platform can't simulate) was completely silent to the player - now shows `Game1.showRedMessage` when nothing was actually triggered. And `WindowsInputInjector` only ever implemented keyboard keys via `SendInput`, not mouse buttons, unlike its Linux/X11 counterpart which already had both - a real cross-platform gap for anyone whose keybind is mouse-bound (e.g. `AutomaticGates`' "Mark Gate as Ignored" in the example-setup data), now fixed with a `MOUSEINPUT`-based implementation mirroring the existing keyboard one.

What a compile check and this playtesting still don't cover: whether `KeySender`'s X11 `XTest` calls actually reach the game window through gamescope's compositor, and whether the radial menu's direction math has its sign conventions right. Both need another real test.

## Building

Requires the game installed with SMAPI, and the [`Pathoschild.Stardew.ModBuildConfig`](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig) NuGet package (already referenced in the `.csproj`), which auto-detects your game path and copies the build output into your `Mods` folder.

```sh
dotnet build
```

## License

MIT — see [LICENSE](LICENSE).
