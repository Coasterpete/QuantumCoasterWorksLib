using Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using Quantum.Editor.Avalonia.Controls;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services.Docking;
using Quantum.Editor.Avalonia.Services.Trains;
using Quantum.Editor.Avalonia.Services.Workspaces;
using Quantum.Track;

namespace Quantum.Tests;

public sealed class TrainWorkspaceTests
{
    [Fact]
    public void TrainComposition_RegistersThreeRealPaneControls()
    {
        WorkspaceProfile train = WorkspaceProfileCatalog.CreateDefault()
            .Get(WorkspaceProfileId.Train);

        Assert.True(train.IsAvailable);
        Assert.Collection(
            train.Composition.Panes.Panes,
            pane => AssertPane(pane, WorkspacePaneIds.TrainConfiguration, "Train Configuration", true),
            pane => AssertPane(pane, WorkspacePaneIds.TrainPreview, "Train Preview", false),
            pane => AssertPane(pane, WorkspacePaneIds.TrainSummary, "Train Summary", true));
        Assert.All(train.AvailablePanes, pane => Assert.True(train.IsPaneVisibleByDefault(pane)));
        Assert.True(typeof(UserControl).IsAssignableFrom(typeof(TrainConfigurationPaneControl)));
        Assert.True(typeof(UserControl).IsAssignableFrom(typeof(TrainPreviewPaneControl)));
        Assert.True(typeof(UserControl).IsAssignableFrom(typeof(TrainSummaryPaneControl)));
    }

    [Fact]
    public void DefaultTrainLayout_PlacesConfigurationPreviewAndSummaryDeterministically()
    {
        EditorDockingAdapter adapter = CreateAdapter(WorkspaceProfileId.Train);

        Assert.Equal(DockingLayoutIds.TrainRoot, adapter.Layout.Id);
        Assert.Equal(
            DockingLayoutIds.TrainConfigurationHost,
            adapter.GetPane(WorkspacePaneIds.TrainConfiguration).Owner?.Id);
        Assert.Equal(
            DockingLayoutIds.TrainPreviewHost,
            adapter.GetPane(WorkspacePaneIds.TrainPreview).Owner?.Id);
        Assert.Equal(
            DockingLayoutIds.TrainSummaryHost,
            adapter.GetPane(WorkspacePaneIds.TrainSummary).Owner?.Id);
        Assert.False(adapter.GetPane(WorkspacePaneIds.TrainPreview).CanClose);
        Assert.All(adapter.Registry.Panes, pane => Assert.True(adapter.IsPaneOpen(pane.Id)));
        Assert.False(Assert.IsType<ToolDock>(
            adapter.GetPane(WorkspacePaneIds.TrainConfiguration).Owner).AutoHide);
        Assert.True(Assert.IsType<ToolDock>(
            adapter.GetPane(WorkspacePaneIds.TrainConfiguration).Owner).IsExpanded);
        Assert.False(Assert.IsType<ToolDock>(
            adapter.GetPane(WorkspacePaneIds.TrainSummary).Owner).AutoHide);
        Assert.True(Assert.IsType<ToolDock>(
            adapter.GetPane(WorkspacePaneIds.TrainSummary).Owner).IsExpanded);
    }

    [Fact]
    public void EditorSession_AppliesAllFieldsThroughBackendValueTypes()
    {
        var session = new TrainConsistEditorSession();

        bool applied = session.TryApply(new TrainConsistInput(
            "6", "2.8", "2.4", "1.3", "1.15", "1.6"));

        Assert.True(applied, session.LastValidationMessage);
        Assert.Equal(1, session.Revision);
        Assert.Equal(6, session.CurrentDefinition.CarCount);
        Assert.Equal(2.8, session.CurrentDefinition.CarSpacing);
        Assert.Equal(2.4, session.CurrentDefinition.CarGeometry.Length);
        Assert.Equal(1.3, session.CurrentDefinition.CarGeometry.Width);
        Assert.Equal(1.15, session.CurrentDefinition.CarGeometry.Height);
        Assert.Equal(1.6, session.CurrentDefinition.BogieLayout.BogieSpacing);
        Assert.Null(session.CurrentDefinition.WheelLayout);
    }

