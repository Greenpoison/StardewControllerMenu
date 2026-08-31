using StardewModdingAPI;

namespace StardewControllerMenu.Framework.Input
{
    /// <summary>Simulates real keyboard/mouse input so other mods' input-polling code sees a genuine keypress.</summary>
    internal interface IInputInjector
    {
        /// <summary>Whether this injector can simulate the given button.</summary>
        bool CanSend(SButton button);

        /// <summary>Press and release every button together, as a single combo.</summary>
        void Send(SButton[] buttons);
    }
}
