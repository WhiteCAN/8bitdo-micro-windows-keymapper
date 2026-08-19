namespace MicroKeyStudio.Protocol.Configuration;

public static class MicroHidCodec
{
    private static readonly Dictionary<string, byte> KeyUsages = CreateKeyUsages();
    private static readonly Dictionary<byte, string> KeyLabels = KeyUsages
        .ToDictionary(pair => pair.Value, pair => pair.Key);
    private static readonly Dictionary<string, byte> ModifierUsages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = 0xe0,
        ["Shift"] = 0xe1,
        ["Alt"] = 0xe2,
        ["Win"] = 0xe3
    };
    private static readonly Dictionary<string, byte> ActionAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Back"] = 0x2a,
        ["PageUp"] = 0x4b,
        ["PageDown"] = 0x4e,
        ["Null"] = 0x00
    };
    private static readonly byte[] ModifierOrder = { 0xe0, 0xe2, 0xe1, 0xe3 };

    public static bool TryEncode(string action, out byte[] usages, out string? error)
    {
        usages = Array.Empty<byte>();
        error = null;
        if (string.IsNullOrWhiteSpace(action))
        {
            error = "An action is required.";
            return false;
        }

        string[] labels = ParseLabels(action);
        if (labels.Length == 0 || labels.Length > 4)
        {
            error = "An action must contain between one and four usages.";
            return false;
        }

        if (labels.Length > 1 && labels.Any(label => string.Equals(label, "None", StringComparison.OrdinalIgnoreCase) || string.Equals(label, "Null", StringComparison.OrdinalIgnoreCase)))
        {
            error = "None cannot be combined with another usage.";
            return false;
        }

        var modifiers = new HashSet<byte>();
        byte? key = null;
        foreach (string label in labels)
        {
            if (ModifierUsages.TryGetValue(label, out byte modifier))
            {
                if (!modifiers.Add(modifier))
                {
                    error = $"Duplicate modifier: {label}.";
                    return false;
                }

                continue;
            }

            if ((!KeyUsages.TryGetValue(label, out byte keyUsage) && !ActionAliases.TryGetValue(label, out keyUsage)) || key is not null)
            {
                error = $"Unknown or duplicate key: {label}.";
                return false;
            }

            key = keyUsage;
        }

        usages = new byte[4];
        int index = 0;
        foreach (byte modifier in ModifierOrder)
        {
            if (modifiers.Contains(modifier))
            {
                usages[index++] = modifier;
            }
        }

        if (key is not null)
        {
            usages[index] = key.Value;
        }

        return true;
    }

    private static string[] ParseLabels(string action)
    {
        string trimmedAction = action.Trim();
        if (KeyUsages.ContainsKey(trimmedAction) || ActionAliases.ContainsKey(trimmedAction))
        {
            return new[] { trimmedAction };
        }

        string[] parts = action.Split('+', StringSplitOptions.None);
        var labels = new List<string>(parts.Length);
        for (int index = 0; index < parts.Length; index++)
        {
            string label = parts[index].Trim();
            if (string.Equals(label, "Keypad", StringComparison.OrdinalIgnoreCase) && index + 1 < parts.Length && string.IsNullOrWhiteSpace(parts[index + 1]))
            {
                labels.Add("Keypad +");
                index++;
                continue;
            }

            labels.Add(label);
        }

        return labels.ToArray();
    }

    public static string Decode(ReadOnlySpan<byte> usages)
    {
        var modifiers = new HashSet<byte>();
        byte? key = null;
        foreach (byte usage in usages)
        {
            if (usage == 0)
            {
                continue;
            }

            if (ModifierUsages.Values.Contains(usage))
            {
                modifiers.Add(usage);
            }
            else if (key is null)
            {
                key = usage;
            }
            else
            {
                return string.Join(' ', usages.ToArray().Select(value => $"{value:x2}"));
            }
        }

        var labels = ModifierOrder
            .Where(modifier => modifiers.Contains(modifier))
            .Select(modifier => ModifierUsages.First(pair => pair.Value == modifier).Key)
            .ToList();
        if (key is not null)
        {
            labels.Add(KeyLabels.TryGetValue(key.Value, out string? label) ? label : $"0x{key.Value:x2}");
        }

        return labels.Count == 0 ? "None" : string.Join("+", labels);
    }

    private static Dictionary<string, byte> CreateKeyUsages()
    {
        var usages = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["`"] = 0x35,
            ["1"] = 0x1e,
            ["2"] = 0x1f,
            ["3"] = 0x20,
            ["4"] = 0x21,
            ["5"] = 0x22,
            ["6"] = 0x23,
            ["7"] = 0x24,
            ["8"] = 0x25,
            ["9"] = 0x26,
            ["0"] = 0x27,
            ["-"] = 0x2d,
            ["="] = 0x2e,
            ["["] = 0x2f,
            ["]"] = 0x30,
            ["\\"] = 0x31,
            [";"] = 0x33,
            ["'"] = 0x34,
            [","] = 0x36,
            ["."] = 0x37,
            ["/"] = 0x38,
            ["Enter"] = 0x28,
            ["Esc"] = 0x29,
            ["Backspace"] = 0x2a,
            ["Tab"] = 0x2b,
            ["Space"] = 0x2c,
            ["Caps Lock"] = 0x39,
            ["Print Screen"] = 0x46,
            ["Scroll Lock"] = 0x47,
            ["Pause"] = 0x48,
            ["Insert"] = 0x49,
            ["Home"] = 0x4a,
            ["Page Up"] = 0x4b,
            ["Delete"] = 0x4c,
            ["End"] = 0x4d,
            ["Page Down"] = 0x4e,
            ["Right"] = 0x4f,
            ["Left"] = 0x50,
            ["Down"] = 0x51,
            ["Up"] = 0x52,
            ["Num Lock"] = 0x53,
            ["Keypad /"] = 0x54,
            ["Keypad *"] = 0x55,
            ["Keypad -"] = 0x56,
            ["Keypad +"] = 0x57,
            ["Keypad Enter"] = 0x58,
            ["Keypad 1"] = 0x59,
            ["Keypad 2"] = 0x5a,
            ["Keypad 3"] = 0x5b,
            ["Keypad 4"] = 0x5c,
            ["Keypad 5"] = 0x5d,
            ["Keypad 6"] = 0x5e,
            ["Keypad 7"] = 0x5f,
            ["Keypad 8"] = 0x60,
            ["Keypad 9"] = 0x61,
            ["Keypad 0"] = 0x62,
            ["Keypad ."] = 0x63,
            ["None"] = 0x00
        };

        for (int index = 0; index < 26; index++)
        {
            usages[((char)('A' + index)).ToString()] = (byte)(0x04 + index);
        }

        for (int index = 1; index <= 24; index++)
        {
            usages[$"F{index}"] = (byte)(index <= 12 ? 0x39 + index : 0x5b + index);
        }

        return usages;
    }
}
