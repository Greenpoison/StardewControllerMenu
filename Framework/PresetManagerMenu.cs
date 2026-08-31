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
    /// A: open the preset in <see cref="PresetEditMenu"/> to toggle its mods. Y: duplicate it (name
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

        /// <summary>Keep the snapped row scrolled into view, then position each row's clickable bounds for however it's currently scrolled - off-screen rows get moved out of click range instead of being drawn. Without this, more than ~5 presets would overflow past the hint bar exactly like the Quick Menu did before it got the same treatment.</summary>
        private void UpdateRowBounds()
        {
            if (this.currentlySnappedComponent != null)
            {
                int snappedIndex = this.currentlySnappedComponent.myID;
                if (snappedIndex < this.ScrollOffset)
                    this.ScrollOffset = snappedIndex;
                else if (snappedIndex >= this.ScrollOffset + VisibleRows)
                    this.ScrollOffset = snappedIndex - VisibleRows + 1;
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

        public override void receiveGamePadButton(Buttons button)
        {
            if (this.PendingDeleteIndex is int pendingIndex)
            {
                if (button == Buttons.A)
                    this.ConfirmDelete(pendingIndex);
                else
                    this.CancelPendingDelete();
                return;
            }

            // D-pad/left-stick navigation is NOT handled here - see QuickMenu's class remarks for why.
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

                case Buttons.X:
                    if (this.currentlySnappedComponent != null)
                        this.RequestDelete(this.currentlySnappedComponent.myID);
                    return;

                case Buttons.B:
                    this.Close();
                    return;
            }

            base.receiveGamePadButton(button);
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

            if (key == Keys.Escape)
            {
                this.Close();
                return;
            }

            if (key == Keys.Delete && this.currentlySnappedComponent != null)
            {
                this.RequestDelete(this.currentlySnappedComponent.myID);
                return;
            }

            base.receiveKeyPress(key);
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
                    name => this.CreateAndEdit(name),
                    this.ReturnToSelf);
                return;
            }

            string presetName = this.Rows[index];
            IEnumerable<string> included = this.Presets.GetActivePresetEntries(presetName).Select(m => m.ModName);
            Game1.activeClickableMenu = new PresetEditMenu(this.Helper, this.Config, this.Presets, presetName, included);
        }

        private void Duplicate(int index)
        {
            if (index < 0 || index >= this.Rows.Count || this.Rows[index] == NewPresetLabel)
                return;

            string sourceName = this.Rows[index];
            List<string> included = this.Presets.GetActivePresetEntries(sourceName).Select(m => m.ModName).ToList();

            Game1.activeClickableMenu = new PresetNamePrompt(
                "Duplicate Preset",
                $"{sourceName} Copy",
                name => this.CreateAndEdit(name, included),
                this.ReturnToSelf);
        }

        private void CreateAndEdit(string name, IEnumerable<string> initialMods = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                this.ReturnToSelf();
                return;
            }

            List<string> included = (initialMods ?? Enumerable.Empty<string>()).ToList();
            this.Presets.SavePreset(name, included);
            Game1.activeClickableMenu = new PresetEditMenu(this.Helper, this.Config, this.Presets, name, included);
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

                string label = TextLayout.FitToWidth(this.Rows[i], this.width - 64 - 16);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(row.bounds.X + 8, row.bounds.Y + 8), Game1.textColor);
            }

            string hint = this.PendingDeleteIndex is int pendingIndex
                ? $"Delete '{this.Rows[pendingIndex]}'? Press A (or click) to confirm - any other input cancels."
                : "A/click: edit mods   Y: duplicate   X/right-click: delete   B: back";
            hint = TextLayout.FitToWidth(hint, this.width - 64);
            Color hintColor = this.PendingDeleteIndex != null ? Color.Red : Game1.textColor;
            Utility.drawTextWithShadow(b, hint, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 40), hintColor);

            this.drawMouse(b);
        }
    }
}
