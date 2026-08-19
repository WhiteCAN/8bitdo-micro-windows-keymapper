using MicroKeyStudio.Protocol.Configuration;

namespace MicroKeyStudio.Protocol.Tests;

public sealed class MicroHidCodecTests
{
    [Theory]
    [InlineData("A", "04 00 00 00")]
    [InlineData("Z", "1d 00 00 00")]
    [InlineData("1", "1e 00 00 00")]
    [InlineData("0", "27 00 00 00")]
    [InlineData("`", "35 00 00 00")]
    [InlineData("/", "38 00 00 00")]
    [InlineData("F1", "3a 00 00 00")]
    [InlineData("F24", "73 00 00 00")]
    [InlineData("Print Screen", "46 00 00 00")]
    [InlineData("Scroll Lock", "47 00 00 00")]
    [InlineData("Pause", "48 00 00 00")]
    [InlineData("Insert", "49 00 00 00")]
    [InlineData("Home", "4a 00 00 00")]
    [InlineData("Page Up", "4b 00 00 00")]
    [InlineData("Delete", "4c 00 00 00")]
    [InlineData("End", "4d 00 00 00")]
    [InlineData("Page Down", "4e 00 00 00")]
    [InlineData("Right", "4f 00 00 00")]
    [InlineData("Left", "50 00 00 00")]
    [InlineData("Down", "51 00 00 00")]
    [InlineData("Up", "52 00 00 00")]
    [InlineData("Caps Lock", "39 00 00 00")]
    [InlineData("Num Lock", "53 00 00 00")]
    [InlineData("Keypad /", "54 00 00 00")]
    [InlineData("Keypad *", "55 00 00 00")]
    [InlineData("Keypad +", "57 00 00 00")]
    [InlineData("Keypad Enter", "58 00 00 00")]
    [InlineData("Keypad 1", "59 00 00 00")]
    [InlineData("Keypad 0", "62 00 00 00")]
    [InlineData("Keypad .", "63 00 00 00")]
    [InlineData("Ctrl", "e0 00 00 00")]
    [InlineData("Shift", "e1 00 00 00")]
    [InlineData("Alt", "e2 00 00 00")]
    [InlineData("Win", "e3 00 00 00")]
    [InlineData("Backspace", "2a 00 00 00")]
    [InlineData("Tab", "2b 00 00 00")]
    [InlineData("Enter", "28 00 00 00")]
    [InlineData("Space", "2c 00 00 00")]
    [InlineData("Esc", "29 00 00 00")]
    [InlineData("None", "00 00 00 00")]
    [InlineData("Ctrl+C", "e0 06 00 00")]
    [InlineData("Ctrl+Shift+S", "e0 e1 16 00")]
    [InlineData("Ctrl+Esc", "e0 29 00 00")]
    [InlineData("Ctrl+Keypad +", "e0 57 00 00")]
    public void Supported_action_encodes_to_literal_hid_usages(string action, string expectedHex)
    {
        Assert.True(MicroHidCodec.TryEncode(action, out byte[] usages, out string? error), error);
        Assert.Equal(Hex(expectedHex), usages);
        Assert.Equal(action, MicroHidCodec.Decode(usages));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+Alt+Shift+Win+C")]
    [InlineData("Mouse Left")]
    [InlineData("Unknown Key")]
    [InlineData("Ctrl++C")]
    [InlineData("+C")]
    [InlineData("C+")]
    [InlineData("Ctrl+None")]
    [InlineData("None+C")]
    public void Unsupported_action_is_rejected_without_usages(string action)
    {
        Assert.False(MicroHidCodec.TryEncode(action, out byte[] usages, out string? error));
        Assert.Empty(usages);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Action_parsing_is_case_insensitive_and_normalizes_modifier_order()
    {
        Assert.True(MicroHidCodec.TryEncode("shift+ctrl+c", out byte[] usages, out string? error), error);
        Assert.Equal(Hex("e0 e1 06 00"), usages);
        Assert.Equal("Ctrl+Shift+C", MicroHidCodec.Decode(usages));
    }

    private static byte[] Hex(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Convert.ToByte(part, 16))
            .ToArray();
    }
}
