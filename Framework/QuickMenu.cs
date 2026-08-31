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
    /// A gamepad-navigable list of every mod action in the active preset. Selecting a row triggers
    /// that action's keybind. X opens the preset manager (create/edit/duplicate/delete presets,
    /// via <see cref="PresetManagerMenu"/>); LB/RB cycle presets and LT/RT cycle profiles without
    /// leaving this screen.
    ///
    /// D-pad/left-stick and arrow-key navigation is handled directly here (receiveGamePadButton /
    /// receiveKeyPress), NOT via the base class's own fallthrough. Both this class and the game
    /// independently react to the same physical D-pad press - the game translates it into a
    /// synthetic WASD keypress for its own "snappy menu" handling whenever
    /// Game1.options.snappyMenus &amp;&amp; gamepadControls are both true - so falling through to
    /// base as well as handling the raw button here double-moved the cursor on every press. Neither
    /// receiveGamePadButton nor receiveKeyPress call their base implementation for that reason; see
    /// the comment in receiveKeyPress for the full explanation.
    /// </summary>
    public class QuickMenu : IClickableMenu
    {
        private readonly IModHelper Helper;
        private readonly ModConfig Config;
        private readonly PresetManager Presets;

        private readonly List<(ModListing Mod, ModAction Action)> Rows = new();
        private readonly List<ClickableComponent> RowComponents = new();

        private int ScrollOffset;

        private const int RowHeight = 64;
        private const int TitleTop = 24;
        private const int StatusGap = 4;
        private const int ContentGap = 12;
        private const int VisibleRows = 6;

        public QuickMenu(IModHelper helper, ModConfig config, PresetManager presets)
            : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 300, 800, 600, showUpperRightCloseButton: true)
        {
            this.Helper = helper;
            this.Config = config;
            this.Presets = presets;

            this.RebuildRows();
        }

        private void RebuildRows()
        {
            this.Rows.Clear();
            foreach (ModListing mod in this.Presets.GetActivePresetEntries(this.Config.ActivePreset))
            {
                foreach (ModAction action in mod.Actions)
                    this.Rows.Add((mod, action));
            }

            this.ScrollOffset = 0;
            this.BuildRowComponents();
            if (this.RowComponents.Count > 0)
                this.currentlySnappedComponent = this.RowComponents[0];
            this.snapCursorToCurrentSnappedComponent();
        }

        private void BuildRowComponents()
        {
            this.RowComponents.Clear();
            for (int i = 0; i < this.Rows.Count; i++)
            {
                this.RowComponents.Add(new ClickableComponent(new Rectangle(), i.ToString())
                {
                    myID = i,
                    upNeighborID = i > 0 ? i - 1 : -1,
                    downNeighborID = i < this.Rows.Count - 1 ? i + 1 : -1
                });
            }

            this.allClickableComponents = new List<ClickableComponent>(this.RowComponents);
            this.UpdateRowBounds();
        }

        /// <summary>The title (large font) and status line (small font: profile/preset) for the current mode. Kept short and on two lines deliberately - a single line long enough to include both a variable-length profile name and preset name in the big font routinely overflowed the menu.</summary>
        private (string Title, string Status) GetHeaderText()
        {
            return ("Quick Menu", $"Profile: {this.Config.ActiveProfile}   Preset: {this.Config.ActivePreset}");
        }

        private int GetContentTop()
        {
            (string title, string status) = this.GetHeaderText();
            int titleHeight = SpriteText.getHeightOfString(title, 9999);
            int statusHeight = (int)Game1.smallFont.MeasureString(status).Y;
            return TitleTop + titleHeight + StatusGap + statusHeight + ContentGap;
        }

        /// <summary>Keep the snapped row scrolled into view, then position each row's clickable bounds for however it's currently scrolled - off-screen rows get moved out of click range instead of being drawn.</summary>
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

            int contentTop = this.GetContentTop();
            for (int i = 0; i < this.RowComponents.Count; i++)
            {
                int slot = i - this.ScrollOffset;
                this.RowComponents[i].bounds = (slot >= 0 && slot < VisibleRows)
                    ? new Rectangle(this.xPositionOnScreen + 32, this.yPositionOnScreen + contentTop + slot * RowHeight, this.width - 64, RowHeight - 8)
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
                    this.SelectRow(i);
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
                        this.SelectRow(this.currentlySnappedComponent.myID);
                    return;

                case Buttons.X:
                    this.OpenPresetManager();
                    return;

                case Buttons.B:
                    this.exitThisMenu();
                    return;

                case Buttons.RightShoulder:
                    this.CyclePreset(1);
                    return;

                case Buttons.LeftShoulder:
                    this.CyclePreset(-1);
                    return;

                case Buttons.RightTrigger:
                    this.CycleProfile(1);
                    return;

                case Buttons.LeftTrigger:
                    this.CycleProfile(-1);
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
                case Keys.OemCloseBrackets:
                    this.CyclePreset(1);
                    return;

                case Keys.OemOpenBrackets:
                    this.CyclePreset(-1);
                    return;

                case Keys.PageDown:
                    this.CycleProfile(1);
                    return;

                case Keys.PageUp:
                    this.CycleProfile(-1);
                    return;

                case Keys.E:
                    this.OpenPresetManager();
                    return;

                case Keys.Escape:
                    this.exitThisMenu();
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

        private void SelectRow(int index)
        {
            if (index < 0 || index >= this.Rows.Count)
                return;

            (ModListing mod, ModAction action) = this.Rows[index];
            bool sent = KeySender.Send(action.Keybind);
            Game1.playSound(sent ? "select" : "cancel");
            Game1.exitActiveMenu();
            if (!sent)
                Game1.showRedMessage($"Couldn't trigger \"{action.Name}\" - see the SMAPI console for why.");
        }

        private void OpenPresetManager()
        {
            Game1.activeClickableMenu = new PresetManagerMenu(this.Helper, this.Config, this.Presets);
        }

        private void CyclePreset(int direction)
        {
            List<string> names = this.Presets.GetPresetNames().ToList();
            if (names.Count == 0)
                return;

            int index = names.IndexOf(this.Config.ActivePreset);
            index = ((index < 0 ? 0 : index) + direction + names.Count) % names.Count;

            this.Config.ActivePreset = names[index];
            this.Helper.WriteConfig(this.Config);
            this.RebuildRows();
            Game1.playSound("smallSelect");
        }

        private void CycleProfile(int direction)
        {
            List<string> names = this.Presets.GetProfileNames().ToList();
            if (names.Count == 0)
                return;

            int index = names.IndexOf(this.Config.ActiveProfile);
            index = ((index < 0 ? 0 : index) + direction + names.Count) % names.Count;

            this.Config.ActiveProfile = names[index];
            this.Config.ActivePreset = "All";
            this.Helper.WriteConfig(this.Config);
            this.Presets.LoadProfile(this.Config.ActiveProfile);
            this.RebuildRows();
            Game1.playSound("smallSelect");
        }

        public override void draw(SpriteBatch b)
        {
            this.UpdateRowBounds();

            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

            (string title, string status) = this.GetHeaderText();
            SpriteText.drawString(b, title, this.xPositionOnScreen + 32, this.yPositionOnScreen + TitleTop);

            int titleHeight = SpriteText.getHeightOfString(title, 9999);
            int statusY = this.yPositionOnScreen + TitleTop + titleHeight + StatusGap;
            status = TextLayout.FitToWidth(status, this.width - 64);
            Utility.drawTextWithShadow(b, status, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, statusY), Game1.textColor);

            if (this.Rows.Count > VisibleRows)
            {
                string counter = $"{this.ScrollOffset + 1}-{System.Math.Min(this.ScrollOffset + VisibleRows, this.Rows.Count)} of {this.Rows.Count}";
                Vector2 counterSize = Game1.smallFont.MeasureString(counter);
                Utility.drawTextWithShadow(b, counter, Game1.smallFont, new Vector2(this.xPositionOnScreen + this.width - 32 - counterSize.X, statusY), Game1.textColor);
            }

            float maxLabelWidth = this.width - 64 - 16;
            for (int i = this.ScrollOffset; i < System.Math.Min(this.ScrollOffset + VisibleRows, this.Rows.Count); i++)
            {
                (ModListing mod, ModAction action) = this.Rows[i];
                ClickableComponent row = this.RowComponents[i];

                bool isSnapped = this.currentlySnappedComponent == row;
                if (isSnapped)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.6f);

                string label = TextLayout.FitToWidth($"{mod.ModName} - {action.Name} [{action.Keybind}]", maxLabelWidth);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(row.bounds.X + 8, row.bounds.Y + 8), Game1.textColor);
            }

            // Two short lines rather than one long one - a single line listing all five controls
            // routinely overflowed the box's width once drawn.
            float maxHintWidth = this.width - 64;
            string hintLine1 = TextLayout.FitToWidth("A: trigger   X: manage presets   B/Esc: close", maxHintWidth);
            string hintLine2 = TextLayout.FitToWidth("LB/RB: preset   LT/RT: profile", maxHintWidth);
            Utility.drawTextWithShadow(b, hintLine1, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 58), Game1.textColor);
            Utility.drawTextWithShadow(b, hintLine2, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 36), Game1.textColor);

            this.drawMouse(b);
        }
    }
}
