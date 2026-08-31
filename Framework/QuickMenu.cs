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
    /// D-pad/left-stick navigation is wired explicitly here rather than relying on the base class:
    /// IClickableMenu's own receiveGamePadButton and gamePadButtonHeld are both no-ops by default
    /// (confirmed by decompiling the game) - every vanilla menu with snap navigation wires it itself
    /// by calling applyMovementKey, so this does the same.
    /// </summary>
    public class QuickMenu : IClickableMenu
    {
        private readonly IModHelper Helper;
        private readonly ModConfig Config;
        private readonly PresetManager Presets;

        private readonly List<(ModListing Mod, ModAction Action)> Rows = new();
        private readonly List<ClickableComponent> RowComponents = new();

        private int ScrollOffset;
        private float DirectionRepeatCooldownMs;

        private const int RowHeight = 64;
        private const int TitleTop = 24;
        private const int StatusGap = 4;
        private const int ContentGap = 12;
        private const int VisibleRows = 6;
        private const int InitialRepeatDelayMs = 300;
        private const int RepeatIntervalMs = 90;

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

        public override void receiveGamePadButton(Buttons button)
        {
            if (DirectionOf(button) is int direction)
            {
                this.Move(direction);
                this.DirectionRepeatCooldownMs = InitialRepeatDelayMs;
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

            base.receiveGamePadButton(button);
        }

        public override void gamePadButtonHeld(Buttons b)
        {
            if (DirectionOf(b) is int direction)
            {
                this.DirectionRepeatCooldownMs -= (float)Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
                if (this.DirectionRepeatCooldownMs <= 0)
                {
                    this.Move(direction);
                    this.DirectionRepeatCooldownMs = RepeatIntervalMs;
                }
                return;
            }

            base.gamePadButtonHeld(b);
        }

        private void Move(int direction)
        {
            this.applyMovementKey(direction);
            this.UpdateRowBounds();
        }

        public override void receiveKeyPress(Keys key)
        {
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
            }

            // Anything else (including WASD/arrow keys) falls through to the base class, which is
            // what actually drives snap navigation for keyboard and (via the game's own translation
            // layer) some gamepad input - see the class remarks.
            base.receiveKeyPress(key);
            this.UpdateRowBounds();
        }

        private void SelectRow(int index)
        {
            if (index < 0 || index >= this.Rows.Count)
                return;

            (ModListing mod, ModAction action) = this.Rows[index];
            KeySender.Send(action.Keybind);
            Game1.playSound("select");
            Game1.exitActiveMenu();
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

        /// <summary>Shrink a label with an ellipsis if it's wider than the given pixel width, so it never draws outside the menu.</summary>
        private static string FitToWidth(string text, float maxWidth)
        {
            if (Game1.smallFont.MeasureString(text).X <= maxWidth)
                return text;

            const string ellipsis = "...";
            while (text.Length > 0 && Game1.smallFont.MeasureString(text + ellipsis).X > maxWidth)
                text = text[..^1];
            return text + ellipsis;
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

                string label = FitToWidth($"{mod.ModName} - {action.Name} [{action.Keybind}]", maxLabelWidth);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(row.bounds.X + 8, row.bounds.Y + 8), Game1.textColor);
            }

            Utility.drawTextWithShadow(b, "A: trigger   X: manage presets   LB/RB: preset   LT/RT: profile   B/Esc: close", Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 40), Game1.textColor);

            this.drawMouse(b);
        }
    }
}
