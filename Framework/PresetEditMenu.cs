using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewControllerMenu.Framework
{
    /// <summary>
    /// Lists every individual action across every mod in the active profile with a checkbox, for
    /// building one named preset. A toggles an action in/out. Y saves back to that preset name and
    /// returns to the quick menu with it active. B discards any changes made this visit - for a
    /// preset that already existed before this screen opened, that means leaving it as it was; for
    /// one just created via "+ New Preset" or Duplicate (<paramref name="isNewPreset"/> in the
    /// constructor), it means actually deleting the just-created stub, since otherwise "discards
    /// changes" would be a lie - the empty (or duplicated) preset would persist on disk even after
    /// cancelling out of creating it.
    ///
    /// Deleting the whole preset being edited (not a single action) is locked by default
    /// (<see cref="DeletionEnabled"/>) - RT/L unlocks it for the rest of this visit. Once unlocked,
    /// the delete triggers (X/LB/RB/LT/right-click, and A/Enter/click too) delete immediately with no
    /// further confirmation - the unlock step itself is the safety gate, by design: an earlier
    /// request-then-confirm split on top of the lock turned out to be one safety mechanism too many
    /// once a player found even the confirm step unreliable on their setup, so it was removed in
    /// favor of this simpler two-state design (locked = safe, unlocked = one press deletes).
    /// </summary>
    public class PresetEditMenu : IClickableMenu
    {
        private readonly IModHelper Helper;
        private readonly ModConfig Config;
        private readonly PresetManager Presets;
        private readonly string PresetName;
        private readonly bool IsNewPreset;

        private readonly List<(ModListing Mod, ModAction Action)> AllActions = new();
        private readonly List<ClickableComponent> RowComponents = new();
        private readonly HashSet<string> Selected;

        private bool DeletionEnabled;
        private int ScrollOffset;

        private const int RowHeight = 64;
        private const int ContentTop = 96;
        private const int VisibleRows = 7;

        public PresetEditMenu(IModHelper helper, ModConfig config, PresetManager presets, string presetName, IEnumerable<string> initiallyIncludedActionKeys, bool isNewPreset)
            : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 300, 800, 600, showUpperRightCloseButton: true)
        {
            this.Helper = helper;
            this.Config = config;
            this.Presets = presets;
            this.PresetName = presetName;
            this.IsNewPreset = isNewPreset;

            foreach (ModListing mod in this.Presets.GetAllEntries())
            {
                foreach (ModAction action in mod.Actions)
                    this.AllActions.Add((mod, action));
            }
            this.Selected = new HashSet<string>(initiallyIncludedActionKeys);

            this.BuildRowComponents();
            if (this.RowComponents.Count > 0)
                this.currentlySnappedComponent = this.RowComponents[0];
            this.snapCursorToCurrentSnappedComponent();
        }

        private void BuildRowComponents()
        {
            this.RowComponents.Clear();
            for (int i = 0; i < this.AllActions.Count; i++)
            {
                this.RowComponents.Add(new ClickableComponent(new Rectangle(), i.ToString())
                {
                    myID = i,
                    upNeighborID = i > 0 ? i - 1 : -1,
                    downNeighborID = i < this.AllActions.Count - 1 ? i + 1 : -1
                });
            }

            this.allClickableComponents = new List<ClickableComponent>(this.RowComponents);
            this.UpdateRowBounds();
        }

        /// <param name="ensureVisibleId">Scroll to keep this row's id visible instead of the currently snapped one - see <see cref="Move"/> for why that distinction matters.</param>
        private void UpdateRowBounds(int? ensureVisibleId = null)
        {
            int targetId = ensureVisibleId ?? this.currentlySnappedComponent?.myID ?? -1;
            if (targetId >= 0)
            {
                if (targetId < this.ScrollOffset)
                    this.ScrollOffset = targetId;
                else if (targetId >= this.ScrollOffset + VisibleRows)
                    this.ScrollOffset = targetId - VisibleRows + 1;
            }
            this.ScrollOffset = System.Math.Clamp(this.ScrollOffset, 0, System.Math.Max(0, this.AllActions.Count - VisibleRows));

            for (int i = 0; i < this.RowComponents.Count; i++)
            {
                int slot = i - this.ScrollOffset;
                this.RowComponents[i].bounds = (slot >= 0 && slot < VisibleRows)
                    ? new Rectangle(this.xPositionOnScreen + 32, this.yPositionOnScreen + ContentTop + slot * RowHeight, this.width - 64, RowHeight - 8)
                    : new Rectangle(-10000, -10000, 1, 1);
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (this.DeletionEnabled)
            {
                this.DeleteNow();
                return;
            }

            for (int i = 0; i < this.RowComponents.Count; i++)
            {
                if (this.RowComponents[i].containsPoint(x, y))
                {
                    this.ToggleRow(i);
                    return;
                }
            }
        }

        /// <summary>Right-click deletes immediately once unlocked, mirroring gamepad X. Unlike PresetManagerMenu's row-scoped delete, this isn't tied to a specific row - the whole screen is already scoped to one preset - so it fires regardless of where the click lands. While locked, it's a no-op (there's no "request" state left to cancel out of).</summary>
        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (this.DeletionEnabled)
                this.DeleteNow();
        }

        private static int? DirectionOf(Buttons button)
        {
            return button switch
            {
                Buttons.DPadUp or Buttons.LeftThumbstickUp => 0,
                Buttons.DPadRight or Buttons.LeftThumbstickRight => 1,
                Buttons.DPadDown or Buttons.LeftThumbstickDown => 2,
                Buttons.DPadLeft or Buttons.LeftThumbstickLeft => 3,
                _ => null
            };
        }

        private static int? DirectionOf(Keys key)
        {
            return key switch
            {
                Keys.Up => 0,
                Keys.Right => 1,
                Keys.Down => 2,
                Keys.Left => 3,
                _ => null
            };
        }

        public override void receiveGamePadButton(Buttons button)
        {
            if (DirectionOf(button) is int direction)
            {
                this.Move(direction);
                return;
            }

            switch (button)
            {
                case Buttons.A:
                    if (this.DeletionEnabled)
                        this.DeleteNow();
                    else if (this.currentlySnappedComponent != null)
                        this.ToggleRow(this.currentlySnappedComponent.myID);
                    return;

                case Buttons.Y:
                    this.Save();
                    return;

                // X is the "natural" button for this, but A/Y/B have all been confirmed working
                // for this player while X specifically has not - possibly a button that a Steam
                // Input binding intercepts before it reaches the game at all. LB/RB/LT aren't used
                // for anything else in this menu (this mod no longer cycles profiles in-game,
                // freeing the triggers up), so they're offered as alternatives rather than betting
                // everything on X being fixable.
                case Buttons.X:
                case Buttons.LeftShoulder:
                case Buttons.RightShoulder:
                case Buttons.LeftTrigger:
                    if (this.DeletionEnabled)
                        this.DeleteNow();
                    else
                        Game1.showRedMessage("Deletion is locked - press RT (or L) to unlock it first.");
                    return;

                case Buttons.RightTrigger:
                    this.ToggleDeletionLock();
                    return;

                case Buttons.B:
                    this.Cancel();
                    return;
            }

            // Deliberately no fallthrough to base.receiveGamePadButton - see the note in receiveKeyPress.
        }

        public override void receiveKeyPress(Keys key)
        {
            if (DirectionOf(key) is int direction)
            {
                this.Move(direction);
                return;
            }

            switch (key)
            {
                // Enter mirrors A - see the same fallback in QuickMenu.receiveKeyPress for why.
                case Keys.Enter:
                    if (this.DeletionEnabled)
                        this.DeleteNow();
                    else if (this.currentlySnappedComponent != null)
                        this.ToggleRow(this.currentlySnappedComponent.myID);
                    return;

                case Keys.P:
                    this.Save();
                    return;

                case Keys.Delete:
                case Keys.Back:
                case Keys.OemMinus:
                    if (this.DeletionEnabled)
                        this.DeleteNow();
                    else
                        Game1.showRedMessage("Deletion is locked - press L (or RT) to unlock it first.");
                    return;

                case Keys.L:
                    this.ToggleDeletionLock();
                    return;

                case Keys.Escape:
                    this.Cancel();
                    return;
            }

            // Deliberately NOT falling through to base.receiveKeyPress here (or in
            // receiveGamePadButton above): the game independently translates gamepad D-pad/stick
            // input into synthetic WASD keypresses for its own menu navigation whenever
            // Game1.options.snappyMenus && gamepadControls are both true, on top of the raw button
            // event this class already handles directly via receiveGamePadButton. Falling through
            // to base as well double-moved the cursor on every single press - confirmed the hard
            // way as "always skips one option." Handling navigation exclusively through the two
            // DirectionOf() checks above (arrow keys for keyboard, D-pad/thumbstick for gamepad)
            // removes that second, uncontrollable path entirely.
        }

        /// <summary>
        /// applyMovementKey snaps the on-screen cursor to the target row's bounds as part of the same
        /// call - if that row was still positioned off-screen (from the last time the view was
        /// scrolled), the cursor jumps to those stale coordinates first. Scrolling the target into
        /// view before calling applyMovementKey, instead of only after, avoids that jump.
        /// </summary>
        private void Move(int direction)
        {
            if (this.currentlySnappedComponent != null)
            {
                int nextId = direction switch
                {
                    0 => this.currentlySnappedComponent.upNeighborID,
                    1 => this.currentlySnappedComponent.rightNeighborID,
                    2 => this.currentlySnappedComponent.downNeighborID,
                    3 => this.currentlySnappedComponent.leftNeighborID,
                    _ => -1
                };
                if (nextId >= 0)
                    this.UpdateRowBounds(nextId);
            }

            this.applyMovementKey(direction);
            this.UpdateRowBounds();
        }

        private void ToggleRow(int index)
        {
            if (index < 0 || index >= this.AllActions.Count)
                return;

            (ModListing mod, ModAction action) = this.AllActions[index];
            string key = ActionKey.Of(mod.ModName, action.Name);
            if (!this.Selected.Remove(key))
                this.Selected.Add(key);
            Game1.playSound("drumkit6");
        }

        private void Save()
        {
            this.Presets.SavePreset(this.PresetName, this.Selected);
            this.Config.ActivePreset = this.PresetName;
            this.Helper.WriteConfig(this.Config);
            Game1.playSound("bigSelect");
            Game1.activeClickableMenu = new QuickMenu(this.Helper, this.Config, this.Presets);
        }

        private void Cancel()
        {
            if (this.IsNewPreset)
                this.Presets.DeletePreset(this.PresetName);

            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = new QuickMenu(this.Helper, this.Config, this.Presets);
        }

        private void ToggleDeletionLock()
        {
            this.DeletionEnabled = !this.DeletionEnabled;
            Game1.playSound(this.DeletionEnabled ? "coin" : "cancel");
        }

        /// <summary>Deletes the preset being edited with no further confirmation - only reachable once <see cref="DeletionEnabled"/> is true, which is itself the deliberate, separate safety gate.</summary>
        private void DeleteNow()
        {
            this.Presets.DeletePreset(this.PresetName);

            // Re-lock immediately: this menu closes right after anyway (a fresh PresetEditMenu
            // always opens locked), but resetting explicitly here means the lock is never left
            // unlocked even if a future change makes this method reachable without also closing
            // the menu.
            this.DeletionEnabled = false;

            if (this.Config.ActivePreset == this.PresetName)
            {
                this.Config.ActivePreset = "All";
                this.Helper.WriteConfig(this.Config);
            }

            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = new QuickMenu(this.Helper, this.Config, this.Presets);
        }

        public override void draw(SpriteBatch b)
        {
            this.UpdateRowBounds();

            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);
            if (this.DeletionEnabled)
                b.Draw(Game1.staminaRect, new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height), Color.Red * 0.15f);

            const string title = "Edit Preset";
            SpriteText.drawString(b, title, this.xPositionOnScreen + 32, this.yPositionOnScreen + 24);

            string lockLabel = this.DeletionEnabled ? "[Deletion: UNLOCKED - next press deletes!]" : "[Deletion: locked]";
            string status = TextLayout.FitToWidth($"Editing: {this.PresetName}   ({this.Selected.Count} action(s) selected)   {lockLabel}", this.width - 64);
            int titleHeight = SpriteText.getHeightOfString(title, 9999);
            int statusY = this.yPositionOnScreen + 24 + titleHeight + 4;
            Utility.drawTextWithShadow(b, status, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, statusY), this.DeletionEnabled ? Color.OrangeRed : Game1.textColor);

            if (this.AllActions.Count > VisibleRows)
            {
                string counter = $"{this.ScrollOffset + 1}-{System.Math.Min(this.ScrollOffset + VisibleRows, this.AllActions.Count)} of {this.AllActions.Count}";
                Vector2 counterSize = Game1.smallFont.MeasureString(counter);
                Utility.drawTextWithShadow(b, counter, Game1.smallFont, new Vector2(this.xPositionOnScreen + this.width - 32 - counterSize.X, statusY), Game1.textColor);
            }

            // A generous safety margin beyond the row's own bounds - reported clipping suggests
            // Game1.smallFont.MeasureString and the actual drawn width may not agree exactly here.
            float maxLabelWidth = this.width - 64 - 96;
            for (int i = this.ScrollOffset; i < System.Math.Min(this.ScrollOffset + VisibleRows, this.AllActions.Count); i++)
            {
                (ModListing mod, ModAction action) = this.AllActions[i];
                ClickableComponent row = this.RowComponents[i];

                bool isSnapped = this.currentlySnappedComponent == row;
                if (isSnapped)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.6f);

                string checkbox = this.Selected.Contains(ActionKey.Of(mod.ModName, action.Name)) ? "[x] " : "[ ] ";
                string label = TextLayout.FitToWidth($"{checkbox}{mod.ModName} - {action.Name} [{action.Keybind}]", maxLabelWidth);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(row.bounds.X + 8, row.bounds.Y + 8), Game1.textColor);
            }

            string cancelHint = this.IsNewPreset ? "B: cancel (deletes this new preset)" : "B: cancel (discards changes)";
            string hintLine1, hintLine2;
            Color hintLine2Color;
            if (this.DeletionEnabled)
            {
                hintLine1 = TextLayout.FitToWidth($"A/Enter/click/X/LB/RB/LT deletes \"{this.PresetName}\" IMMEDIATELY, no further confirmation", this.width - 64);
                hintLine2 = TextLayout.FitToWidth($"RT/L: lock deletion again (safer)   Y: save   {cancelHint}", this.width - 64);
                hintLine2Color = Game1.textColor;
            }
            else
            {
                hintLine1 = TextLayout.FitToWidth($"A/Enter: toggle action   Y: save   {cancelHint}", this.width - 64);
                hintLine2 = TextLayout.FitToWidth("RT/L: unlock deletion of this whole preset (locked by default)", this.width - 64);
                hintLine2Color = Game1.textColor;
            }
            Utility.drawTextWithShadow(b, hintLine1, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 58), this.DeletionEnabled ? Color.Red : Game1.textColor);
            Utility.drawTextWithShadow(b, hintLine2, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 36), hintLine2Color);
            this.drawMouse(b);
        }
    }
}
