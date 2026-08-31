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
| A / `Enter` / click | Trigger the selected entry |
| X / `E` | Open the preset manager (create, edit, duplicate, delete presets) |
| LB / RB, `[` / `]` | Cycle to the previous/next preset in the current profile |
| B / Escape | Close the menu |

Switching preset writes the choice straight back to `config.json`, so it's remembered next session too. Profiles are deliberately **not** switchable from here - see Profiles below.

**Preset manager** (`Framework/PresetManagerMenu.cs`, opened with X from the Quick Menu): lists every saved preset plus a "+ New Preset" entry.

| Button | Action |
| --- | --- |
| A / `Enter` / click | Open the highlighted preset in the action-toggle editor (see below); on "+ New Preset", name a new one first |
| Y | Duplicate the highlighted preset - name the copy, then it opens straight into editing |
| B | Back to the Quick Menu |

Deleting a preset isn't done from this screen - open it in the action-toggle editor below and delete it from there. This menu used to have its own separate request/confirm delete flow, but it overlapped with the editor's and was removed once the editor's flow was confirmed working reliably - one delete path instead of two.

**Action-toggle editor** (`Framework/PresetEditMenu.cs`, reached by opening a preset from the manager): lists every individual action across every mod in the active profile with a checkbox, regardless of what's currently in the preset. Presets are built at the action level, not the whole-mod level - a preset can include just one specific action from a mod that has a dozen, without dragging the other eleven along.

| Button | Action |
| --- | --- |
| D-pad / left stick | Move the selection (scrolls the same way the Quick Menu does) |
| A / `Enter` / click | Toggle the highlighted action in/out of this preset |
| Y / `P` | Save - writes the current checkboxes back to this preset's name and switches the Quick Menu to it |
| RT / `L` | Toggle whether deleting *this* preset is unlocked (see below) |
| LT / `Delete`/`Backspace`/`-` | Only does anything once unlocked: deletes the *whole preset* being edited (not a single action) immediately, no further confirmation |
| B | Cancel - discards any toggles made this visit; the preset keeps whatever it was before you opened the editor |

This is now the only place a preset can be deleted from. **Deletion is locked by default every time this screen opens**, and RT (or `L`) has to unlock it first - locked, LT just shows a message telling you to unlock it first. Once unlocked, LT deletes immediately with no separate confirmation step; A keeps its normal job (toggling an action) regardless of the lock state, so there's no risk of an accidental A press deleting anything. This replaced an earlier design where several buttons (X/LB/RB/LT/right-click, and A too once unlocked) all triggered the same delete, layered under a request-then-confirm step on top of the lock: in testing, the lock toggle worked reliably, but no button reliably triggered the confirm step afterwards, and by that point the lock was already doing the job a confirm step is for (a deliberate, distinct action before anything destructive can happen) - so both the confirm step and the extra alternative buttons were dropped in favor of one dedicated button (LT) for one dedicated job.

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

Two preset names are reserved and can't be deleted (enforced in `PresetManager.DeletePreset`, not just in the menu, so a UI change can't accidentally reopen the hole):

- **"All"** isn't a real saved preset at all - it's a special case in `GetActivePresetEntries` that bypasses filtering entirely, so there's nothing on disk to delete and no "All.json" can be created to shadow it.
- **"Radial Menu"** (`PresetManager.RadialPresetName`) *is* a real, saved preset - always present even before it's ever been edited - that exists solely to feed the [radial menu](#radial-menu-experimental) below. It shows up in the preset manager and is edited exactly like any other preset (open it, toggle actions, Y to save), just without any way to delete it; RT/LT both explain why instead of doing anything, and saving it doesn't switch the Quick Menu's active preset the way saving a normal preset does.

## Triggering the actual keybind

When you select an entry, `Framework/KeySender.cs` simulates the real input, using a platform-specific injector:

