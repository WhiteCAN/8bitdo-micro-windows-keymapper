using System.Text.Json;
using System.Text.Json.Serialization;
using MicroKeyStudio.Protocol.Actions;
using MicroKeyStudio.Protocol.Profiles;

namespace MicroKeyStudio.Storage;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static async Task SaveAsync(MappingProfile profile, string path, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, profile, Options, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<MappingProfile> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Profile file was not found.", path);
        }

        await using FileStream stream = File.OpenRead(path);
        MappingProfile? profile = await JsonSerializer.DeserializeAsync<MappingProfile>(stream, Options, cancellationToken).ConfigureAwait(false);
        return profile ?? throw new InvalidDataException("Profile file did not contain a profile.");
    }

    public static async Task SaveLibraryAsync(ProfileLibrary library, string path, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, library, Options, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ProfileLibrary> LoadLibraryAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return ProfileLibrary.Empty;
        }

        await using FileStream stream = File.OpenRead(path);
        ProfileLibrary? library = await JsonSerializer.DeserializeAsync<ProfileLibrary>(stream, Options, cancellationToken).ConfigureAwait(false);
        return library ?? ProfileLibrary.Empty;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new MappedActionJsonConverter());
        return options;
    }

    private sealed class MappedActionJsonConverter : JsonConverter<MappedAction>
    {
        public override MappedAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            string type = root.GetProperty("type").GetString()
                ?? throw new JsonException("Mapped action type is missing.");

            return type switch
            {
                "keyboardKey" => new MappedAction.KeyboardKey(root.GetProperty("key").GetString() ?? string.Empty),
                "keyChord" => new MappedAction.KeyChord(ReadStringArray(root.GetProperty("keys"))),
                "mouseAction" => new MappedAction.MouseAction(root.GetProperty("action").GetString() ?? string.Empty),
                "mediaKey" => new MappedAction.MediaKey(root.GetProperty("key").GetString() ?? string.Empty),
                "macro" => new MappedAction.Macro(root.GetProperty("macroId").GetString() ?? string.Empty),
                "disabled" => new MappedAction.Disabled(),
                "passThrough" => new MappedAction.PassThrough(),
                _ => throw new JsonException($"Unknown mapped action type '{type}'.")
            };
        }

        public override void Write(Utf8JsonWriter writer, MappedAction value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            switch (value)
            {
                case MappedAction.KeyboardKey keyboardKey:
                    writer.WriteString("type", "keyboardKey");
                    writer.WriteString("key", keyboardKey.Key);
                    break;
                case MappedAction.KeyChord keyChord:
                    writer.WriteString("type", "keyChord");
                    writer.WritePropertyName("keys");
                    JsonSerializer.Serialize(writer, keyChord.Keys, options);
                    break;
                case MappedAction.MouseAction mouseAction:
                    writer.WriteString("type", "mouseAction");
                    writer.WriteString("action", mouseAction.Action);
                    break;
                case MappedAction.MediaKey mediaKey:
                    writer.WriteString("type", "mediaKey");
                    writer.WriteString("key", mediaKey.Key);
                    break;
                case MappedAction.Macro macro:
                    writer.WriteString("type", "macro");
                    writer.WriteString("macroId", macro.MacroId);
                    break;
                case MappedAction.Disabled:
                    writer.WriteString("type", "disabled");
                    break;
                case MappedAction.PassThrough:
                    writer.WriteString("type", "passThrough");
                    break;
                default:
                    throw new JsonException($"Unsupported mapped action type '{value.GetType().Name}'.");
            }
            writer.WriteEndObject();
        }

        private static IReadOnlyList<string> ReadStringArray(JsonElement element)
        {
            return element.EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray();
        }
    }
}
