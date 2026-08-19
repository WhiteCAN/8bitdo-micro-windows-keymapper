using System.Text.Json;

namespace MicroKeyStudio.Storage.Tests;

public sealed class MicroConfigBackupStoreTests
{
    [Fact]
    public async Task Save_and_load_round_trip_a_private_180_byte_snapshot()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string unrelatedPath = Path.Combine(directory, "keep-me.txt");
            await File.WriteAllTextAsync(unrelatedPath, "unrelated", CancellationToken.None);
            byte[] payload = Enumerable.Range(0, 180).Select(value => (byte)value).ToArray();
            DateTimeOffset createdAt = new(2026, 8, 16, 14, 15, 16, TimeSpan.FromHours(9));

            string path = await MicroConfigBackupStore.SaveAsync(payload, directory, createdAt, CancellationToken.None);
            MicroConfigBackup loaded = await MicroConfigBackupStore.LoadAsync(path, CancellationToken.None);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path, CancellationToken.None));

            Assert.StartsWith("micro-config-backup-20260816-", Path.GetFileName(path));
            Assert.Equal(payload, loaded.Payload);
            Assert.Equal(createdAt, loaded.CreatedAt);
            Assert.DoesNotContain("device", await File.ReadAllTextAsync(path, CancellationToken.None), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(new[] { "schema", "timestamp", "payload" }, document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.Equal("unrelated", await File.ReadAllTextAsync(unrelatedPath, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Save_rejects_payloads_that_are_not_180_bytes()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                MicroConfigBackupStore.SaveAsync(new byte[179], directory, DateTimeOffset.UtcNow, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_rejects_wrong_schema_version()
    {
        string path = await WriteBackupJsonAsync("{\"schema\":2,\"timestamp\":\"2026-08-16T05:15:16+00:00\",\"payload\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => MicroConfigBackupStore.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task Load_rejects_payloads_that_are_not_180_bytes()
    {
        string path = await WriteBackupJsonAsync("{\"schema\":1,\"timestamp\":\"2026-08-16T05:15:16+00:00\",\"payload\":\"AA==\"}");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => MicroConfigBackupStore.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task Load_rejects_extra_device_identifier_fields()
    {
        string path = await WriteBackupJsonAsync(CreateBackupJson(extraField: "\"deviceName\":\"Micro\""));
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => MicroConfigBackupStore.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("timestamp")]
    [InlineData("payload")]
    public async Task Load_rejects_backups_missing_required_fields(string missingField)
    {
        string path = await WriteBackupJsonAsync(CreateBackupJson(missingField));
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => MicroConfigBackupStore.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"micro-config-backup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task<string> WriteBackupJsonAsync(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "backup.json");
        await File.WriteAllTextAsync(path, json, CancellationToken.None);
        return path;
    }

    private static string CreateBackupJson(string? missingField = null, string extraField = "")
    {
        var fields = new List<string>();
        if (missingField != "schema")
        {
            fields.Add("\"schema\":1");
        }
        if (missingField != "timestamp")
        {
            fields.Add("\"timestamp\":\"2026-08-16T05:15:16+00:00\"");
        }
        if (missingField != "payload")
        {
            fields.Add($"\"payload\":\"{Convert.ToBase64String(new byte[180])}\"");
        }
        if (!string.IsNullOrEmpty(extraField))
        {
            fields.Add(extraField);
        }

        return $"{{{string.Join(',', fields)}}}";
    }
}