- **Windows** (including Steam Deck players running the Windows build of the game via Proton): uses the Win32 `SendInput` API, the same mechanism real Windows automation tools use. This works under Wine/Proton because Wine implements the same `user32.dll` entry point for compatibility.
- **Native Linux** (e.g. Steam Deck running the Linux build of the game — this is what my own Deck runs): uses the X11 `XTest` extension, which works when the game's window is on an X11 or XWayland display (the normal case under gamescope).

**Known limitation:** neither backend can simulate a *gamepad* button press (an `SButton` like `ControllerA` or `LeftShoulder`, which SMAPI reads from the real `GamePadState`) — there's no portable equivalent of `SendInput`/`XTest` for joystick input on either platform. This only matters if a mod's keybind requires a controller button with no keyboard/mouse alternative. It does *not* apply to a controller button that Steam Input (or similar) remaps to emit a keyboard key - that arrives as a genuine keyboard event before it ever reaches the game, same as anything `KeySender` sends. When a keybind lists multiple alternatives (e.g. `"F, ControllerBack"`), `KeySender` picks whichever it can actually send; if every alternative requires a controller button, it logs a warning instead of silently doing nothing. In the real 27-mod scan under `example-setup/`, only two actions hit this - and both have a working keyboard-bound duplicate anyway.

## Radial menu (experimental)

`Framework/RadialMenu.cs`, wired up in `ModEntry.OnUpdateTicked`, is a first pass at "hold a button to pop up a radial menu, tilt the stick toward an entry, release to trigger it." It's disabled by nothing (there's no on/off switch yet) but effectively opt-in, since its hold-button defaults to `ControllerBack` - a button nothing else in this project uses - so it won't fire unless you rebind it. Its entries come from a dedicated, always-present preset (`PresetManager.RadialPresetName`, "Radial Menu" - see Presets above), not whatever preset happens to be active in the Quick Menu - a dozen-mod "All" view would make for very cramped wedges, and reading from the active preset would mean the radial menu's contents changed every time you switched presets elsewhere, which isn't what you'd expect from something meant to be a small, curated, always-the-same set of go-to actions.

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
- [x] Gave the radial menu its own dedicated, always-present preset (see Presets below) instead of reading whatever's currently active - a dozen-mod "All" view makes for very cramped wedges, and this also means switching presets in the Quick Menu no longer changes what's on the radial menu

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

Yet another round of testing found A doing nothing when pressed on the Quick Menu's "All" preset, and delete still not working in the preset manager - both while touchscreen taps worked fine for the same actions. The likely explanation: this player's own SMAPI mods are triggered via Steam Input mapping their controller to emit keyboard keys rather than raw gamepad button presses (their words, from earlier in this project), and the same is plausibly true for how they interact with menus in general - if a controller button is mapped to send `Enter` rather than a native `Buttons.A` press, `receiveGamePadButton` never sees it at all, which would explain "A does nothing" while a genuine click (`receiveLeftClick`) works fine. Rather than trying to diagnose their exact Steam Input configuration, added `Enter` as a keyboard-equivalent fallback for `A` across all four menus - a hedge that helps regardless of whether the real cause turns out to be this or something else. Also shortened the preset manager's delete hint text (it was long enough to get cut off mid-word, e.g. "X/right-...", by the same `TextLayout.FitToWidth` truncation meant to prevent overflow - split across two lines instead), and widened the safety margin on every row-label width budget after a report of action labels still getting clipped by the box edge in the action-toggle editor specifically - unable to pin down an exact cause without live access, so leaned on a larger margin as a defensive fix rather than a root-cause one.

