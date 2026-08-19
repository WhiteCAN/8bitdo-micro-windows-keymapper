namespace MicroKeyStudio.Protocol.Profiles;

public sealed record ProfileLibrary(
    int SchemaVersion,
    string? SelectedProfileId,
    IReadOnlyList<MappingProfile> Profiles)
{
    public static ProfileLibrary Empty { get; } = new(1, null, Array.Empty<MappingProfile>());
}
