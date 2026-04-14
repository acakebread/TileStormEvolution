using UnityEngine;
using UnityEngine.InputSystem;

public static class LegacyInputMappingUtil
{
    public static Key ToKey(this KeyCode oldKey)
    {
        // Mouse, joystick, and other clearly non-keyboard keys
        if (oldKey == KeyCode.None ||
            (oldKey >= KeyCode.Mouse0 && oldKey <= KeyCode.Mouse6) ||
            (oldKey >= KeyCode.JoystickButton0 && oldKey <= KeyCode.JoystickButton19))
        {
            return Key.None;
        }

        // Full explicit remapping - no assumptions, no casting
        return oldKey switch
        {
            KeyCode.LeftArrow   => Key.LeftArrow,
            KeyCode.RightArrow  => Key.RightArrow,
            KeyCode.UpArrow     => Key.UpArrow,
            KeyCode.DownArrow   => Key.DownArrow,

            KeyCode.LeftShift   => Key.LeftShift,
            KeyCode.RightShift  => Key.RightShift,
            KeyCode.LeftControl => Key.LeftCtrl,
            KeyCode.RightControl=> Key.RightCtrl,
            KeyCode.LeftAlt     => Key.LeftAlt,
            KeyCode.RightAlt    => Key.RightAlt,

            KeyCode.Space       => Key.Space,
            KeyCode.Return      => Key.Enter,
            KeyCode.KeypadEnter => Key.NumpadEnter,
            KeyCode.Backspace   => Key.Backspace,
            KeyCode.Delete      => Key.Delete,
            KeyCode.Escape      => Key.Escape,
            KeyCode.Tab         => Key.Tab,

            KeyCode.Home        => Key.Home,
            KeyCode.End         => Key.End,
            KeyCode.PageUp      => Key.PageUp,
            KeyCode.PageDown    => Key.PageDown,
            KeyCode.Insert      => Key.Insert,

            // All letters A-Z
            KeyCode.A => Key.A, KeyCode.B => Key.B, KeyCode.C => Key.C,
            KeyCode.D => Key.D, KeyCode.E => Key.E, KeyCode.F => Key.F,
            KeyCode.G => Key.G, KeyCode.H => Key.H, KeyCode.I => Key.I,
            KeyCode.J => Key.J, KeyCode.K => Key.K, KeyCode.L => Key.L,
            KeyCode.M => Key.M, KeyCode.N => Key.N, KeyCode.O => Key.O,
            KeyCode.P => Key.P, KeyCode.Q => Key.Q, KeyCode.R => Key.R,
            KeyCode.S => Key.S, KeyCode.T => Key.T, KeyCode.U => Key.U,
            KeyCode.V => Key.V, KeyCode.W => Key.W, KeyCode.X => Key.X,
            KeyCode.Y => Key.Y, KeyCode.Z => Key.Z,

            // Numbers (top row)
            KeyCode.Alpha0 => Key.Digit0,
            KeyCode.Alpha1 => Key.Digit1,
            KeyCode.Alpha2 => Key.Digit2,
            KeyCode.Alpha3 => Key.Digit3,
            KeyCode.Alpha4 => Key.Digit4,
            KeyCode.Alpha5 => Key.Digit5,
            KeyCode.Alpha6 => Key.Digit6,
            KeyCode.Alpha7 => Key.Digit7,
            KeyCode.Alpha8 => Key.Digit8,
            KeyCode.Alpha9 => Key.Digit9,

            // Function keys
            KeyCode.F1  => Key.F1,  KeyCode.F2  => Key.F2,  KeyCode.F3  => Key.F3,
            KeyCode.F4  => Key.F4,  KeyCode.F5  => Key.F5,  KeyCode.F6  => Key.F6,
            KeyCode.F7  => Key.F7,  KeyCode.F8  => Key.F8,  KeyCode.F9  => Key.F9,
            KeyCode.F10 => Key.F10, KeyCode.F11 => Key.F11, KeyCode.F12 => Key.F12,

            // Everything else (punctuation, rare keys, etc.)
            _ => Key.None
        };
    }
}