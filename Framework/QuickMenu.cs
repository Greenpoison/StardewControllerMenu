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

        private const int RowHeight = 64;

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
                var bounds = new Rectangle(this.xPositionOnScreen + 32, this.yPositionOnScreen + 96 + i * RowHeight, this.width - 64, RowHeight - 8);
                this.RowComponents.Add(new ClickableComponent(bounds, i.ToString())
                {
                    myID = i,
                    upNeighborID = i > 0 ? i - 1 : -1,
                    downNeighborID = i < this.Rows.Count - 1 ? i + 1 : -1
                });
            }

            this.allClickableComponents = new List<ClickableComponent>(this.RowComponents);
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

        public override void receiveGamePadButton(Buttons button)
        {
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

                case Keys.S:
                    if (this.EditMode)
                        this.PromptSavePreset();
                    return;
            }

            base.receiveKeyPress(key);
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
            Game1.activeClickableMenu = new NamingMenu(this.OnPresetNamed, "Save Preset", defaultName);
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

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

            string header = this.EditMode
                ? $"Editing Preset - Profile: {this.Config.ActiveProfile}"
                : $"Quick Menu - Profile: {this.Config.ActiveProfile} - Preset: {this.Config.ActivePreset}";
            SpriteText.drawString(b, header, this.xPositionOnScreen + 32, this.yPositionOnScreen + 24);

            for (int i = 0; i < this.Rows.Count; i++)
            {
                (ModListing mod, ModAction action) = this.Rows[i];
                ClickableComponent row = this.RowComponents[i];

                bool isSnapped = this.currentlySnappedComponent == row;
                if (isSnapped)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.6f);

                string checkbox = this.EditMode
                    ? (this.EditingModNames.Contains(mod.ModName) ? "[x] " : "[ ] ")
                    : "";
                string label = $"{checkbox}{mod.ModName} - {action.Name} [{action.Keybind}]";
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
