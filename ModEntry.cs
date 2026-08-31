using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewControllerMenu.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewControllerMenu
{
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private PresetManager Presets;
        private RadialMenu ActiveRadialMenu;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            this.Presets = new PresetManager(helper, this.Monitor);
            KeySender.Init(this.Monitor);

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            this.Presets.LoadProfile(this.Config.ActiveProfile);

            // A hand-edited or stale config.json can pair a profile with a preset name that doesn't
            // exist in it (e.g. after switching profiles outside the menu, or deleting a preset from
            // a different profile than the one that was active). GetActivePresetEntries already falls
            // back to "All" silently in that case, but the header would otherwise keep showing the
            // nonexistent preset's name - fix the config itself instead of just working around it.
            if (this.Config.ActivePreset != "All" && !this.Presets.GetPresetNames().Contains(this.Config.ActivePreset))
            {
                this.Monitor.Log($"Active preset '{this.Config.ActivePreset}' doesn't exist in profile '{this.Config.ActiveProfile}' - falling back to 'All'.", LogLevel.Warn);
                this.Config.ActivePreset = "All";
                this.Helper.WriteConfig(this.Config);
            }
        }

        private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            if (this.Config.OpenMenuButton.JustPressed())
            {
                // Prevents another mod bound to the same combo (see the example-setup README for a
                // real one) from also reacting this tick. Doesn't help against a hardcoded vanilla
                // behavior on the same button - that's the actual reason OpenMenuButton avoids stick
                // clicks by default, since SuppressActiveKeybinds can't reach those.
                this.Helper.Input.SuppressActiveKeybinds(this.Config.OpenMenuButton);
                Game1.activeClickableMenu = new QuickMenu(this.Helper, this.Config, this.Presets);
            }
        }

        /// <summary>Drives the experimental radial menu: it needs per-tick polling (not just button-change events) to tell "held" from "just pressed" and to keep re-reading stick/mouse direction while it's open.</summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            SButtonState state = this.Config.RadialMenuButton.GetState();

            if (this.ActiveRadialMenu == null)
            {
                if (state == SButtonState.Pressed && Game1.activeClickableMenu == null)
                {
                    // Deliberately not this.Config.ActivePreset - the radial menu always reads from
                    // its own reserved preset (see PresetManager.RadialPresetName), so switching
                    // presets in the Quick Menu never changes what's on the radial menu.
                    var entries = this.Presets.GetActivePresetEntries(PresetManager.RadialPresetName);
                    this.ActiveRadialMenu = new RadialMenu(entries);
                    Game1.activeClickableMenu = this.ActiveRadialMenu;
                }
                return;
            }

            if (Game1.activeClickableMenu != this.ActiveRadialMenu)
            {
                // something else took over the menu slot (e.g. player paused) - drop our reference without touching activeClickableMenu
                this.ActiveRadialMenu = null;
                return;
            }

            if (state == SButtonState.Held || state == SButtonState.Pressed)
            {
                this.ActiveRadialMenu.UpdateDirection(this.GetRadialDirection());
            }
            else
            {
                this.ActiveRadialMenu.ActivateHighlighted();
                Game1.exitActiveMenu();
                this.ActiveRadialMenu = null;
            }
        }

        /// <summary>Right stick tilt if present, else left stick, else mouse position relative to screen center. All converted to screen-space (y+ = down) for <see cref="RadialMenu.UpdateDirection"/>.</summary>
        private Vector2 GetRadialDirection()
        {
            GamePadThumbSticks sticks = Game1.input.GetGamePadState().ThumbSticks;
            Vector2 stick = sticks.Right.LengthSquared() > 0.1f ? sticks.Right : sticks.Left;
            if (stick.LengthSquared() > 0.1f)
                return new Vector2(stick.X, -stick.Y) * 200f; // stick space is y+ = up; flip to screen space and scale past the dead zone

            Vector2 mouse = new(Game1.getMouseX(), Game1.getMouseY());
            Vector2 center = new(Game1.uiViewport.Width / 2f, Game1.uiViewport.Height / 2f);
            return mouse - center;
        }
    }
}
