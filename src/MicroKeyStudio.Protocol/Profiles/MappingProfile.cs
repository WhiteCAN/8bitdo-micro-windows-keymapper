using MicroKeyStudio.Protocol.Actions;
using MicroKeyStudio.Protocol.Buttons;

namespace MicroKeyStudio.Protocol.Profiles;

public sealed record MappingProfile(
    string Id,
    string Name,
    int SchemaVersion,
    IReadOnlyDictionary<MicroButton, MappedAction> Mappings);
