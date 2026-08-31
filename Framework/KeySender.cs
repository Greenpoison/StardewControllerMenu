namespace StardewControllerMenu.Framework
{
    /// <summary>
    /// Responsible for actually triggering a mod's keybind when the player selects it in the menu.
    /// Not implemented yet: injecting synthetic input that other SMAPI mods will recognize as a real
    /// keypress requires OS-level input simulation (e.g. Windows SendInput), and needs a separate
    /// code path under Proton on Steam Deck. Tracked as the first task for v0.2.
    /// </summary>
    public static class KeySender
    {
        public static void Send(string keybindString)
        {
            // TODO: implement real input injection.
        }
    }
}
