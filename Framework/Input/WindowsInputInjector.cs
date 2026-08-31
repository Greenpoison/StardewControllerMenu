using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using StardewModdingAPI;

namespace StardewControllerMenu.Framework.Input
{
    /// <summary>
    /// Simulates keyboard input via the Win32 <c>SendInput</c> API. This is the standard automation
    /// approach on real Windows, and it also works when the game is running under Wine/Proton (as it
    /// does when a Steam Deck player installs the Windows build of the game) because Wine implements
    /// the same user32.dll entry point for compatibility with real Windows programs.
    /// XNA/MonoGame's <see cref="Keys"/> enum values are defined to equal the matching Win32
    /// virtual-key codes, so no separate translation table is needed here.
    /// </summary>
    internal class WindowsInputInjector : IInputInjector
    {
        private const int InputTypeKeyboard = 1;
        private const uint KeyEventFlagKeyUp = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKeyCode;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KeyboardInput Keyboard;
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

        public bool CanSend(SButton button) => button.TryGetKeyboard(out _);

        public void Send(SButton[] buttons)
        {
            var virtualKeys = new List<ushort>();
            foreach (SButton button in buttons)
            {
                if (!button.TryGetKeyboard(out Microsoft.Xna.Framework.Input.Keys key))
                    return;
                virtualKeys.Add((ushort)key);
            }

            var inputs = new List<NativeInput>();
            foreach (ushort vk in virtualKeys)
                inputs.Add(MakeKeyInput(vk, keyUp: false));
            for (int i = virtualKeys.Count - 1; i >= 0; i--)
                inputs.Add(MakeKeyInput(virtualKeys[i], keyUp: true));

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
    }
}
