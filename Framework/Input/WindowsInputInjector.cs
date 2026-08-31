using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using StardewModdingAPI;

namespace StardewControllerMenu.Framework.Input
{
    /// <summary>
    /// Simulates keyboard and mouse-button input via the Win32 <c>SendInput</c> API. This is the
    /// standard automation approach on real Windows, and it also works when the game is running
    /// under Wine/Proton (as it does when a Steam Deck player installs the Windows build of the
    /// game) because Wine implements the same user32.dll entry point for compatibility with real
    /// Windows programs.
    /// XNA/MonoGame's <see cref="Keys"/> enum values are defined to equal the matching Win32
    /// virtual-key codes, so no separate translation table is needed for keyboard buttons.
    /// </summary>
    internal class WindowsInputInjector : IInputInjector
    {
        private const int InputTypeMouse = 0;
        private const int InputTypeKeyboard = 1;
        private const uint KeyEventFlagKeyUp = 0x0002;

        private const uint MouseEventFLeftDown = 0x0002;
        private const uint MouseEventFLeftUp = 0x0004;
        private const uint MouseEventFRightDown = 0x0008;
        private const uint MouseEventFRightUp = 0x0010;
        private const uint MouseEventFMiddleDown = 0x0020;
        private const uint MouseEventFMiddleUp = 0x0040;
        private const uint MouseEventFXDown = 0x0080;
        private const uint MouseEventFXUp = 0x0100;

        // Win32 XBUTTON1/XBUTTON2 identifiers, carried in MOUSEINPUT.mouseData alongside the X-button flags.
        private static readonly Dictionary<SButton, (uint DownFlag, uint UpFlag, uint Data)> MouseButtons = new()
        {
            [SButton.MouseLeft] = (MouseEventFLeftDown, MouseEventFLeftUp, 0),
            [SButton.MouseRight] = (MouseEventFRightDown, MouseEventFRightUp, 0),
            [SButton.MouseMiddle] = (MouseEventFMiddleDown, MouseEventFMiddleUp, 0),
            [SButton.MouseX1] = (MouseEventFXDown, MouseEventFXUp, 1),
            [SButton.MouseX2] = (MouseEventFXDown, MouseEventFXUp, 2),
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKeyCode;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int Dx;
            public int Dy;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KeyboardInput Keyboard;

            [FieldOffset(0)]
            public MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInput
        {
            public int Type;
            public InputUnion Union;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint numInputs, NativeInput[] inputs, int structSize);

        public WindowsInputInjector()
        {
            // Force user32.dll to resolve now, so a missing DLL throws during construction instead of on first use.
            SendInput(0, Array.Empty<NativeInput>(), Marshal.SizeOf<NativeInput>());
        }

        public bool CanSend(SButton button) => button.TryGetKeyboard(out _) || MouseButtons.ContainsKey(button);

        public void Send(SButton[] buttons)
        {
            var keyboardKeys = new List<ushort>();
            var mouseButtons = new List<SButton>();

            foreach (SButton button in buttons)
            {
                if (button.TryGetKeyboard(out Microsoft.Xna.Framework.Input.Keys key))
                    keyboardKeys.Add((ushort)key);
                else if (MouseButtons.ContainsKey(button))
                    mouseButtons.Add(button);
                else
                    return;
            }

            var inputs = new List<NativeInput>();
            foreach (ushort vk in keyboardKeys)
                inputs.Add(MakeKeyInput(vk, keyUp: false));
            foreach (SButton mouseButton in mouseButtons)
                inputs.Add(MakeMouseInput(mouseButton, buttonUp: false));

            for (int i = mouseButtons.Count - 1; i >= 0; i--)
                inputs.Add(MakeMouseInput(mouseButtons[i], buttonUp: true));
            for (int i = keyboardKeys.Count - 1; i >= 0; i--)
                inputs.Add(MakeKeyInput(keyboardKeys[i], keyUp: true));

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<NativeInput>());
        }

        private static NativeInput MakeKeyInput(ushort virtualKeyCode, bool keyUp)
        {
            return new NativeInput
            {
                Type = InputTypeKeyboard,
                Union = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKeyCode = virtualKeyCode,
                        ScanCode = 0,
                        Flags = keyUp ? KeyEventFlagKeyUp : 0,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        private static NativeInput MakeMouseInput(SButton button, bool buttonUp)
        {
            (uint downFlag, uint upFlag, uint data) = MouseButtons[button];
            return new NativeInput
            {
                Type = InputTypeMouse,
                Union = new InputUnion
                {
                    Mouse = new MouseInput
                    {
                        Dx = 0,
                        Dy = 0,
                        MouseData = data,
                        Flags = buttonUp ? upFlag : downFlag,
                        Time = 0,
                        ExtraInfo = IntPtr.Zero
                    }
                }
            };
        }
    }
}
