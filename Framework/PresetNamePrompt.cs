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
        private readonly ClickableComponent TextBoxComponent;
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

            // Represents the textbox in the snap-navigation graph, so a player who navigates down to
            // Save/Cancel (which deselects it - see receiveGamePadButton) can navigate back up to it
            // and resume editing, instead of being stuck only toggling between the two buttons.
            this.TextBoxComponent = new ClickableComponent(new Rectangle(this.TextBox.X, this.TextBox.Y, this.TextBox.Width, this.TextBox.Height), "Name")
            {
                myID = 0,
                downNeighborID = 1,
                rightNeighborID = 1 // same target as Down - Save sits directly below/right of the name field, and either direction should reach it
            };
            this.SaveButton = new ClickableComponent(new Rectangle(this.TextBox.X, this.TextBox.Y + 64, 150, 56), "Save")
            {
                myID = 1,
                upNeighborID = 0,
                rightNeighborID = 2
            };
            this.CancelButton = new ClickableComponent(new Rectangle(this.TextBox.X + 180, this.TextBox.Y + 64, 150, 56), "Cancel")
            {
                myID = 2,
                upNeighborID = 0,
                leftNeighborID = 1
            };
            this.allClickableComponents = new List<ClickableComponent> { this.TextBoxComponent, this.SaveButton, this.CancelButton };
            this.currentlySnappedComponent = this.TextBoxComponent;
            this.snapCursorToCurrentSnappedComponent();
        }

        private void Submit()
        {
            this.TextBox.Selected = false;
            Game1.keyboardDispatcher.Subscriber = null;
            this.OnSubmit(this.TextBox.Text);
        }

        private void Cancel()
        {
            this.TextBox.Selected = false;
            Game1.keyboardDispatcher.Subscriber = null;
            this.OnCancel();
        }

        /// <summary>Keep TextBox.Selected in sync with whether the snap cursor is actually on it, so navigating back onto it resumes editing and navigating off it (see receiveGamePadButton) doesn't leave it silently still capturing keystrokes.</summary>
        private void SyncTextBoxSelection()
        {
            this.TextBox.Selected = this.currentlySnappedComponent == this.TextBoxComponent;
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

        /// <summary>A direction press either hands focus from the textbox to Save/Cancel navigation, or (once it's already off the textbox) actually moves the snap cursor - matching what the game's own NamingMenu does for the identical reason. Typing itself doesn't go through receiveKeyPress at all (see Game1.keyboardDispatcher.Subscriber in the constructor), so there's no fallthrough to a base implementation needed here or in receiveGamePadButton below - see QuickMenu's remarks for why that fallthrough is actively harmful for gamepad input anyway.</summary>
        private void HandleDirection(int direction)
        {
            if (this.TextBox.Selected)
            {
                this.TextBox.Selected = false;
                return;
            }

            this.applyMovementKey(direction);
            this.SyncTextBoxSelection();
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                this.Cancel();
                return;
            }

            if (DirectionOf(key) is int direction)
                this.HandleDirection(direction);
        }

        public override void receiveGamePadButton(Buttons button)
        {
            if (button == Buttons.B)
            {
                this.Cancel();
                return;
            }

            if (DirectionOf(button) is int direction)
            {
                this.HandleDirection(direction);
                return;
            }

            if (button == Buttons.A && !this.TextBox.Selected)
            {
                if (this.currentlySnappedComponent == this.SaveButton)
                    this.Submit();
                else if (this.currentlySnappedComponent == this.CancelButton)
                    this.Cancel();
            }
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

            Utility.drawTextWithShadow(b, "Enter: save   Esc/B: cancel   D-pad: move between name/Save/Cancel", Game1.smallFont, new Vector2(this.TextBox.X - 40, this.TextBox.Y + 140), Game1.textColor);

            this.drawMouse(b);
        }
    }
}
