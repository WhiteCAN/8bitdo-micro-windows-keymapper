using System.Windows.Input;
using MicroKeyStudio.App.Input;
using MicroKeyStudio.Protocol.Configuration;

namespace MicroKeyStudio.App.Tests;

public sealed class KeyboardChordFormatterTests
{
    [Theory]
    [InlineData(Key.C, ModifierKeys.None, "C")]
    [InlineData(Key.C, ModifierKeys.Control, "Ctrl+C")]
    [InlineData(Key.S, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+S")]
    [InlineData(Key.Escape, ModifierKeys.None, "Esc")]
    [InlineData(Key.Escape, ModifierKeys.Control, "Ctrl+Esc")]
    public void Physical_key_is_normalized(Key key, ModifierKeys modifiers, string expected)
    {
        bool formatted = KeyboardChordFormatter.TryFormat(key, modifiers, out string action);

        Assert.True(formatted);
        Assert.Equal(expected, action);
    }

    [Fact]
    public void Physical_key_uses_canonical_modifier_order()
    {
        bool formatted = KeyboardChordFormatter.TryFormat(
            Key.C,
            ModifierKeys.Windows | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Control,
            out string action);

        Assert.True(formatted);
        Assert.Equal("Ctrl+Alt+Shift+Win+C", action);
    }

    [Theory]
    [InlineData(Key.Oem3, "Shift+`")]
    [InlineData(Key.D1, "Shift+1")]
    [InlineData(Key.D0, "Shift+0")]
    [InlineData(Key.OemMinus, "Shift+-")]
    [InlineData(Key.OemPlus, "Shift+=")]
    [InlineData(Key.Oem4, "Shift+[")]
    [InlineData(Key.Oem6, "Shift+]")]
    [InlineData(Key.Oem5, "Shift+\\")]
    [InlineData(Key.Oem1, "Shift+;")]
    [InlineData(Key.Oem7, "Shift+'")]
    [InlineData(Key.OemComma, "Shift+,")]
    [InlineData(Key.OemPeriod, "Shift+.")]
    [InlineData(Key.Oem2, "Shift+/")]
    public void Physical_shift_symbol_keys_use_their_base_hid_key(Key key, string expected)
    {
        bool formatted = KeyboardChordFormatter.TryFormat(key, ModifierKeys.Shift, out string action);

        Assert.True(formatted);
        Assert.Equal(expected, action);
        Assert.True(MicroHidCodec.TryEncode(action, out _, out string? error), error);
    }

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.LWin)]
    public void Modifier_only_physical_key_is_not_formatted(Key key)
    {
        bool formatted = KeyboardChordFormatter.TryFormat(key, ModifierKeys.None, out string action);

        Assert.False(formatted);
        Assert.Equal(string.Empty, action);
    }

    [Fact]
    public void System_key_is_rejected_until_the_caller_normalizes_it_to_the_actual_key()
    {
        bool formatted = KeyboardChordFormatter.TryFormat(Key.System, ModifierKeys.Control, out string action);

        Assert.False(formatted);
        Assert.Equal(string.Empty, action);
    }
}
