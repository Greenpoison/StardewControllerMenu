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
    /// Lists every saved preset in the active profile, plus an option to create a new one.
    /// A: open the preset in <see cref="PresetEditMenu"/> to toggle its actions. Y: duplicate it (name
    /// the copy, then edit it). X: request deletion - a separate confirmation step follows, using a
    /// different button than X on purpose, so you can't delete something by pressing the same button
    /// twice out of habit. B: back to the quick menu.
    /// </summary>
    public class PresetManagerMenu : IClickableMenu
    {
        private const string NewPresetLabel = "+ New Preset";

        private readonly IModHelper Helper;
        private readonly ModConfig Config;
        private readonly PresetManager Presets;

        private readonly List<string> Rows = new();
        private readonly List<ClickableComponent> RowComponents = new();

        /// <summary>Index into <see cref="Rows"/> awaiting delete confirmation, or null if nothing is pending.</summary>
        private int? PendingDeleteIndex;

        private int ScrollOffset;

        private const int RowHeight = 64;
        private const int ContentTop = 96;
        private const int VisibleRows = 5;

        public PresetManagerMenu(IModHelper helper, ModConfig config, PresetManager presets)
            : base(Game1.uiViewport.Width / 2 - 300, Game1.uiViewport.Height / 2 - 250, 600, 500, showUpperRightCloseButton: true)
        {
            this.Helper = helper;
            this.Config = config;
            this.Presets = presets;

            this.Rows.Add(NewPresetLabel);
            this.Rows.AddRange(this.Presets.GetPresetNames().Where(name => name != "All"));

            for (int i = 0; i < this.Rows.Count; i++)
            {
                this.RowComponents.Add(new ClickableComponent(new Rectangle(), this.Rows[i])
                {
                    myID = i,
                    upNeighborID = i > 0 ? i - 1 : -1,
                    downNeighborID = i < this.Rows.Count - 1 ? i + 1 : -1
                });
            }

            this.allClickableComponents = new List<ClickableComponent>(this.RowComponents);
            if (this.RowComponents.Count > 0)
                this.currentlySnappedComponent = this.RowComponents[0];
            this.UpdateRowBounds();
            this.snapCursorToCurrentSnappedComponent();
        }

        /// <summary>Keep the given row (the snapped one by default) scrolled into view, then position each row's clickable bounds for however it's currently scrolled - off-screen rows get moved out of click range instead of being drawn. Without this, more than ~5 presets would overflow past the hint bar exactly like the Quick Menu did before it got the same treatment.</summary>
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
            this.ScrollOffset = System.Math.Clamp(this.ScrollOffset, 0, System.Math.Max(0, this.Rows.Count - VisibleRows));

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

            if (this.PendingDeleteIndex is int pendingIndex)
            {
                this.ConfirmDelete(pendingIndex);
                return;
            }

            for (int i = 0; i < this.RowComponents.Count; i++)
            {
                if (this.RowComponents[i].containsPoint(x, y))
                {
                    this.Select(i);
                    return;
                }
            }
        }

        /// <summary>Right-click requests deletion, mirroring gamepad X - there was previously no mouse/touch way to request one at all, only the gamepad button.</summary>
        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (this.PendingDeleteIndex != null)
            {
                this.CancelPendingDelete();
                return;
            }

            for (int i = 0; i < this.RowComponents.Count; i++)
            {
                if (this.RowComponents[i].containsPoint(x, y))
                {
                    this.RequestDelete(i);
                    return;
                }
            }
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
            if (this.PendingDeleteIndex is int pendingIndex)
            {
                if (button == Buttons.A || button == Buttons.RightTrigger)
                    this.ConfirmDelete(pendingIndex);
                else
                    this.CancelPendingDelete();
                return;
            }

            if (DirectionOf(button) is int direction)
            {
                this.Move(direction);
                return;
            }

            switch (button)
            {
                case Buttons.A:
                    if (this.currentlySnappedComponent != null)
                        this.Select(this.currentlySnappedComponent.myID);
                    return;

                case Buttons.Y:
                    if (this.currentlySnappedComponent != null)
                        this.Duplicate(this.currentlySnappedComponent.myID);
                    return;

                // X is the "natural" button for this, but A/Y/B have all been confirmed working
                // for this player while X and delete specifically have not - possibly a button
                // that a Steam Input binding intercepts before it reaches the game at all. LB/RB
                // and LT aren't used for anything else in this menu (this mod no longer cycles
                // profiles from in-game, freeing the triggers up), so they're offered as
                // alternative triggers rather than betting everything on X being fixable.
                case Buttons.X:
                case Buttons.LeftShoulder:
                case Buttons.RightShoulder:
                case Buttons.LeftTrigger:
                    if (this.currentlySnappedComponent != null)
                        this.RequestDelete(this.currentlySnappedComponent.myID);
                    return;

                case Buttons.B:
                    this.Close();
                    return;
            }

            // Deliberately no fallthrough to base.receiveGamePadButton - see QuickMenu's remarks for why.
        }

        public override void receiveKeyPress(Keys key)
        {
            if (this.PendingDeleteIndex is int pendingIndex)
            {
                if (key == Keys.Enter)
                    this.ConfirmDelete(pendingIndex);
                else
                    this.CancelPendingDelete();
                return;
            }

            if (DirectionOf(key) is int direction)
            {
                this.Move(direction);
                return;
            }

            // Enter mirrors A - see the same fallback in QuickMenu.receiveKeyPress for why.
            if (key == Keys.Enter && this.currentlySnappedComponent != null)
            {
                this.Select(this.currentlySnappedComponent.myID);
                return;
            }

            if (key == Keys.Escape)
            {
                this.Close();
                return;
            }

            if ((key == Keys.Delete || key == Keys.Back || key == Keys.OemMinus) && this.currentlySnappedComponent != null)
            {
                this.RequestDelete(this.currentlySnappedComponent.myID);
                return;
            }

            // Deliberately no fallthrough to base.receiveKeyPress - see QuickMenu's remarks for why.
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

        private void Select(int index)
        {
            if (index < 0 || index >= this.Rows.Count)
                return;

            if (this.Rows[index] == NewPresetLabel)
            {
                Game1.activeClickableMenu = new PresetNamePrompt(
                    "New Preset",
                    "",
                    name => this.CreateAndEdit("New Preset", name),
                    this.ReturnToSelf);
                return;
            }

            string presetName = this.Rows[index];
            IEnumerable<string> included = GetActionKeys(this.Presets.GetActivePresetEntries(presetName));
            Game1.activeClickableMenu = new PresetEditMenu(this.Helper, this.Config, this.Presets, presetName, included, isNewPreset: false);
        }

        private void Duplicate(int index)
        {
            if (index < 0 || index >= this.Rows.Count || this.Rows[index] == NewPresetLabel)
                return;

            string sourceName = this.Rows[index];
            List<string> included = GetActionKeys(this.Presets.GetActivePresetEntries(sourceName)).ToList();

            Game1.activeClickableMenu = new PresetNamePrompt(
                "Duplicate Preset",
                $"{sourceName} Copy",
                name => this.CreateAndEdit("Duplicate Preset", name, included),
                this.ReturnToSelf);
        }

        /// <summary>Creates a brand-new preset (from "+ New Preset" or Duplicate) and opens it for editing. Refuses to overwrite an existing preset silently - <see cref="PresetManager.SavePreset"/> would happily clobber one, so this checks first and re-prompts for a different name instead.</summary>
        private void CreateAndEdit(string promptTitle, string name, IEnumerable<string> initialActionKeys = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                this.ReturnToSelf();
                return;
            }

            if (this.Presets.GetPresetNames().Contains(name))
            {
                Game1.showRedMessage($"A preset named \"{name}\" already exists - pick a different name.");
                Game1.activeClickableMenu = new PresetNamePrompt(
                    promptTitle,
                    name,
                    newName => this.CreateAndEdit(promptTitle, newName, initialActionKeys),
                    this.ReturnToSelf);
                return;
            }

            List<string> included = (initialActionKeys ?? Enumerable.Empty<string>()).ToList();
            this.Presets.SavePreset(name, included);
            Game1.activeClickableMenu = new PresetEditMenu(this.Helper, this.Config, this.Presets, name, included, isNewPreset: true);
        }

        private static IEnumerable<string> GetActionKeys(IEnumerable<ModListing> mods)
        {
            return mods.SelectMany(mod => mod.Actions.Select(action => ActionKey.Of(mod.ModName, action.Name)));
        }

        private void RequestDelete(int index)
        {
            if (index < 0 || index >= this.Rows.Count || this.Rows[index] == NewPresetLabel)
                return;

            this.PendingDeleteIndex = index;
            Game1.playSound("cancel");
        }

        private void ConfirmDelete(int index)
        {
            if (index < 0 || index >= this.Rows.Count)
            {
                this.PendingDeleteIndex = null;
                return;
            }

            string name = this.Rows[index];
            this.Presets.DeletePreset(name);

            if (this.Config.ActivePreset == name)
            {
                this.Config.ActivePreset = "All";
                this.Helper.WriteConfig(this.Config);
            }

            Game1.playSound("bigDeSelect");
            this.ReturnToSelf();
        }

        private void CancelPendingDelete()
        {
            this.PendingDeleteIndex = null;
            Game1.playSound("smallSelect");
        }

        private void ReturnToSelf()
        {
            Game1.activeClickableMenu = new PresetManagerMenu(this.Helper, this.Config, this.Presets);
        }

        private void Close()
        {
            Game1.activeClickableMenu = new QuickMenu(this.Helper, this.Config, this.Presets);
        }

        public override void draw(SpriteBatch b)
        {
            this.UpdateRowBounds();

            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

            SpriteText.drawString(b, "Presets", this.xPositionOnScreen + 32, this.yPositionOnScreen + 24);

            if (this.Rows.Count > VisibleRows)
            {
                string counter = $"{this.ScrollOffset + 1}-{System.Math.Min(this.ScrollOffset + VisibleRows, this.Rows.Count)} of {this.Rows.Count}";
                Vector2 counterSize = Game1.smallFont.MeasureString(counter);
                Utility.drawTextWithShadow(b, counter, Game1.smallFont, new Vector2(this.xPositionOnScreen + this.width - 32 - counterSize.X, this.yPositionOnScreen + 32), Game1.textColor);
            }

            for (int i = this.ScrollOffset; i < System.Math.Min(this.ScrollOffset + VisibleRows, this.Rows.Count); i++)
            {
                ClickableComponent row = this.RowComponents[i];
                bool isPendingDelete = this.PendingDeleteIndex == i;
                bool isSnapped = this.currentlySnappedComponent == row;
                if (isPendingDelete)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Red * 0.5f);
                else if (isSnapped)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.6f);

                string label = TextLayout.FitToWidth(this.Rows[i], this.width - 64 - 96);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(row.bounds.X + 8, row.bounds.Y + 8), Game1.textColor);
            }

            if (this.PendingDeleteIndex is int pendingIndex)
            {
                // The confirm instruction goes first (not the preset name) so truncation - if the
                // name is long enough to need it - trims the name, not the part that matters most.
                string confirmHint = TextLayout.FitToWidth($"A/RT/Enter/click confirms deleting '{this.Rows[pendingIndex]}' - anything else cancels", this.width - 64);
                Utility.drawTextWithShadow(b, confirmHint, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 40), Color.Red);
            }
            else
            {
                string hintLine1 = TextLayout.FitToWidth("A/Enter/click: edit   Y: duplicate   X/LB/RB/LT: delete", this.width - 64);
                string hintLine2 = TextLayout.FitToWidth("(or right-click a row to delete it)   B: back", this.width - 64);
                Utility.drawTextWithShadow(b, hintLine1, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 58), Game1.textColor);
                Utility.drawTextWithShadow(b, hintLine2, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 36), Game1.textColor);
            }

            this.drawMouse(b);
        }
    }
}
