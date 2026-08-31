using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewControllerMenu.Framework
{
    /// <summary>A gamepad-navigable list of every mod action in the active preset. Selecting a row triggers that action's keybind.</summary>
    public class QuickMenu : IClickableMenu
    {
        private readonly List<(ModListing Mod, ModAction Action)> Rows = new();
        private readonly List<ClickableComponent> RowComponents = new();

        private const int RowHeight = 64;

        public QuickMenu(IReadOnlyList<ModListing> entries)
            : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 300, 800, 600, showUpperRightCloseButton: true)
        {
            foreach (ModListing mod in entries)
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
            if (button == Buttons.A && this.currentlySnappedComponent != null)
            {
                this.SelectRow(this.currentlySnappedComponent.myID);
                return;
            }

            base.receiveGamePadButton(button);
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

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);
            drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

            SpriteText.drawString(b, "Mod Quick Menu", this.xPositionOnScreen + 32, this.yPositionOnScreen + 24);

            for (int i = 0; i < this.Rows.Count; i++)
            {
                (ModListing mod, ModAction action) = this.Rows[i];
                ClickableComponent row = this.RowComponents[i];

                bool isSnapped = this.currentlySnappedComponent == row;
                if (isSnapped)
                    b.Draw(Game1.staminaRect, row.bounds, Color.Wheat * 0.6f);

                string label = $"{mod.ModName} - {action.Name} [{action.Keybind}]";
                Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(row.bounds.X + 8, row.bounds.Y + 8), Game1.textColor);
            }

            this.drawMouse(b);
        }
    }
}
