using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewControllerMenu.Framework
{
    /// <summary>
    /// A minimal save/cancel text prompt. The game's own <see cref="NamingMenu"/> has no cancel path
    /// at all (by design - it's meant for mandatory naming, like naming a pet) which traps the player
    /// mid-edit if they open it by mistake, so this reimplements just enough of it to add Cancel.
    /// </summary>
    public class PresetNamePrompt : IClickableMenu
    {
        public delegate void SubmitBehavior(string name);

        private readonly string Title;
        private readonly SubmitBehavior OnSubmit;
        private readonly Action OnCancel;
        private readonly TextBox TextBox;
        private readonly ClickableComponent SaveButton;
        private readonly ClickableComponent CancelButton;

        public PresetNamePrompt(string title, string defaultName, SubmitBehavior onSubmit, Action onCancel)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
        {
            this.Title = title;
            this.OnSubmit = onSubmit;
            this.OnCancel = onCancel;

            this.TextBox = new TextBox(null, null, Game1.dialogueFont, Game1.textColor)
            {
                X = Game1.uiViewport.Width / 2 - 192,
                Y = Game1.uiViewport.Height / 2 - 24,
                Width = 384,
                Height = 48,
                Text = defaultName ?? "",
                Selected = true
            };
            this.TextBox.OnEnterPressed += _ => this.Submit();
            Game1.keyboardDispatcher.Subscriber = this.TextBox;

            this.SaveButton = new ClickableComponent(new Rectangle(this.TextBox.X, this.TextBox.Y + 64, 150, 56), "Save")
            {
                myID = 1,
                rightNeighborID = 2
            };
            this.CancelButton = new ClickableComponent(new Rectangle(this.TextBox.X + 180, this.TextBox.Y + 64, 150, 56), "Cancel")
            {
                myID = 2,
                leftNeighborID = 1
            };
            this.allClickableComponents = new List<ClickableComponent> { this.SaveButton, this.CancelButton };
            this.currentlySnappedComponent = this.SaveButton;
            this.snapCursorToCurrentSnappedComponent();
        }

        private void Submit()
        {
            this.TextBox.Selected = false;
            this.OnSubmit(this.TextBox.Text);
        }

        private void Cancel()
        {
            this.TextBox.Selected = false;
            this.OnCancel();
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                this.Cancel();
                return;
            }

            if (!this.TextBox.Selected)
                base.receiveKeyPress(key);
        }

        public override void receiveGamePadButton(Buttons button)
        {
            if (button == Buttons.B)
            {
                this.Cancel();
                return;
            }

            // The textbox has focus for as long as this prompt is open, which otherwise blocks the
            // game's own D-pad-to-navigation translation entirely (see receiveKeyPress) - so a
            // direction press has to explicitly hand focus back first, exactly like the game's own
            // NamingMenu does for the same reason.
            if (this.TextBox.Selected)
            {
                switch (button)
                {
                    case Buttons.DPadUp:
                    case Buttons.DPadDown:
                    case Buttons.DPadLeft:
                    case Buttons.DPadRight:
                    case Buttons.LeftThumbstickUp:
                    case Buttons.LeftThumbstickDown:
                    case Buttons.LeftThumbstickLeft:
                    case Buttons.LeftThumbstickRight:
                        this.TextBox.Selected = false;
                        return;
                }
            }

            if (button == Buttons.A && !this.TextBox.Selected)
            {
                if (this.currentlySnappedComponent == this.SaveButton)
                    this.Submit();
                else if (this.currentlySnappedComponent == this.CancelButton)
                    this.Cancel();
                return;
            }

            base.receiveGamePadButton(button);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);
            this.TextBox.Update();

            if (this.SaveButton.containsPoint(x, y))
                this.Submit();
            else if (this.CancelButton.containsPoint(x, y))
                this.Cancel();
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            SpriteText.drawString(b, this.Title, Game1.uiViewport.Width / 2 - 150, Game1.uiViewport.Height / 2 - 128);

            this.TextBox.Draw(b);

            bool saveSnapped = this.currentlySnappedComponent == this.SaveButton;
            bool cancelSnapped = this.currentlySnappedComponent == this.CancelButton;
            if (saveSnapped)
                b.Draw(Game1.staminaRect, this.SaveButton.bounds, Color.Wheat * 0.7f);
            if (cancelSnapped)
                b.Draw(Game1.staminaRect, this.CancelButton.bounds, Color.Wheat * 0.7f);

            Utility.drawTextWithShadow(b, "Save", Game1.smallFont, new Vector2(this.SaveButton.bounds.X + 16, this.SaveButton.bounds.Y + 16), Game1.textColor);
            Utility.drawTextWithShadow(b, "Cancel", Game1.smallFont, new Vector2(this.CancelButton.bounds.X + 8, this.CancelButton.bounds.Y + 16), Game1.textColor);

            Utility.drawTextWithShadow(b, "Enter: save   Esc/B: cancel", Game1.smallFont, new Vector2(this.TextBox.X, this.TextBox.Y + 140), Game1.textColor);

            this.drawMouse(b);
        }
    }
}
