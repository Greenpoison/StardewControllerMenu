using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using StardewModdingAPI;

namespace StardewControllerMenu.Framework.Input
{
    /// <summary>
    /// Simulates keyboard/mouse input on native Linux (e.g. Steam Deck's SteamOS running the Linux
    /// build of the game) via the X11 XTest extension. This works when the game's window is on an
    /// X11 or XWayland display, which is the normal case for gamescope on Steam Deck.
    /// There is no portable Linux equivalent of XTest for gamepad buttons, so this injector can only
    /// send keyboard/mouse presses - a keybind whose every alternative requires a controller button
    /// can't be triggered this way (see <see cref="Framework.KeySender"/>, which picks a keyboard/mouse
    /// alternative when one is available).
    /// </summary>
    internal class X11InputInjector : IInputInjector, IDisposable
    {
        [DllImport("libX11.so.6")]
        private static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern void XFlush(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XStringToKeysym(string str);

        [DllImport("libX11.so.6")]
        private static extern byte XKeysymToKeycode(IntPtr display, IntPtr keysym);

        [DllImport("libXtst.so.6")]
        private static extern void XTestFakeKeyEvent(IntPtr display, uint keycode, bool isPress, ulong delay);

        [DllImport("libXtst.so.6")]
        private static extern void XTestFakeButtonEvent(IntPtr display, uint button, bool isPress, ulong delay);

        private readonly IntPtr Display;

        private static readonly Dictionary<SButton, string> KeysymNames = BuildKeysymNames();

        // X11 pointer button numbers: 1 = left, 2 = middle, 3 = right, 8/9 = side buttons.
        private static readonly Dictionary<SButton, uint> MouseButtons = new()
        {
            [SButton.MouseLeft] = 1,
            [SButton.MouseMiddle] = 2,
            [SButton.MouseRight] = 3,
            [SButton.MouseX1] = 8,
            [SButton.MouseX2] = 9,
        };

        public X11InputInjector()
        {
            this.Display = XOpenDisplay(IntPtr.Zero);
            if (this.Display == IntPtr.Zero)
                throw new InvalidOperationException("XOpenDisplay returned null - no X11 display available (e.g. a pure-Wayland session with no XWayland).");
        }

        public bool CanSend(SButton button) => KeysymNames.ContainsKey(button) || MouseButtons.ContainsKey(button);

        public void Send(SButton[] buttons)
        {
            var keycodes = new List<uint>();
            var mouseButtons = new List<uint>();

            foreach (SButton button in buttons)
            {
                if (KeysymNames.TryGetValue(button, out string keysymName))
                {
                    IntPtr keysym = XStringToKeysym(keysymName);
                    byte keycode = XKeysymToKeycode(this.Display, keysym);
                    if (keycode == 0)
                        return; // this X server has no key for that keysym; abort rather than send a partial combo
                    keycodes.Add(keycode);
                }
                else if (MouseButtons.TryGetValue(button, out uint mouseButton))
                    mouseButtons.Add(mouseButton);
                else
                    return;
            }

            foreach (uint code in keycodes)
                XTestFakeKeyEvent(this.Display, code, true, 0);
            foreach (uint button in mouseButtons)
                XTestFakeButtonEvent(this.Display, button, true, 0);

            // A real gotcha: releasing immediately (delay 0) can be too fast for the target
            // application's own input polling to ever observe the "down" state between an instant
            // press and release. XTest's delay parameter is relative to the previous event and is
            // paced by the X server itself once queued - not a blocking sleep on this thread - so
            // holding the combo briefly before releasing costs nothing here.
            const ulong holdMs = 50;
            bool firstRelease = true;
            for (int i = mouseButtons.Count - 1; i >= 0; i--)
            {
                XTestFakeButtonEvent(this.Display, mouseButtons[i], false, firstRelease ? holdMs : 0);
                firstRelease = false;
            }
            for (int i = keycodes.Count - 1; i >= 0; i--)
            {
                XTestFakeKeyEvent(this.Display, keycodes[i], false, firstRelease ? holdMs : 0);
                firstRelease = false;
            }

            XFlush(this.Display);
        }

        public void Dispose()
        {
            if (this.Display != IntPtr.Zero)
                XCloseDisplay(this.Display);
        }

        private static Dictionary<SButton, string> BuildKeysymNames()
        {
            var map = new Dictionary<SButton, string>
            {
                [SButton.LeftControl] = "Control_L",
                [SButton.RightControl] = "Control_R",
                [SButton.LeftAlt] = "Alt_L",
                [SButton.RightAlt] = "Alt_R",
                [SButton.LeftShift] = "Shift_L",
                [SButton.RightShift] = "Shift_R",
                [SButton.LeftWindows] = "Super_L",
                [SButton.RightWindows] = "Super_R",
                [SButton.Space] = "space",
                [SButton.Enter] = "Return",
                [SButton.Escape] = "Escape",
                [SButton.Tab] = "Tab",
                [SButton.Back] = "BackSpace",
                [SButton.Delete] = "Delete",
                [SButton.Insert] = "Insert",
                [SButton.Home] = "Home",
                [SButton.End] = "End",
                [SButton.PageUp] = "Page_Up",
                [SButton.PageDown] = "Page_Down",
                [SButton.Up] = "Up",
                [SButton.Down] = "Down",
                [SButton.Left] = "Left",
                [SButton.Right] = "Right",
                [SButton.CapsLock] = "Caps_Lock",
                [SButton.NumLock] = "Num_Lock",
                [SButton.Scroll] = "Scroll_Lock",
                [SButton.Pause] = "Pause",
                [SButton.PrintScreen] = "Print",
                [SButton.Add] = "KP_Add",
                [SButton.Subtract] = "KP_Subtract",
                [SButton.Multiply] = "KP_Multiply",
                [SButton.Divide] = "KP_Divide",
                [SButton.Decimal] = "KP_Decimal",
                [SButton.OemPlus] = "equal",
                [SButton.OemMinus] = "minus",
                [SButton.OemComma] = "comma",
                [SButton.OemPeriod] = "period",
                [SButton.OemQuestion] = "slash",
                [SButton.OemSemicolon] = "semicolon",
                [SButton.OemQuotes] = "apostrophe",
                [SButton.OemOpenBrackets] = "bracketleft",
                [SButton.OemCloseBrackets] = "bracketright",
                [SButton.OemPipe] = "backslash",
                [SButton.OemBackslash] = "backslash",
                [SButton.OemTilde] = "grave",
            };

            for (char c = 'A'; c <= 'Z'; c++)
                map[Enum.Parse<SButton>(c.ToString())] = c.ToString().ToLowerInvariant();

            for (int i = 0; i <= 9; i++)
                map[Enum.Parse<SButton>($"D{i}")] = i.ToString();

            for (int i = 0; i <= 9; i++)
                map[Enum.Parse<SButton>($"NumPad{i}")] = $"KP_{i}";

            for (int i = 1; i <= 24; i++)
                map[Enum.Parse<SButton>($"F{i}")] = $"F{i}";

            return map;
        }
    }
}