The `Enter` fallback helped (actions can now be toggled into a preset), but two things remained broken, plus a new symptom: the mouse cursor sometimes jumping to the top-left of the screen while scrolling with the left stick. That turned out to explain both remaining problems at once. The mechanism: `applyMovementKey` (called to move the snap cursor) snaps the *on-screen* cursor to the target row's bounds as part of the same call - but `UpdateRowBounds`, which recalculates a row's position for the current scroll offset, was only ever called *after* `applyMovementKey`. So the moment a row that was previously scrolled off-screen (parked at placeholder bounds like `(-10000, -10000, 1, 1)`) became the new target, the cursor snapped to those stale coordinates first - which the game clamps into the corner of the screen, hence the jump. That same desync meant the cursor's real screen position could be well away from wherever the highlight visually was, which is exactly why right-click (the touch/mouse alternative for requesting a delete) kept missing every row: the click was landing wherever the stale cursor position actually was, not on the highlighted preset. Fixed in all three list menus (`QuickMenu`, `PresetManagerMenu`, `PresetEditMenu`) by predicting which row `applyMovementKey` is about to snap to (from the current row's own neighbor-id fields) and scrolling it into view *before* calling `applyMovementKey`, not just after.

Separately, checked `KeySender`'s actual behavior directly by reading the SMAPI log over SSH rather than asking for another round of "does this work" - confirmed `libXtst.so.6` is present on this Deck and no error/warning ever came from the mod, meaning `KeySender.Send` believed it succeeded every time, yet the target mod's action never happened. That points at a specific, well-known `XTest` gotcha: sending a key-down immediately followed by a key-up with zero delay between them can be too fast for the target application's own input polling to ever observe the "down" state at all. Fixed by holding the combo briefly (50ms) before releasing - paced by the X server itself via `XTestFakeKeyEvent`'s own delay parameter once the request is queued, not a blocking sleep on the game's thread. Applied the same fix to `WindowsInputInjector`, using a background-task delay between two separate `SendInput` calls instead (`SendInput` has no built-in pacing, and a blocking sleep there would stall the game's main thread since `Send` runs on it).

Delete via the preset manager still wasn't confirmed working even after the cursor-jump fix, so rather than keep chasing it in that screen, added a second, independent way to delete reachable from the action-toggle editor instead (X requests, A/Enter/click confirms, anything else cancels - same pattern, different screen) - a screen where interaction was already confirmed working, since actions could already be toggled in there with A. Both delete paths remain; this one's just easier to reach if the preset manager's own navigation is still giving trouble.

Even the second, independent delete path still didn't work, and both share one thing: X is the "natural" button for requesting it, while A, Y, and B had all been separately confirmed working elsewhere (toggling actions with A, reaching the duplicate-naming prompt with Y, exiting menus with B). That's a specific signal rather than a general one - it points at X itself, not at delete's logic or this project's navigation code. The likely cause: a Steam Input binding (or an OS/Deck-level shortcut) intercepting X before it ever reaches the game, which no amount of fixing this mod's own button-handling code can work around. Rather than keep guessing at what X actually does on this setup, added several alternative triggers that don't conflict with anything else in either screen - LB, RB, and LT (this mod no longer cycles profiles in-game, so the triggers were free) alongside X for requesting, and RT alongside A for confirming - so the player isn't dependent on any single button turning out to work. Also added a deliberate extra layer on top, by request: deletion in the action-toggle editor is now locked every time the screen opens, and requires an explicit unlock (RT, or `L`) before a delete request does anything at all - on the reasoning that a request/confirm split alone still lets a single habitual button-mash through if both inputs land close together.

The lock toggle (RT/`L`) itself was then confirmed working reliably - it visibly switches states every time. But once unlocked, no button reached the confirm step afterwards, regardless of which one was pressed - a different symptom from "X specifically doesn't work," since by this point *every* button, including A (already confirmed working for toggling actions on the very same screen), failed to confirm a pending delete. That rules out any single button being intercepted, and points instead at the request/confirm split itself no longer being a reliable safety mechanism worth keeping on top of the lock. Removed it: `PresetEditMenu.PendingDelete` and its whole state machine are gone, and once `DeletionEnabled` is true, every existing delete trigger (A/`Enter`/click, plus X/LB/RB/LT/right-click) deletes the preset immediately with no separate confirmation screen. The lock toggle is now the *only* safety gate - a deliberate, distinct action has to happen before delete does anything at all, which was the actual purpose the confirm step was serving; a second step doing the same job on top of it was redundant even before it turned out to be unreliable.