    [Fact]
    public void EditorSession_InvalidInputPreservesLastValidImmutableDefinition()
    {
        var session = new TrainConsistEditorSession();
        Assert.True(session.TryApply(new TrainConsistInput(
            "5", "2.7", "2.3", "1.25", "1.05", "1.5")));
        TrainConsistDefinition lastValid = session.CurrentDefinition;
        int revision = session.Revision;

        bool applied = session.TryApply(new TrainConsistInput(
            "5", "2.7", "2.3", "1.25", "1.05", "2.4"));

        Assert.False(applied);
        Assert.Same(lastValid, session.CurrentDefinition);
        Assert.Equal(revision, session.Revision);
        Assert.False(session.LastAttemptSucceeded);
        Assert.Contains("less than or equal", session.LastValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("cars", "2.5", "2.2", "1.2", "1.1", "1.4", "whole number")]
    [InlineData("4", "distance", "2.2", "1.2", "1.1", "1.4", "number in metres")]
    [InlineData("0", "2.5", "2.2", "1.2", "1.1", "1.4", "greater than zero")]
    public void EditorSession_RejectsParsingAndBackendValidationCleanly(
        string carCount,
        string spacing,
        string length,
        string width,
        string height,
        string bogieSpacing,
        string expectedMessage)
    {
        var session = new TrainConsistEditorSession();
        TrainConsistDefinition initial = session.CurrentDefinition;

        bool applied = session.TryApply(new TrainConsistInput(
            carCount,
            spacing,
            length,
            width,
            height,
            bogieSpacing));

        Assert.False(applied);
        Assert.Same(initial, session.CurrentDefinition);
        Assert.Contains(expectedMessage, session.LastValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Presentation_ComputesReadoutsAndCenteredSchematicDeterministically()
    {
        var definition = new TrainConsistDefinition(
            carCount: 3,
            carSpacing: 2.5,
            carLength: 2.0,
            carWidth: 1.2,
            carHeight: 1.1,
            bogieSpacing: 1.4);

        TrainConsistPresentation presentation = TrainConsistPresentation.Create(definition);

        Assert.Equal(7.0, presentation.ApproximateTotalLength, precision: 10);
        Assert.Equal(0.5, presentation.InterCarGap, precision: 10);
        Assert.Equal(new[] { -2.5, 0.0, 2.5 }, presentation.Cars.Select(car => car.Center));
        Assert.Equal(-3.5, presentation.Cars[0].Start, precision: 10);
        Assert.Equal(-3.2, presentation.Cars[0].RearBogieCenter, precision: 10);
        Assert.Equal(-1.8, presentation.Cars[0].FrontBogieCenter, precision: 10);
    }

    [Fact]
    public void WorkspaceSelector_SwitchesTrackToTrainAndBackWithoutReplacingProfiles()
    {
        var manager = new WorkspaceProfileManager(WorkspaceProfileCatalog.CreateDefault());
        var selector = new WorkspaceSelectorModel(manager);
        WorkspaceProfile track = manager.CurrentProfile;

        Assert.True(selector.TryActivate(WorkspaceProfileId.Train));
        WorkspaceProfile train = manager.CurrentProfile;
        Assert.Equal(WorkspaceProfileId.Train, train.Id);
        Assert.True(selector.TryActivate(WorkspaceProfileId.Track));
        Assert.Same(track, manager.CurrentProfile);
        Assert.Same(train, manager.Catalog.Get(WorkspaceProfileId.Train));
    }

    [Fact]
    public void WorkspaceLayouts_PreserveIndependentInMemoryStateAcrossSwitches()
    {
        var manager = new WorkspaceProfileManager(WorkspaceProfileCatalog.CreateDefault());
        var adapters = new Dictionary<WorkspaceProfileId, EditorDockingAdapter>
        {
            [WorkspaceProfileId.Track] = CreateAdapter(WorkspaceProfileId.Track),
            [WorkspaceProfileId.Train] = CreateAdapter(WorkspaceProfileId.Train)
        };
        IDockable trackRouteHost = Assert.IsAssignableFrom<IDockable>(
            adapters[WorkspaceProfileId.Track].GetPane(WorkspacePaneIds.Route).Owner);
        trackRouteHost.Proportion = 0.29;
        manager.SwitchTo(WorkspaceProfileId.Train);
        IDockable trainConfigurationHost = Assert.IsAssignableFrom<IDockable>(
            adapters[manager.CurrentProfileId].GetPane(WorkspacePaneIds.TrainConfiguration).Owner);
        trainConfigurationHost.Proportion = 0.38;
        manager.SwitchTo(WorkspaceProfileId.Track);

        Assert.Equal(0.29, trackRouteHost.Proportion, precision: 10);
        Assert.Equal(0.38, trainConfigurationHost.Proportion, precision: 10);
        Assert.Equal(DockingLayoutIds.Root, adapters[manager.CurrentProfileId].Layout.Id);
    }

    [Fact]
    public void WorkspaceLayouts_PersistToSeparateFilesAndRestoreOnlyMatchingComposition()
    {
        WithTemporaryDirectory(directoryPath =>
        {
            string trackPath = Path.Combine(directoryPath, DockLayoutPersistenceService.LayoutFileName);
            string trainPath = Path.Combine(directoryPath, "train-docking-layout.json");
            var trackPersistence = new DockLayoutPersistenceService(trackPath);
            var trainPersistence = new DockLayoutPersistenceService(trainPath);
            EditorDockingAdapter track = CreateAdapter(WorkspaceProfileId.Track, trackPersistence);
            EditorDockingAdapter train = CreateAdapter(WorkspaceProfileId.Train, trainPersistence);
            Assert.IsAssignableFrom<IDockable>(
                track.GetPane(WorkspacePaneIds.Route).Owner).Proportion = 0.27;
            Assert.IsAssignableFrom<IDockable>(
                train.GetPane(WorkspacePaneIds.TrainConfiguration).Owner).Proportion = 0.37;

            Assert.True(track.TrySaveLayout(), trackPersistence.LastErrorMessage);
            Assert.True(train.TrySaveLayout(), trainPersistence.LastErrorMessage);

            Assert.NotEqual(trackPersistence.LayoutFilePath, trainPersistence.LayoutFilePath);
            Assert.True(File.Exists(trackPath));
            Assert.True(File.Exists(trainPath));
            EditorDockingAdapter restoredTrack = CreateAdapter(WorkspaceProfileId.Track, trackPersistence);
            EditorDockingAdapter restoredTrain = CreateAdapter(WorkspaceProfileId.Train, trainPersistence);
            Assert.Equal(DockingLayoutIds.Root, restoredTrack.Layout.Id);
            Assert.Equal(DockingLayoutIds.TrainRoot, restoredTrain.Layout.Id);
            Assert.Equal(
                0.27,
                Assert.IsAssignableFrom<IDockable>(
                    restoredTrack.GetPane(WorkspacePaneIds.Route).Owner).Proportion,
                precision: 10);
            Assert.Equal(
                0.37,
                Assert.IsAssignableFrom<IDockable>(
                    restoredTrain.GetPane(WorkspacePaneIds.TrainConfiguration).Owner).Proportion,
                precision: 10);
        });
    }

    [Fact]
    public void DefaultPersistencePaths_AreNamespacedByWorkspace()
    {
        string trackPath = DockLayoutPersistenceService.GetDefaultLayoutFilePath(
            WorkspaceProfileId.Track);
        string trainPath = DockLayoutPersistenceService.GetDefaultLayoutFilePath(
            WorkspaceProfileId.Train);

        Assert.EndsWith(DockLayoutPersistenceService.LayoutFileName, trackPath);
        Assert.EndsWith("train-docking-layout.json", trainPath);
        Assert.NotEqual(trackPath, trainPath);
    }

    private static EditorDockingAdapter CreateAdapter(
        WorkspaceProfileId id,
        DockLayoutPersistenceService? persistence = null)
    {
        WorkspaceComposition composition = WorkspaceProfileCatalog.CreateDefault()
            .GetComposition(id);
        IReadOnlyDictionary<string, object> contexts = composition.Panes.Panes.ToDictionary(
            pane => pane.Id,
            _ => (object)new object(),
            StringComparer.Ordinal);
        return new EditorDockingAdapter(composition, contexts, persistence);
    }

    private static void AssertPane(
        DockPaneRegistration pane,
        string id,
        string title,
        bool canClose)
    {
        Assert.Equal(id, pane.Id);
        Assert.Equal(title, pane.Title);
        Assert.Equal(canClose, pane.CanClose);
    }

    private static void WithTemporaryDirectory(Action<string> test)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "QuantumCoasterWorks.Tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            test(directoryPath);
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}
