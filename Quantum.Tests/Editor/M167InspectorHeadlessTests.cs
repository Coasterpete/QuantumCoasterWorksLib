using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Quantum.Editor.Avalonia;
using Quantum.Editor.Avalonia.Controls;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services;
using Quantum.Editor.Avalonia.Services.Authoring;
using Quantum.Editor.Avalonia.Services.Commands;
using Quantum.Editor.Avalonia.Services.Documents;
using Quantum.IO.TrackLayout.V2;
using Quantum.Math;
using Quantum.Track.Authoring;

namespace Quantum.Tests.Editor;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AvaloniaHeadlessCollection : ICollectionFixture<AvaloniaHeadlessFixture>
{
    public const string Name = "Avalonia headless";
}

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class M167ViewportHeadlessTests
{
    private readonly HeadlessUnitTestSession session;

    public M167ViewportHeadlessTests(AvaloniaHeadlessFixture fixture)
    {
        session = fixture.Session;
    }

    [Fact]
    public async Task SnapshotReplacementPreservesProjectionCenterPanAndScale()
    {
        await session.Dispatch(
            () =>
            {
                var viewport = new TrackViewportControl
                {
                    Snapshot = Snapshot(10.0),
                    Projection = TrackViewportProjection.Side
                };
                viewport.SetCameraState(
                    new Point(12.0, -4.0),
                    new Vector(23.0, 17.0),
                    8.5);
                TrackViewportCameraState before = viewport.CaptureCameraState();

                viewport.Snapshot = Snapshot(18.0);

                TrackViewportCameraState after = viewport.CaptureCameraState();
                Assert.Equal(before, after);
                Assert.False(after.FitPending);
                return true;
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task ExplicitFitAndInitialDocumentPresentationRequestFit()
    {
        await session.Dispatch(
            () =>
            {
                var viewport = new TrackViewportControl { Snapshot = Snapshot(10.0) };
                viewport.SetCameraState(new Point(2.0, 3.0), new Vector(4.0, 5.0), 7.0);
                Assert.False(viewport.CaptureCameraState().FitPending);

                viewport.FitToTrack();
                Assert.True(viewport.CaptureCameraState().FitPending);

                viewport.SetCameraState(new Point(2.0, 3.0), new Vector(4.0, 5.0), 7.0);
                viewport.BeginDocumentPresentation();
                viewport.Snapshot = Snapshot(20.0);
                Assert.True(viewport.CaptureCameraState().FitPending);
                return true;
            },
            CancellationToken.None);
    }

    private static TrackViewportSnapshot Snapshot(double length)
    {
        return new TrackViewportSnapshot(
            new[]
            {
                new TrackViewportSample(
                    0,
                    0,
                    0.0,
                    Vector3d.Zero,
                    Vector3d.UnitX,
                    Vector3d.UnitY,
                    Vector3d.UnitZ,
                    0.0,
                    0.0),
                new TrackViewportSample(
                    1,
                    0,
                    length,
                    new Vector3d(length, 0.0, 0.0),
                    Vector3d.UnitX,
                    Vector3d.UnitY,
                    Vector3d.UnitZ,
                    0.0,
                    0.0)
            },
            length,
            0.0,
            0.0,
            Array.Empty<string>(),
            continuityReport: null);
    }
}

public sealed class AvaloniaHeadlessFixture : IDisposable
{
    internal HeadlessUnitTestSession Session { get; } =
        HeadlessUnitTestSession.StartNew(typeof(App));

    public void Dispose() => Session.Dispose();
}

[Collection(AvaloniaHeadlessCollection.Name)]
public sealed class M167InspectorHeadlessTests
{
    private readonly HeadlessUnitTestSession session;

    public M167InspectorHeadlessTests(AvaloniaHeadlessFixture fixture)
    {
        session = fixture.Session;
    }

    [Fact]
    public async Task PointerGestureUsesAbsoluteBaseCommitsNewestAndSurvivesPreviewRefresh()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost();
            StraightLengthScrubberControl scrubber = host.Scrubber;
            Point origin = CenterInWindow(scrubber, host.Window);

            host.Window.MouseDown(origin, MouseButton.Left, RawInputModifiers.None);
            Assert.True(host.Workspace.IsInteractiveEditActive);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.AcceptedPreviewLength == 30.0);

            host.Window.MouseMove(
                new Point(origin.X + 50.0, origin.Y),
                RawInputModifiers.None);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.AcceptedPreviewLength == 35.0);
            Assert.Same(scrubber, host.Inspector.LengthScrubber);
            Assert.False(host.Workspace.UndoRedo.CanUndo);

            host.Window.MouseMove(
                new Point(origin.X + 20.0, origin.Y),
                RawInputModifiers.None);
            Assert.Equal(32.0, host.Workspace.StraightLengthEdit!.RawLength, 9);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.AcceptedPreviewLength == 32.0);
            Assert.Equal(32.0, host.Workspace.StraightLengthEdit!.RawLength, 9);
            Assert.Equal("launch", host.Workspace.CurrentSelection!.NodeId);
            Assert.False(host.Workspace.UndoRedo.CanUndo);

            host.Window.MouseUp(
                new Point(origin.X + 20.0, origin.Y),
                MouseButton.Left,
                RawInputModifiers.None);
            await WaitUntilAsync(() => !host.Workspace.IsInteractiveEditActive);

            Assert.Equal(32.0, StraightLength(host.Document), 9);
            Assert.True(host.Workspace.UndoRedo.CanUndo);
            Assert.Equal(2, host.Workspace.CaptureStraightLengthMetrics().RawPointerUpdates);
            Assert.True(host.Workspace.UndoLast());
            Assert.Equal(30.0, StraightLength(host.Document), 9);
        });
    }

    [Fact]
    public async Task ShiftUsesFineSensitivityFromOriginalAuthoredLength()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost();
            Point origin = CenterInWindow(host.Scrubber, host.Window);
            host.Window.MouseDown(origin, MouseButton.Left, RawInputModifiers.None);

            host.Window.MouseMove(
                new Point(origin.X + 50.0, origin.Y),
                RawInputModifiers.Shift);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.AcceptedPreviewLength == 30.5);

            Assert.Equal(30.5, host.Workspace.StraightLengthEdit!.RawLength, 9);
            host.Scrubber.Cancel();
            await WaitUntilAsync(() => !host.Workspace.IsInteractiveEditActive);
        });
    }

    [Fact]
    public async Task EscapeAndPointerCaptureLossCancelWithoutHistory()
    {
        await DispatchAsync(async () =>
        {
            using Host escapeHost = CreateHost();
            Point escapeOrigin = CenterInWindow(escapeHost.Scrubber, escapeHost.Window);
            escapeHost.Window.MouseDown(
                escapeOrigin,
                MouseButton.Left,
                RawInputModifiers.None);
            escapeHost.Window.MouseMove(
                new Point(escapeOrigin.X + 30.0, escapeOrigin.Y),
                RawInputModifiers.None);
            escapeHost.Window.KeyPress(
                Key.Escape,
                RawInputModifiers.None,
                PhysicalKey.Escape,
                null);
            await WaitUntilAsync(() => !escapeHost.Workspace.IsInteractiveEditActive);
            Assert.Equal(30.0, StraightLength(escapeHost.Document), 9);
            Assert.False(escapeHost.Workspace.UndoRedo.CanUndo);

            using Host captureHost = CreateHost();
            Point captureOrigin = CenterInWindow(captureHost.Scrubber, captureHost.Window);
            captureHost.Window.MouseDown(
                captureOrigin,
                MouseButton.Left,
                RawInputModifiers.None);
            captureHost.Scrubber.ReleasePointerCapture();
            await WaitUntilAsync(() => !captureHost.Workspace.IsInteractiveEditActive);
            Assert.Equal(30.0, StraightLength(captureHost.Document), 9);
            Assert.False(captureHost.Workspace.UndoRedo.CanUndo);
        });
    }

    [Fact]
    public async Task InvalidRawLengthShowsStatusAndReleaseRestoresCommittedPresentation()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost();
            Point origin = CenterInWindow(host.Scrubber, host.Window);
            host.Window.MouseDown(origin, MouseButton.Left, RawInputModifiers.None);
            host.Window.MouseMove(
                new Point(origin.X - 400.0, origin.Y),
                RawInputModifiers.None);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.Status == StraightLengthEditStatus.Invalid);

            Assert.Equal(-10.0, host.Workspace.StraightLengthEdit!.RawLength, 9);
            Assert.Contains("Last valid preview", host.Inspector.LengthScrubStatusText);
            Assert.NotEmpty(host.Workspace.StraightLengthEdit.Diagnostics);
            Assert.Equal(30.0, host.Workspace.ViewportSnapshot.TotalLength, 9);

            host.Window.MouseUp(
                new Point(origin.X - 400.0, origin.Y),
                MouseButton.Left,
                RawInputModifiers.None);
            await WaitUntilAsync(() => !host.Workspace.IsInteractiveEditActive);

            Assert.Equal(30.0, StraightLength(host.Document), 9);
            Assert.Equal(30.0, host.Workspace.ViewportSnapshot.TotalLength, 9);
            Assert.False(host.Workspace.UndoRedo.CanUndo);
        });
    }

    [Fact]
    public async Task NewestPreviewWinsAndSourceCommandsAreDisabledDuringGesture()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost();
            Point origin = CenterInWindow(host.Scrubber, host.Window);
            host.Window.MouseDown(origin, MouseButton.Left, RawInputModifiers.None);
            for (int delta = 5; delta <= 80; delta += 5)
            {
                host.Window.MouseMove(
                    new Point(origin.X + delta, origin.Y),
                    RawInputModifiers.None);
            }

            Assert.False(host.Workspace.Commands.CanExecute(EditorCommandIds.SaveDocument));
            Assert.False(host.Workspace.Commands.CanExecute(EditorCommandIds.Undo));
            Assert.False(host.Workspace.Commands.CanExecute(EditorCommandIds.Redo));
            Assert.False(host.Workspace.Commands.CanExecute(EditorCommandIds.NewDocument));
            Assert.Equal("launch", host.Workspace.CurrentSelection!.NodeId);
            host.Window.MouseUp(
                new Point(origin.X + 80.0, origin.Y),
                MouseButton.Left,
                RawInputModifiers.None);
            await WaitUntilAsync(() => !host.Workspace.IsInteractiveEditActive);
            Assert.Equal(38.0, host.Workspace.ViewportSnapshot.TotalLength, 9);
            TrackViewportSnapshot committedSnapshot = host.Workspace.ViewportSnapshot;
            await Task.Delay(100);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(committedSnapshot, host.Workspace.ViewportSnapshot);
            Assert.Equal(38.0, StraightLength(host.Document), 9);
            Assert.True(host.Workspace.UndoRedo.CanUndo);
        });
    }

    [Fact]
    public async Task RouteSourceMutationAndSelectionButtonsDisableDuringGesturePolicy()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost();
            Point origin = CenterInWindow(host.Scrubber, host.Window);
            host.Window.MouseDown(origin, MouseButton.Left, RawInputModifiers.None);

            var route = new RoutePaneControl
            {
                GraphNodes = host.Workspace.GraphNodes,
                Selection = host.Workspace.CurrentSelection,
                SourceEditingEnabled = false
            };
            var routeWindow = new Window { Width = 400, Height = 600, Content = route };
            try
            {
                routeWindow.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.All(
                    route.GetVisualDescendants().OfType<Button>(),
                    button => Assert.False(button.IsEnabled));
            }
            finally
            {
                routeWindow.Close();
            }

            host.Scrubber.Cancel();
            await WaitUntilAsync(() => !host.Workspace.IsInteractiveEditActive);
        });
    }

    [Fact]
    public async Task KeyboardArrowsUseNormalFineAndCoarseStepsThenEnterCommitsExactNewest()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost();
            Assert.True(host.Scrubber.Focus());
            Assert.True(host.Scrubber.IsKeyboardFocusWithin);

            PressKey(host, Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.AcceptedPreviewLength == 30.1);
            PressKey(host, Key.Right, RawInputModifiers.Shift, PhysicalKey.ArrowRight);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.AcceptedPreviewLength == 30.11);
            PressKey(host, Key.Right, RawInputModifiers.Control, PhysicalKey.ArrowRight);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.AcceptedPreviewLength == 31.11);

            Assert.Equal(31.11, host.Workspace.StraightLengthEdit!.RawLength, 9);
            Assert.True(host.Scrubber.IsKeyboardFocusWithin);
            PressKey(host, Key.Enter, RawInputModifiers.None, PhysicalKey.Enter);
            Assert.False(host.Scrubber.IsEnabled);
            await WaitUntilAsync(() => !host.Workspace.IsInteractiveEditActive);

            Assert.Equal(31.11, StraightLength(host.Document), 9);
            Assert.True(host.Workspace.UndoRedo.CanUndo);
            await WaitUntilAsync(() =>
                host.Inspector.LengthScrubber?.IsKeyboardFocusWithin == true);
        });
    }

    [Fact]
    public async Task KeyboardEscapeCancelsAndRestoresFocusWithoutHistory()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost();
            Assert.True(host.Scrubber.Focus());
            PressKey(host, Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.AcceptedPreviewLength == 29.9);
            PressKey(host, Key.Escape, RawInputModifiers.None, PhysicalKey.Escape);
            await WaitUntilAsync(() => !host.Workspace.IsInteractiveEditActive);

            Assert.Equal(30.0, StraightLength(host.Document), 9);
            Assert.False(host.Workspace.UndoRedo.CanUndo);
            await WaitUntilAsync(() =>
                host.Inspector.LengthScrubber?.IsKeyboardFocusWithin == true);
        });
    }

    [Fact]
    public async Task ConfiguredPointerSensitivityReplacesDefaults()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost(new StraightLengthScrubSensitivity(
                normalMetersPerStep: 0.2,
                fineMetersPerStep: 0.02,
                coarseMetersPerStep: 2.0));
            Point origin = CenterInWindow(host.Scrubber, host.Window);
            host.Window.MouseDown(origin, MouseButton.Left, RawInputModifiers.None);
            host.Window.MouseMove(
                new Point(origin.X + 10.0, origin.Y),
                RawInputModifiers.None);
            await WaitUntilAsync(() =>
                host.Workspace.StraightLengthEdit?.AcceptedPreviewLength == 32.0);
            host.Scrubber.Cancel();
            await WaitUntilAsync(() => !host.Workspace.IsInteractiveEditActive);
        });
    }

    [Fact]
    public async Task ScrubberExposesAccessibleNameHelpAndDisabledState()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost();
            Assert.Equal(
                "Straight section length live editor",
                AutomationProperties.GetName(host.Scrubber));
            Assert.Contains("Enter commits", AutomationProperties.GetHelpText(host.Scrubber));
            Assert.True(host.Scrubber.IsEnabled);

            Assert.True(host.Workspace.BeginStraightLengthEdit("launch"));
            host.Inspector.Refresh(host.Workspace);
            Assert.False(host.Inspector.LengthScrubber!.IsEnabled);
            host.Workspace.CancelStraightLengthEdit();
            await WaitUntilAsync(() => !host.Workspace.IsInteractiveEditActive);
        });
    }

    [Fact]
    public async Task TypedApplyStillUsesExistingOneShotPath()
    {
        await DispatchAsync(async () =>
        {
            using Host host = CreateHost();
            NumericUpDown length = host.Inspector.GetVisualDescendants()
                .OfType<NumericUpDown>()
                .Single(field => string.Equals(
                    field.Tag as string,
                    "sectionLength",
                    StringComparison.Ordinal));
            Button apply = host.Inspector.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => string.Equals(
                    button.Content as string,
                    "Apply section",
                    StringComparison.Ordinal));
            length.Value = 33.0m;
            length.Text = "33";

            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
            await WaitUntilAsync(() => StraightLength(host.Document) == 33.0);

            Assert.Equal(33.0, StraightLength(host.Document), 9);
            Assert.False(host.Workspace.IsInteractiveEditActive);
            Assert.True(host.Workspace.UndoRedo.CanUndo);
        });
    }

    private Task DispatchAsync(Func<Task> action)
    {
        return session.Dispatch(
            async () =>
            {
                await action();
                return true;
            },
            CancellationToken.None);
    }

    private static Host CreateHost(StraightLengthScrubSensitivity? sensitivity = null)
    {
        var workspace = new EditorWorkspace(
            straightLengthScrubSensitivity: sensitivity);
        TrackEditorDocument document = CreateDocument();
        workspace.Documents.SetActiveDocument(document);
        EditorGraphNode launch = workspace.GraphNodes.Single(node => node.NodeId == "launch");
        workspace.Select(launch.Selection);
        var inspector = new InspectorPaneControl();
        inspector.Refresh(workspace);
        workspace.WorkspaceChanged += (_, _) => inspector.Refresh(workspace);
        var window = new Window
        {
            Width = 440,
            Height = 760,
            Content = inspector
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Host(
            workspace,
            document,
            inspector,
            window,
            Assert.IsType<StraightLengthScrubberControl>(inspector.LengthScrubber));
    }

    private static TrackEditorDocument CreateDocument()
    {
        return TrackEditorDocument.Create(
            new TrackLayoutPackageV2Dto
            {
                Metadata = new TrackLayoutMetadataV2Dto
                {
                    Units = "meters",
                    SourceName = "M167.4 headless fixture",
                    LayoutId = "m167-4-headless"
                },
                Sections = new[]
                {
                    new TrackLayoutSectionV2Dto
                    {
                        Kind = TrackLayoutPackageV2Vocabulary.StraightSectionKind,
                        Id = "launch",
                        Length = 30.0
                    }
                }
            },
            "M167.4 headless fixture");
    }

    private static Point CenterInWindow(Control control, Window window)
    {
        var localCenter = new Point(control.Bounds.Width * 0.5, control.Bounds.Height * 0.5);
        return control.TranslatePoint(localCenter, window) ??
            throw new InvalidOperationException("The scrubber is not attached to the test window.");
    }

    private static void PressKey(
        Host host,
        Key key,
        RawInputModifiers modifiers,
        PhysicalKey physicalKey)
    {
        host.Window.KeyPress(key, modifiers, physicalKey, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Timed out waiting for the headless UI condition.");
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }
    }

    private static double StraightLength(TrackEditorDocument document)
    {
        return Assert.IsType<StraightSectionDefinition>(
            document.Graph!.Nodes.Single(node => node.Id == "launch").Section).Length;
    }

    private sealed class Host : IDisposable
    {
        internal Host(
            EditorWorkspace workspace,
            TrackEditorDocument document,
            InspectorPaneControl inspector,
            Window window,
            StraightLengthScrubberControl scrubber)
        {
            Workspace = workspace;
            Document = document;
            Inspector = inspector;
            Window = window;
            Scrubber = scrubber;
        }

        internal EditorWorkspace Workspace { get; }

        internal TrackEditorDocument Document { get; }

        internal InspectorPaneControl Inspector { get; }

        internal Window Window { get; }

        internal StraightLengthScrubberControl Scrubber { get; }

        public void Dispose()
        {
            Window.Close();
            Workspace.Dispose();
        }
    }
}