Deleting also now explicitly re-locks (`DeletionEnabled = false`) as part of the same method that deletes, rather than relying only on the fact that the menu closes right afterward (a fresh `PresetEditMenu` always opens locked anyway) - belt-and-braces so the lock can never be left in the unlocked state, even if a future change made this method reachable without also closing the menu.

RT-then-A confirmed working - unlocking and deleting worked, but only from inside the action-toggle editor for that specific preset, which is exactly the one place it's meant to work. With that confirmed reliable, simplified further per request: the preset manager's own separate delete flow (X/LB/RB/LT/right-click to request, A/RT/Enter/click to confirm) was removed entirely, since it's now a redundant, less-reliable second way to do the same thing. The action-toggle editor is the single remaining place to delete a preset, and its own controls were narrowed to one dedicated button per job instead of several alternatives standing in for each other: RT unlocks, LT deletes immediately once unlocked, and A goes back to only ever toggling an action (it no longer doubles as a delete trigger when unlocked). The several alternative delete buttons added earlier while X's reliability was still in question (X/LB/RB/right-click) are gone now that a specific working combination is confirmed.

Per request, gave the radial menu a dedicated preset instead of having it read whichever one happens to be active: `PresetManager.RadialPresetName` ("Radial Menu") is a real, saved preset like any other - built by toggling actions in the same action-toggle editor - except it always exists (auto-created in memory on `LoadProfile` if no file for it exists yet, the same pattern "All" doesn't need since it's never a real preset at all) and can't be deleted. That guard lives in three places on purpose, all independently reachable: `PresetManager.DeletePreset` refuses the name outright (so no future menu code can reopen the hole by forgetting to check), `PresetEditMenu` never lets `DeletionEnabled` become true for it in the first place (`ToggleDeletionLock` just explains why instead of toggling), and the delete button itself also checks directly rather than relying solely on the lock never being set. `PresetEditMenu.Save()` also skips the usual "switch the Quick Menu to whatever I just saved" behavior for this one preset - saving your radial loadout shouldn't silently change what the Quick Menu shows. While making this change, applied the same "can't be deleted" guarantee to "All" as well, since it was asked to be double-checked: it turned out to already be unreachable end-to-end (never added to the presets dictionary, never listed as a row in the preset manager), but added an explicit guard in `DeletePreset` and blocked creating a new preset literally named "All" too, rather than leaving that safety implicit in "the lookup happens to fail."

Adding the lock label to the action-toggle editor's status line made it long enough to visibly run into the "X-Y of Z" scroll counter, which is right-aligned on the same line - both `FitToWidth` calls used the menu's full inner width as their budget, with neither aware the other one exists. Fixed by measuring the counter first and reserving its width (plus a small gap) out of the status line's budget before fitting it, in both this menu and `QuickMenu` (same layout, same latent risk from a long profile/preset name - just not yet reported there).

What a compile check and this playtesting still don't cover: whether LT reliably deletes across other presets and other sessions (confirmed for one so far), whether the XTest timing fix actually resolves triggering, and whether the radial menu's direction math has its sign conventions right, now sourced from its own dedicated preset. Also still open: a report that A stopped toggling actions in the action-toggle editor, which doesn't match anything in the code (A's handler is unconditional there) - needs on-device follow-up to tell an input-mapping issue (the pattern seen before with X/delete on this player's Steam Input setup) apart from something more code-related that isn't reproducible from reading the source alone.

## Building

Requires the game installed with SMAPI, and the [`Pathoschild.Stardew.ModBuildConfig`](https://www.nuget.org/packages/Pathoschild.Stardew.ModBuildConfig) NuGet package (already referenced in the `.csproj`), which auto-detects your game path and copies the build output into your `Mods` folder.

```sh
dotnet build
```

## License

MIT — see [LICENSE](LICENSE).
