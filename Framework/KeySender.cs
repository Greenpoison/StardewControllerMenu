using System;
using System.Linq;
using StardewControllerMenu.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace StardewControllerMenu.Framework
{
    /// <summary>
    /// Triggers another mod's keybind by simulating the real input it's listening for. A keybind
    /// string can list several alternatives (e.g. "F, ControllerBack"); when one alternative needs
    /// only keyboard/mouse buttons and another needs a gamepad button, this picks the injectable one,
    /// since either one presses the actual bound action.
    /// </summary>
    public static class KeySender
    {
        private static IMonitor Monitor;
        private static IInputInjector Injector;
        private static bool TriedToBuildInjector;

        public static void Init(IMonitor monitor)
        {
            Monitor = monitor;
        }

        public static void Send(string keybindString)
        {
            if (!KeybindList.TryParse(keybindString, out KeybindList list, out string[] errors) || list == null)
            {
                Monitor?.Log($"Can't trigger '{keybindString}': failed to parse ({string.Join("; ", errors)}).", LogLevel.Warn);
                return;
            }

            IInputInjector injector = GetInjector();
            if (injector == null)
            {
                Monitor?.Log($"Can't trigger '{keybindString}': no working input-injection backend on this platform.", LogLevel.Warn);
                return;
            }

            Keybind chosen = list.Keybinds.FirstOrDefault(keybind => keybind.Buttons.All(injector.CanSend));
            if (chosen == null)
            {
                Monitor?.Log($"Can't trigger '{keybindString}': every alternative needs a button this platform can't simulate (e.g. a gamepad button).", LogLevel.Warn);
                return;
            }

            injector.Send(chosen.Buttons);
        }

        private static IInputInjector GetInjector()
        {
            if (TriedToBuildInjector)
                return Injector;

            TriedToBuildInjector = true;
            try
            {
                if (OperatingSystem.IsWindows())
                    Injector = new WindowsInputInjector();
                else if (OperatingSystem.IsLinux())
                    Injector = new X11InputInjector();
                else
                    Monitor?.Log($"No input injector implemented for this OS yet.", LogLevel.Warn);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to initialize input injector: {ex.Message}", LogLevel.Warn);
            }

            return Injector;
        }
    }
}
