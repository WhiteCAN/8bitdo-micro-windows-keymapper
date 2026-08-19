using System.Windows.Input;

namespace MicroKeyStudio.App.Input;

public static class KeyboardChordFormatter
{
    public static bool TryFormat(Key key, ModifierKeys modifiers, out string action)
    {
        action = string.Empty;
        if (IsModifierKey(key) || !TryFormatKey(key, out string keyLabel))
        {
            return false;
        }

        var labels = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            labels.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            labels.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            labels.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            labels.Add("Win");
        }

        labels.Add(keyLabel);
        action = string.Join("+", labels);
        return true;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
    }

    private static bool TryFormatKey(Key key, out string label)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            label = key.ToString();
            return true;
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            label = key.ToString()[1..];
            return true;
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            label = key.ToString();
            return true;
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            label = $"Keypad {key.ToString()[6..]}";
            return true;
        }

        label = key switch
        {
            Key.Oem3 => "`",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.Oem4 => "[",
            Key.Oem6 => "]",
            Key.Oem5 => "\\",
            Key.Oem1 => ";",
            Key.Oem7 => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.Oem2 => "/",
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Tab => "Tab",
            Key.Space => "Space",
            Key.Capital => "Caps Lock",
            Key.PrintScreen => "Print Screen",
            Key.Scroll => "Scroll Lock",
            Key.Pause => "Pause",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.Prior => "Page Up",
            Key.Delete => "Delete",
            Key.End => "End",
            Key.Next => "Page Down",
            Key.Right => "Right",
            Key.Left => "Left",
            Key.Down => "Down",
            Key.Up => "Up",
            Key.NumLock => "Num Lock",
            Key.Divide => "Keypad /",
            Key.Multiply => "Keypad *",
            Key.Subtract => "Keypad -",
            Key.Add => "Keypad +",
            Key.Decimal => "Keypad .",
            _ => string.Empty
        };

        return label.Length > 0;
    }
}
