using Quantum.Editor.Avalonia.Services.Workspaces;

namespace Quantum.Tests;

public sealed class WorkspaceProfileInfrastructureTests
{
    [Fact]
    public void Catalog_RegistersAndLooksUpProfileByStableIdentifier()
    {
        var catalog = new WorkspaceProfileCatalog();
        var profile = new WorkspaceProfile(
            new WorkspaceProfileId("custom"),
            "Custom",
            WorkspaceComposition.CreateComingSoon(new WorkspaceProfileId("custom"), "Custom"));

        catalog.Register(profile);

        Assert.True(catalog.Contains(profile.Id));
        Assert.True(catalog.TryGet(profile.Id, out WorkspaceProfile lookedUp));
        Assert.Same(profile, lookedUp);
        Assert.Same(profile, catalog.Get(profile.Id));
        Assert.Throws<InvalidOperationException>(() => catalog.Register(profile));
    }

    [Fact]
    public void DefaultCatalog_SelectsCompleteTrackWorkspaceAndExposesFutureDefinitions()
    {
        WorkspaceProfileCatalog catalog = WorkspaceProfileCatalog.CreateDefault();

        Assert.Equal(WorkspaceProfileId.Track, catalog.DefaultProfileId);
        WorkspaceProfile track = catalog.DefaultProfile;
        Assert.Equal("Track", track.DisplayName);
        Assert.True(track.IsAvailable);
        Assert.True(track.IsVisible);
        Assert.Equal(5, track.AvailablePanes.Count);
        Assert.All(track.AvailablePanes, pane => Assert.True(track.IsPaneVisibleByDefault(pane)));
        Assert.True(track.HasCommandGroup(WorkspaceCommandGroupIds.File));
        Assert.True(track.HasCommandGroup(WorkspaceCommandGroupIds.Edit));
        Assert.True(track.HasCommandGroup(WorkspaceCommandGroupIds.View));
        Assert.True(track.IsOverlayEnabledByDefault(WorkspaceOverlayIds.TransportedFrames));

        Assert.Equal(5, catalog.Profiles.Count);
        Assert.Collection(catalog.AvailableProfiles, profile => Assert.Equal(WorkspaceProfileId.Track, profile.Id));
        Assert.Equal(5, catalog.VisibleProfiles.Count);
        Assert.False(catalog.Get(WorkspaceProfileId.Train).IsAvailable);
        Assert.True(catalog.Get(WorkspaceProfileId.Support).IsVisible);
        Assert.False(catalog.Get(WorkspaceProfileId.Terrain).IsAvailable);
        Assert.True(catalog.Get(WorkspaceProfileId.Simulation).IsVisible);
    }

    [Fact]
    public void Catalog_LooksUpCompositionThroughRegisteredProfile()
    {
        WorkspaceProfileCatalog catalog = WorkspaceProfileCatalog.CreateDefault();
        WorkspaceProfile track = catalog.Get(WorkspaceProfileId.Track);

        Assert.Same(track.Composition, catalog.GetComposition(WorkspaceProfileId.Track));
        Assert.True(catalog.TryGetComposition(
            WorkspaceProfileId.Track,
            out WorkspaceComposition composition));
        Assert.Same(track.Composition, composition);
        Assert.False(catalog.TryGetComposition(
            new WorkspaceProfileId("missing"),
            out _));
    }

    [Fact]
    public void Manager_UsesCatalogDefaultProfile()
    {
        WorkspaceProfileCatalog catalog = WorkspaceProfileCatalog.CreateDefault();

        var manager = new WorkspaceProfileManager(catalog);

        Assert.Same(catalog.DefaultProfile, manager.CurrentProfile);
        Assert.Same(manager.CurrentProfile, manager.ActiveProfile);
        Assert.Equal(WorkspaceProfileId.Track, manager.CurrentProfileId);
    }

    [Fact]
    public void Manager_SwitchesOnlyToAvailableRegisteredProfilesAndNotifiesOnce()
    {
        var catalog = new WorkspaceProfileCatalog();
        var track = new WorkspaceProfile(
            WorkspaceProfileId.Track,
            "Track",
            WorkspaceComposition.CreateComingSoon(WorkspaceProfileId.Track, "Track"));
        var alternateId = new WorkspaceProfileId("alternate");
        var alternate = new WorkspaceProfile(
            alternateId,
            "Alternate",
            WorkspaceComposition.CreateComingSoon(alternateId, "Alternate"));
        var unavailable = new WorkspaceProfile(
            WorkspaceProfileId.Train,
            "Train",
            WorkspaceComposition.CreateComingSoon(WorkspaceProfileId.Train, "Train"),
            isAvailable: false,
            isVisible: true);
        catalog.Register(track, makeDefault: true);
        catalog.Register(alternate);
        catalog.Register(unavailable);
        var manager = new WorkspaceProfileManager(catalog);
        int notifications = 0;
        manager.CurrentProfileChanged += (_, _) => notifications++;

        Assert.True(manager.TrySwitchTo(alternate.Id));
        Assert.Same(alternate, manager.CurrentProfile);
        Assert.Equal(1, notifications);

        manager.SwitchTo(alternate.Id);
        Assert.Equal(1, notifications);

        Assert.False(manager.TrySwitchTo(unavailable.Id));
        Assert.False(manager.TrySwitchTo(new WorkspaceProfileId("missing")));
        Assert.Same(alternate, manager.CurrentProfile);
        Assert.Equal(1, notifications);
        Assert.Throws<InvalidOperationException>(() => manager.SwitchTo(unavailable.Id));

        manager.ResetToDefault();
        Assert.Same(track, manager.CurrentProfile);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void Selector_InitializesWithTrackAndFourDisabledComingSoonWorkspaces()
    {
        var manager = new WorkspaceProfileManager(WorkspaceProfileCatalog.CreateDefault());

        var selector = new WorkspaceSelectorModel(manager);

        Assert.Equal(WorkspaceProfileId.Track, selector.SelectedItem.Id);
        Assert.Equal(
            new[]
            {
                WorkspaceProfileId.Track,
                WorkspaceProfileId.Train,
                WorkspaceProfileId.Support,
                WorkspaceProfileId.Terrain,
                WorkspaceProfileId.Simulation
            },
            selector.Items.Select(item => item.Id));
        Assert.True(selector.Items[0].IsEnabled);
        Assert.Equal("Track", selector.Items[0].DisplayText);
        Assert.All(
            selector.Items.Skip(1),
            item =>
            {
                Assert.False(item.IsEnabled);
                Assert.EndsWith(" (Coming Soon)", item.DisplayText);
            });
    }

    [Fact]
    public void Selector_ActivatesTrackAndRejectsDisabledPlaceholderWorkspace()
    {
        var manager = new WorkspaceProfileManager(WorkspaceProfileCatalog.CreateDefault());
        var selector = new WorkspaceSelectorModel(manager);

        Assert.True(selector.TryActivate(WorkspaceProfileId.Track));
        Assert.Equal(WorkspaceProfileId.Track, selector.SelectedItem.Id);

        Assert.False(selector.TryActivate(WorkspaceProfileId.Train));
        Assert.Equal(WorkspaceProfileId.Track, selector.SelectedItem.Id);
    }
}
