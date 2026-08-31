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
    /// <summary>Lists every individual action across every mod in the active profile with a checkbox, for building one named preset. A toggles an action in/out. Y saves back to that preset name and returns to the quick menu with it active. B discards any changes made this visit and returns without saving.</summary>
    public class PresetEditMenu : IClickableMenu
    {
        private readonly IModHelper Helper;
        private readonly ModConfig Config;
        private readonly PresetManager Presets;
        private readonly string PresetName;

        private readonly List<(ModListing Mod, ModAction Action)> AllActions = new();
        private readonly List<ClickableComponent> RowComponents = new();
        private readonly HashSet<string> Selected;

        private int ScrollOffset;

        private const int RowHeight = 64;
        private const int ContentTop = 96;
        private const int VisibleRows = 7;

        public PresetEditMenu(IModHelper helper, ModConfig config, PresetManager presets, string presetName, IEnumerable<string> initiallyIncludedActionKeys)
            : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 300, 800, 600, showUpperRightCloseButton: true)
        {
            this.Helper = helper;
            this.Config = config;
            this.Presets = presets;
            this.PresetName = presetName;

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
            for (int i = 0; i < this.RowComponents.Count; i++)
            {
                if (this.RowComponents[i].containsPoint(x, y))
                {
                    this.ToggleRow(i);
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
                        this.ToggleRow(this.currentlySnappedComponent.myID);
                    return;

                case Buttons.Y:
                    this.Save();
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
                case Keys.P:
                    this.Save();
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

        private void Move(int direction)
        {
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
            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = new QuickMenu(this.Helper, this.Config, this.Presets);
        }

        public override void draw(SpriteBatch b)
        {
            this.UpdateRowBounds();

            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

            const string title = "Edit Preset";
            SpriteText.drawString(b, title, this.xPositionOnScreen + 32, this.yPositionOnScreen + 24);

            string status = TextLayout.FitToWidth($"Editing: {this.PresetName}   ({this.Selected.Count} action(s) selected)", this.width - 64);
            int titleHeight = SpriteText.getHeightOfString(title, 9999);
            int statusY = this.yPositionOnScreen + 24 + titleHeight + 4;
            Utility.drawTextWithShadow(b, status, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, statusY), Game1.textColor);

            if (this.AllActions.Count > VisibleRows)
            {
                string counter = $"{this.ScrollOffset + 1}-{System.Math.Min(this.ScrollOffset + VisibleRows, this.AllActions.Count)} of {this.AllActions.Count}";
                Vector2 counterSize = Game1.smallFont.MeasureString(counter);
                Utility.drawTextWithShadow(b, counter, Game1.smallFont, new Vector2(this.xPositionOnScreen + this.width - 32 - counterSize.X, statusY), Game1.textColor);
            }

            float maxLabelWidth = this.width - 64 - 16;
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

            string hint = TextLayout.FitToWidth("A: toggle action   Y: save   B: cancel (discards changes)", this.width - 64);
            Utility.drawTextWithShadow(b, hint, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 40), Game1.textColor);
            this.drawMouse(b);
        }
    }
}
