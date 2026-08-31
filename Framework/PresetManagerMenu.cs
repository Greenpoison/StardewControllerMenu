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
    /// Lists every saved preset in the active profile, plus an option to create a new one. This
    /// includes <see cref="PresetManager.RadialPresetName"/>, the reserved preset that feeds the
    /// radial menu - it's edited exactly like any other preset here, it just can't be deleted (see
    /// <see cref="PresetEditMenu"/>).
    /// A: open the preset in <see cref="PresetEditMenu"/> to toggle its actions. Y: duplicate it (name
    /// the copy, then edit it). B: back to the quick menu. Deleting a preset isn't done from here -
    /// see <see cref="PresetEditMenu"/> (RT to unlock deletion, then LT to delete) - this menu used to
    /// have its own separate request/confirm delete flow, but it overlapped with that one and was
    /// removed once the edit menu's flow was confirmed working reliably.
    /// </summary>
    public class PresetManagerMenu : IClickableMenu
    {
        private const string NewPresetLabel = "+ New Preset";

        private readonly IModHelper Helper;
        private readonly ModConfig Config;
        private readonly PresetManager Presets;

        private readonly List<string> Rows = new();
        private readonly List<ClickableComponent> RowComponents = new();

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
            this.Rows.AddRange(this.Presets.GetEditablePresetNames());

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

            for (int i = 0; i < this.RowComponents.Count; i++)
            {
                if (this.RowComponents[i].containsPoint(x, y))
                {
                    this.Select(i);
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

                case Buttons.B:
                    this.Close();
                    return;
            }

            // Deliberately no fallthrough to base.receiveGamePadButton - see QuickMenu's remarks for why.
        }

        public override void receiveKeyPress(Keys key)
        {
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

            if (name == "All" || this.Presets.GetEditablePresetNames().Contains(name))
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
                bool isSnapped = this.currentlySnappedComponent == row;
                if (isSnapped)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.6f);

                string label = TextLayout.FitToWidth(this.Rows[i], this.width - 64 - 96);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(row.bounds.X + 8, row.bounds.Y + 8), Game1.textColor);
            }

            string hint = TextLayout.FitToWidth("A/Enter/click: edit (RT then LT deletes from there)   Y: duplicate   B: back", this.width - 64);
            Utility.drawTextWithShadow(b, hint, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 40), Game1.textColor);

            this.drawMouse(b);
        }
    }
}
