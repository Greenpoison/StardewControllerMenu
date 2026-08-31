using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewControllerMenu.Framework
{
    /// <summary>
    /// EXPERIMENTAL. A radial ("pie") menu over a preset's entries: hold a button to open it,
    /// tilt the stick (or move the mouse) toward an entry, release to trigger it. See the "Radial
    /// menu (experimental)" section in the README for the prior art this design borrows from and
    /// why the default hold-button is a digital button rather than a trigger.
    /// Draws entries as labels arranged around a circle rather than true colored pie wedges - a
    /// simpler SpriteBatch-only approach that avoids needing custom shader/primitive rendering.
    /// </summary>
    public class RadialMenu : IClickableMenu
    {
        private readonly List<(ModListing Mod, ModAction Action)> Items = new();

        private const int Radius = 160;

        /// <summary>Below this squared distance (in the same units passed to <see cref="UpdateDirection"/>), no wedge is highlighted - lets the player release near center to cancel.</summary>
        private const float DeadZoneSquared = 24f * 24f;

        public int HighlightedIndex { get; private set; } = -1;

        public RadialMenu(IReadOnlyList<ModListing> entries)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height)
        {
            foreach (ModListing mod in entries)
            {
                foreach (ModAction action in mod.Actions)
                    this.Items.Add((mod, action));
            }
        }

        private static Vector2 Center => new(Game1.uiViewport.Width / 2f, Game1.uiViewport.Height / 2f);

        /// <summary>
        /// Recompute which wedge is highlighted from a direction vector in screen-space (x+ = right,
        /// y+ = down - the same convention as mouse coordinates). Pass a vector shorter than the dead
        /// zone (e.g. Vector2.Zero) to clear the highlight.
        /// </summary>
        public void UpdateDirection(Vector2 direction)
        {
            if (this.Items.Count == 0 || direction.LengthSquared() < DeadZoneSquared)
            {
                this.HighlightedIndex = -1;
                return;
            }

            // Screen-space "up" (negative y) is angle 0, increasing clockwise - matches the layout in Draw().
            float angle = MathF.Atan2(direction.X, -direction.Y);
            if (angle < 0)
                angle += MathHelper.TwoPi;

            float wedgeAngle = MathHelper.TwoPi / this.Items.Count;
            this.HighlightedIndex = (int)MathF.Round(angle / wedgeAngle) % this.Items.Count;
        }

        /// <summary>Trigger the highlighted entry's keybind, if any is highlighted. Call on button release. Returns whether anything was triggered.</summary>
        public bool ActivateHighlighted()
        {
            if (this.HighlightedIndex < 0 || this.HighlightedIndex >= this.Items.Count)
                return false;

            (ModListing mod, ModAction action) = this.Items[this.HighlightedIndex];
            KeySender.Send(action.Keybind);
            Game1.playSound("select");
            return true;
        }

        public override void draw(SpriteBatch b)
        {
            Vector2 center = Center;
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.35f);

            for (int i = 0; i < this.Items.Count; i++)
            {
                float angle = i * MathHelper.TwoPi / this.Items.Count;
                Vector2 direction = new(MathF.Sin(angle), -MathF.Cos(angle));
                Vector2 position = center + direction * Radius;

                (ModListing mod, ModAction action) = this.Items[i];
                string label = $"{mod.ModName}\n{action.Name}";
                Vector2 size = Game1.smallFont.MeasureString(label);
                Vector2 topLeft = position - size / 2f;

                if (i == this.HighlightedIndex)
                    b.Draw(Game1.staminaRect, new Rectangle((int)topLeft.X - 6, (int)topLeft.Y - 4, (int)size.X + 12, (int)size.Y + 8), Color.Wheat * 0.7f);

                Utility.drawTextWithShadow(b, label, Game1.smallFont, topLeft, Game1.textColor);
            }

            // Center anchor dot, purely visual.
            b.Draw(Game1.staminaRect, new Rectangle((int)center.X - 3, (int)center.Y - 3, 6, 6), Color.White);
        }
    }
}
