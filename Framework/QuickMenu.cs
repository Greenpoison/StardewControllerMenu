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
    /// that action's keybind. Also handles switching profiles/presets and building new presets,
    /// entirely from the controller - LB/RB cycle presets, LT/RT cycle profiles, X toggles preset-edit
    /// mode (A then toggles a row's mod in/out of the preset being built), Y saves it.
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

        private bool EditMode;
        private readonly HashSet<string> EditingModNames = new();

        private int ScrollOffset;
        private float DirectionRepeatCooldownMs;

        private const int RowHeight = 64;
        private const int ContentTop = 96;
        private const int VisibleRows = 7;
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
                    this.ToggleEditMode();
                    return;

                case Buttons.Y:
                    if (this.EditMode)
                        this.PromptSavePreset();
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
                    this.ToggleEditMode();
                    return;

                case Keys.P:
                    if (this.EditMode)
                        this.PromptSavePreset();
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

            if (this.EditMode)
            {
                if (!this.EditingModNames.Remove(mod.ModName))
                    this.EditingModNames.Add(mod.ModName);
                Game1.playSound("drumkit6");
                return;
            }

            KeySender.Send(action.Keybind);
            Game1.playSound("select");
            Game1.exitActiveMenu();
        }

        private void CyclePreset(int direction)
        {
            if (this.EditMode)
                return; // switching preset mid-edit would discard the in-progress selection

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
            if (this.EditMode)
                return;

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

        private void ToggleEditMode()
        {
            this.EditMode = !this.EditMode;

            if (this.EditMode)
            {
                this.EditingModNames.Clear();
                if (this.Config.ActivePreset != "All")
                {
                    foreach (ModListing mod in this.Presets.GetActivePresetEntries(this.Config.ActivePreset))
                        this.EditingModNames.Add(mod.ModName);
                }
            }

            Game1.playSound(this.EditMode ? "bigSelect" : "bigDeSelect");
        }

        private void PromptSavePreset()
        {
            string defaultName = this.Config.ActivePreset == "All" ? "" : this.Config.ActivePreset;
            Game1.activeClickableMenu = new PresetNamePrompt("Save Preset", defaultName, this.OnPresetNamed, this.OnPresetNameCancelled);
        }

        private void OnPresetNamed(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                this.Presets.SavePreset(name, this.EditingModNames);
                this.Config.ActivePreset = name;
                this.Helper.WriteConfig(this.Config);
            }

            this.EditMode = false;
            this.RebuildRows();
            Game1.activeClickableMenu = this;
        }

        private void OnPresetNameCancelled()
        {
            // Stay in edit mode with the in-progress selection intact - just return to the menu.
            Game1.activeClickableMenu = this;
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

            string header = this.EditMode
                ? $"Editing Preset - Profile: {this.Config.ActiveProfile}"
                : $"Quick Menu - Profile: {this.Config.ActiveProfile} - Preset: {this.Config.ActivePreset}";
            SpriteText.drawString(b, header, this.xPositionOnScreen + 32, this.yPositionOnScreen + 24);

            if (this.Rows.Count > VisibleRows)
            {
                string counter = $"{this.ScrollOffset + 1}-{System.Math.Min(this.ScrollOffset + VisibleRows, this.Rows.Count)} of {this.Rows.Count}";
                Vector2 counterSize = Game1.smallFont.MeasureString(counter);
                Utility.drawTextWithShadow(b, counter, Game1.smallFont, new Vector2(this.xPositionOnScreen + this.width - 32 - counterSize.X, this.yPositionOnScreen + 32), Game1.textColor);
            }

            float maxLabelWidth = this.width - 64 - 16;
            for (int i = this.ScrollOffset; i < System.Math.Min(this.ScrollOffset + VisibleRows, this.Rows.Count); i++)
            {
                (ModListing mod, ModAction action) = this.Rows[i];
                ClickableComponent row = this.RowComponents[i];

                bool isSnapped = this.currentlySnappedComponent == row;
                if (isSnapped)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.6f);

                string checkbox = this.EditMode
                    ? (this.EditingModNames.Contains(mod.ModName) ? "[x] " : "[ ] ")
                    : "";
                string label = FitToWidth($"{checkbox}{mod.ModName} - {action.Name} [{action.Keybind}]", maxLabelWidth);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(row.bounds.X + 8, row.bounds.Y + 8), Game1.textColor);
            }

            string hint = this.EditMode
                ? "A: toggle mod in preset   Y: save preset   X: cancel edit"
                : "A: trigger   X: build preset   LB/RB: preset   LT/RT: profile";
            Utility.drawTextWithShadow(b, hint, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 40), Game1.textColor);

            this.drawMouse(b);
        }
    }
}
