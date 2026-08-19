using MicroKeyStudio.Protocol.Actions;
using MicroKeyStudio.Protocol.Buttons;
using MicroKeyStudio.Protocol.Profiles;

namespace MicroKeyStudio.Storage.Tests;

public sealed class ProfileStoreTests
{
    [Fact]
    public async Task Profile_library_round_trips_to_json()
    {
        string path = Path.Combine(Path.GetTempPath(), $"microkey-library-{Guid.NewGuid():N}.json");
        var library = new ProfileLibrary(
            1,
            "profile-2",
            new[]
            {
                new MappingProfile(
                    "profile-1",
                    "Default Profile",
                    1,
                    new Dictionary<MicroButton, MappedAction>
                    {
                        [MicroButton.Up] = new MappedAction.KeyboardKey(".")
                    }),
                new MappingProfile(
                    "profile-2",
                    "profile1",
                    1,
                    new Dictionary<MicroButton, MappedAction>
                    {
                        [MicroButton.A] = new MappedAction.KeyChord(new[] { "Alt", "'" })
                    })
            });

        await ProfileStore.SaveLibraryAsync(library, path, CancellationToken.None);
        ProfileLibrary loaded = await ProfileStore.LoadLibraryAsync(path, CancellationToken.None);

        Assert.Equal(library.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal("profile-2", loaded.SelectedProfileId);
        Assert.Equal(2, loaded.Profiles.Count);
        Assert.Equal("Default Profile", loaded.Profiles[0].Name);
        Assert.Equal(new MappedAction.KeyboardKey("."), loaded.Profiles[0].Mappings[MicroButton.Up]);
        Assert.Equal(new MappedAction.KeyChord(new[] { "Alt", "'" }), loaded.Profiles[1].Mappings[MicroButton.A]);
    }

    [Fact]
    public async Task Missing_profile_library_loads_empty_library()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-library-{Guid.NewGuid():N}.json");

        ProfileLibrary loaded = await ProfileStore.LoadLibraryAsync(path, CancellationToken.None);

        Assert.Equal(1, loaded.SchemaVersion);
        Assert.Null(loaded.SelectedProfileId);
        Assert.Empty(loaded.Profiles);
    }

    [Fact]
    public async Task Profile_round_trips_to_json()
    {
        string path = Path.Combine(Path.GetTempPath(), $"microkey-profile-{Guid.NewGuid():N}.json");
        var profile = new MappingProfile(
            "default",
            "Default",
            1,
            new Dictionary<MicroButton, MappedAction>
            {
                [MicroButton.A] = new MappedAction.KeyboardKey("Enter"),
                [MicroButton.B] = new MappedAction.KeyChord(new[] { "Ctrl", "C" })
            });

        await ProfileStore.SaveAsync(profile, path, CancellationToken.None);
        MappingProfile loaded = await ProfileStore.LoadAsync(path, CancellationToken.None);

        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal(profile.Name, loaded.Name);
        Assert.Equal(profile.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(new MappedAction.KeyboardKey("Enter"), loaded.Mappings[MicroButton.A]);
        Assert.Equal(new MappedAction.KeyChord(new[] { "Ctrl", "C" }), loaded.Mappings[MicroButton.B]);
    }

    [Fact]
    public async Task Load_rejects_missing_file()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => ProfileStore.LoadAsync(path, CancellationToken.None));
    }
}
