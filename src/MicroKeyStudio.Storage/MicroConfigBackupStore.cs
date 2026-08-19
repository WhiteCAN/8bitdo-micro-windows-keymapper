using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroKeyStudio.Storage;

public sealed record MicroConfigBackup(DateTimeOffset CreatedAt, byte[] Payload);

public static class MicroConfigBackupStore
{
    private const int SchemaVersion = 1;
    private const int PayloadLength = 180;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<string> SaveAsync(
        byte[] payload,
        string directory,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        if (payload.Length != PayloadLength)
        {
            throw new ArgumentException("A Micro configuration backup payload must contain exactly 180 bytes.", nameof(payload));
        }

        Directory.CreateDirectory(directory);
        string fileName = $"micro-config-backup-{createdAt:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json";
        string path = Path.Combine(directory, fileName);
        var backup = new BackupDocument(SchemaVersion, createdAt, payload);

        await using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, backup, JsonOptions, cancellationToken).ConfigureAwait(false);
        return path;
    }

    public static async Task<MicroConfigBackup> LoadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            ValidateDocumentShape(document.RootElement);
            BackupDocument? backup = document.RootElement.Deserialize<BackupDocument>(JsonOptions);
            if (backup is null || backup.Schema != SchemaVersion)
            {
                throw new InvalidDataException("The Micro configuration backup schema is unsupported.");
            }

            byte[] payload = Convert.FromBase64String(backup.Payload);
            if (payload.Length != PayloadLength)
            {
                throw new InvalidDataException("The Micro configuration backup payload must contain exactly 180 bytes.");
            }

            return new MicroConfigBackup(backup.Timestamp, payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Micro configuration backup is invalid.", exception);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The Micro configuration backup payload is not valid Base64.", exception);
        }
    }

    private static void ValidateDocumentShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The Micro configuration backup must be a JSON object.");
        }

        string[] propertyNames = root.EnumerateObject().Select(property => property.Name).ToArray();
        if (propertyNames.Length != 3 || !propertyNames.ToHashSet(StringComparer.Ordinal).SetEquals(new[] { "schema", "timestamp", "payload" }))
        {
            throw new InvalidDataException("The Micro configuration backup must contain exactly schema, timestamp, and payload.");
        }
    }

    private sealed class BackupDocument
    {
        [JsonPropertyName("schema")]
        public int Schema { get; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; }

        [JsonPropertyName("payload")]
        public string Payload { get; }

        public BackupDocument(int schema, DateTimeOffset timestamp, byte[] payload)
        {
            Schema = schema;
            Timestamp = timestamp;
            Payload = Convert.ToBase64String(payload);
        }

        [JsonConstructor]
        public BackupDocument(int schema, DateTimeOffset timestamp, string payload)
        {
            Schema = schema;
            Timestamp = timestamp;
            Payload = payload;
        }
    }
}
