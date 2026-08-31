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
    /// <summary>Lists every mod in the active profile with a checkbox, for building one named preset. A toggles a mod in/out. Y saves back to that preset name and returns to the quick menu with it active. B discards any changes made this visit and returns without saving.</summary>
    public class PresetEditMenu : IClickableMenu
    {
        private readonly IModHelper Helper;
        private readonly ModConfig Config;
        private readonly PresetManager Presets;
        private readonly string PresetName;

        private readonly List<ModListing> AllMods;
        private readonly List<ClickableComponent> RowComponents = new();
        private readonly HashSet<string> Selected;

        private int ScrollOffset;

        private const int RowHeight = 64;
        private const int ContentTop = 96;
        private const int VisibleRows = 7;

        public PresetEditMenu(IModHelper helper, ModConfig config, PresetManager presets, string presetName, IEnumerable<string> initiallyIncludedModNames)
            : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 300, 800, 600, showUpperRightCloseButton: true)
        {
            this.Helper = helper;
            this.Config = config;
            this.Presets = presets;
            this.PresetName = presetName;
            this.AllMods = this.Presets.GetAllEntries().ToList();
            this.Selected = new HashSet<string>(initiallyIncludedModNames);

            this.BuildRowComponents();
            if (this.RowComponents.Count > 0)
                this.currentlySnappedComponent = this.RowComponents[0];
            this.snapCursorToCurrentSnappedComponent();
        }

        private void BuildRowComponents()
        {
            this.RowComponents.Clear();
            for (int i = 0; i < this.AllMods.Count; i++)
            {
                this.RowComponents.Add(new ClickableComponent(new Rectangle(), i.ToString())
                {
                    myID = i,
                    upNeighborID = i > 0 ? i - 1 : -1,
                    downNeighborID = i < this.AllMods.Count - 1 ? i + 1 : -1
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
            this.ScrollOffset = System.Math.Clamp(this.ScrollOffset, 0, System.Math.Max(0, this.AllMods.Count - VisibleRows));

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

        public override void receiveGamePadButton(Buttons button)
        {
            // D-pad/left-stick navigation is NOT handled here - see QuickMenu's class remarks for why.
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

            base.receiveGamePadButton(button);
        }

        public override void receiveKeyPress(Keys key)
        {
            switch (key)
            {
                case Keys.P:
                    this.Save();
                    return;

                case Keys.Escape:
                    this.Cancel();
                    return;
            }

            base.receiveKeyPress(key);
            this.UpdateRowBounds();
        }

        private void ToggleRow(int index)
        {
            if (index < 0 || index >= this.AllMods.Count)
                return;

            string modName = this.AllMods[index].ModName;
            if (!this.Selected.Remove(modName))
                this.Selected.Add(modName);
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

            const string title = "Edit Mods";
            SpriteText.drawString(b, title, this.xPositionOnScreen + 32, this.yPositionOnScreen + 24);

            string status = TextLayout.FitToWidth($"Editing: {this.PresetName}   ({this.Selected.Count} selected)", this.width - 64);
            int titleHeight = SpriteText.getHeightOfString(title, 9999);
            int statusY = this.yPositionOnScreen + 24 + titleHeight + 4;
            Utility.drawTextWithShadow(b, status, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, statusY), Game1.textColor);

            if (this.AllMods.Count > VisibleRows)
            {
                string counter = $"{this.ScrollOffset + 1}-{System.Math.Min(this.ScrollOffset + VisibleRows, this.AllMods.Count)} of {this.AllMods.Count}";
                Vector2 counterSize = Game1.smallFont.MeasureString(counter);
                Utility.drawTextWithShadow(b, counter, Game1.smallFont, new Vector2(this.xPositionOnScreen + this.width - 32 - counterSize.X, statusY), Game1.textColor);
            }

            float maxLabelWidth = this.width - 64 - 16;
            for (int i = this.ScrollOffset; i < System.Math.Min(this.ScrollOffset + VisibleRows, this.AllMods.Count); i++)
            {
                ModListing mod = this.AllMods[i];
                ClickableComponent row = this.RowComponents[i];

                bool isSnapped = this.currentlySnappedComponent == row;
                if (isSnapped)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.6f);

                string checkbox = this.Selected.Contains(mod.ModName) ? "[x] " : "[ ] ";
                string label = TextLayout.FitToWidth($"{checkbox}{mod.ModName} ({mod.Actions.Count} actions)", maxLabelWidth);
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(row.bounds.X + 8, row.bounds.Y + 8), Game1.textColor);
            }

            string hint = TextLayout.FitToWidth("A: toggle mod   Y: save   B: cancel (discards changes)", this.width - 64);
            Utility.drawTextWithShadow(b, hint, Game1.smallFont, new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + this.height - 40), Game1.textColor);
            this.drawMouse(b);
        }
    }
}
