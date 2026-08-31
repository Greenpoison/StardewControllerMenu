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

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            this.Presets = new PresetManager(helper, this.Monitor);
            KeySender.Init(this.Monitor);

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            this.Presets.LoadProfile(this.Config.ActiveProfile);
        }

        private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            if (this.Config.OpenMenuButton.JustPressed())
            {
                var entries = this.Presets.GetActivePresetEntries(this.Config.ActivePreset);
                Game1.activeClickableMenu = new QuickMenu(entries);
            }
        }
    }
}
