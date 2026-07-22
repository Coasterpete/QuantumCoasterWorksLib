using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Quantum.Editor.Avalonia.Models;
using Quantum.Editor.Avalonia.Services.Viewport;
using Quantum.Math;

namespace Quantum.Editor.Avalonia.Controls;

internal readonly record struct TrackViewportCameraState(
    TrackViewportProjection Projection,
    Point WorldCenter,
    Vector Pan,
    double Scale,
    bool FitPending);

public sealed class TrackViewportControl : Control, IViewportSurface
{
    private static readonly Color[] SectionColors =
    {
        Color.Parse("#6CC4FF"),
        Color.Parse("#9EE493"),
        Color.Parse("#F9C74F"),
        Color.Parse("#F896D8"),
        Color.Parse("#B8A1FF"),
        Color.Parse("#FF8A65"),
        Color.Parse("#80CBC4")
    };

    private TrackViewportSnapshot snapshot = TrackViewportSnapshot.Empty;
    private EditorSelection? selection;
    private int stationCursorSampleIndex = -1;
    private int highlightedSectionIndex = -1;
    private int pointerSectionIndex = -1;
    private TrackViewportProjection projection = TrackViewportProjection.Isometric;
    private bool showFrames = true;
    private bool fitPending = true;
    private bool fitOnNextNonEmptySnapshot = true;
    private bool panning;
    private Point lastPointerPosition;
    private Point worldCenter;
    private Vector pan;
    private double scale = 1.0;

    public TrackViewportControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public event EventHandler<ViewportSampleSelectedEventArgs>? SampleSelected;

    public event EventHandler<SectionPointerChangedEventArgs>? SectionPointerChanged;

    string IViewportSurface.Name => "Track technical viewport";

    public TrackViewportSnapshot Snapshot
    {
        get => snapshot;
        set
        {
            TrackViewportSnapshot replacement = value ?? TrackViewportSnapshot.Empty;
            if (ReferenceEquals(snapshot, replacement))
            {
                return;
            }

            snapshot = replacement;
            if (stationCursorSampleIndex >= snapshot.Samples.Count)
            {
                stationCursorSampleIndex = -1;
            }

            if (snapshot.Samples.Count != 0 && fitOnNextNonEmptySnapshot)
            {
                fitPending = true;
                fitOnNextNonEmptySnapshot = false;
            }
            InvalidateVisual();
        }
    }

    public EditorSelection? Selection
    {
        get => selection;
        set
        {
            selection = value;
            InvalidateVisual();
        }
    }

    public int StationCursorSampleIndex
    {
        get => stationCursorSampleIndex;
        set
        {
            int replacement = value;
            if (replacement < 0 || replacement >= snapshot.Samples.Count)
            {
                replacement = -1;
            }

            if (stationCursorSampleIndex == replacement)
            {
                return;
            }

            stationCursorSampleIndex = replacement;
            InvalidateVisual();
        }
    }

    public int HighlightedSectionIndex
    {
        get => highlightedSectionIndex;
        set
        {
            int replacement = value;
            if (replacement < -1)
            {
                replacement = -1;
            }

            if (highlightedSectionIndex == replacement)
            {
                return;
            }

            highlightedSectionIndex = replacement;
            InvalidateVisual();
        }
    }

    public TrackViewportProjection Projection
    {
        get => projection;
        set
        {
            if (projection == value)
            {
                return;
            }

            projection = value;
            fitPending = true;
            fitOnNextNonEmptySnapshot = false;
            InvalidateVisual();
        }
    }

    public bool ShowFrames
    {
        get => showFrames;
        set
        {
            showFrames = value;
            InvalidateVisual();
        }
    }

    public void FitToTrack()
    {
        fitPending = true;
        fitOnNextNonEmptySnapshot = false;
        InvalidateVisual();
    }

    internal void BeginDocumentPresentation()
    {
        fitOnNextNonEmptySnapshot = true;
    }

    internal TrackViewportCameraState CaptureCameraState() =>
        new TrackViewportCameraState(
            projection,
            worldCenter,
            pan,
            scale,
            fitPending);

    internal void SetCameraState(
        Point replacementWorldCenter,
        Vector replacementPan,
        double replacementScale)
    {
        if (!double.IsFinite(replacementScale) || replacementScale <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(replacementScale));
        }

        worldCenter = replacementWorldCenter;
        pan = replacementPan;
        scale = replacementScale;
        fitPending = false;
        fitOnNextNonEmptySnapshot = false;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#11161D")), Bounds);
        DrawGrid(context);

        if (snapshot.Samples.Count == 0)
        {
            return;
        }

        if (fitPending)
        {
            CalculateFit();
        }

        DrawSelectedSectionHighlight(context);
        DrawCenterline(context);
        if (showFrames)
        {
            DrawFrames(context);
        }

        DrawStationCursor(context);
        DrawSelectedSample(context);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        Focus();

        PointerPoint point = eventArgs.GetCurrentPoint(this);
        lastPointerPosition = point.Position;
        if (point.Properties.IsMiddleButtonPressed || point.Properties.IsRightButtonPressed)
        {
            SetPointerSection(-1);
            panning = true;
            eventArgs.Pointer.Capture(this);
            eventArgs.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed && TryFindNearestSample(point.Position, out TrackViewportSample sample))
        {
            SampleSelected?.Invoke(this, new ViewportSampleSelectedEventArgs(sample));
            eventArgs.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (!panning)
        {
            Point pointer = eventArgs.GetPosition(this);
            SetPointerSection(
                TryFindNearestSample(pointer, out TrackViewportSample hoveredSample)
                    ? hoveredSample.SectionIndex
                    : -1);
            return;
        }

        Point position = eventArgs.GetPosition(this);
        pan += position - lastPointerPosition;
        lastPointerPosition = position;
        InvalidateVisual();
        eventArgs.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs eventArgs)
    {
        base.OnPointerExited(eventArgs);
        SetPointerSection(-1);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (!panning)
        {
            return;
        }

        panning = false;
        eventArgs.Pointer.Capture(null);
        eventArgs.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs eventArgs)
    {
        base.OnPointerWheelChanged(eventArgs);
        double zoomFactor = eventArgs.Delta.Y > 0.0 ? 1.15 : 1.0 / 1.15;
        Point pointer = eventArgs.GetPosition(this);
        Point center = Bounds.Center;
        Vector pointerFromCenter = pointer - center;

        pan = pointerFromCenter - ((pointerFromCenter - pan) * zoomFactor);
        scale = System.Math.Clamp(scale * zoomFactor, 0.01, 10000.0);
        fitPending = false;
        InvalidateVisual();
        eventArgs.Handled = true;
    }

    private void DrawGrid(DrawingContext context)
    {
        var minorPen = new Pen(new SolidColorBrush(Color.Parse("#1A2430")), 1.0);
        var majorPen = new Pen(new SolidColorBrush(Color.Parse("#253443")), 1.0);
        const double spacing = 32.0;
        double startX = ((pan.X % spacing) + spacing) % spacing;
        double startY = ((pan.Y % spacing) + spacing) % spacing;

        int column = 0;
        for (double x = startX; x < Bounds.Width; x += spacing, column++)
        {
            context.DrawLine(column % 4 == 0 ? majorPen : minorPen, new Point(x, 0.0), new Point(x, Bounds.Height));
        }

        int row = 0;
        for (double y = startY; y < Bounds.Height; y += spacing, row++)
        {
            context.DrawLine(row % 4 == 0 ? majorPen : minorPen, new Point(0.0, y), new Point(Bounds.Width, y));
        }
    }

    private void DrawCenterline(DrawingContext context)
    {
        IReadOnlyList<TrackViewportSample> samples = snapshot.Samples;
        for (int index = 1; index < samples.Count; index++)
        {
            TrackViewportSample previous = samples[index - 1];
            TrackViewportSample current = samples[index];
            Color color = SectionColors[System.Math.Max(0, current.SectionIndex) % SectionColors.Length];
            context.DrawLine(
                new Pen(new SolidColorBrush(color), 3.0, lineCap: PenLineCap.Round),
                ToScreen(previous.Position),
                ToScreen(current.Position));
        }
    }

    private void DrawSelectedSectionHighlight(DrawingContext context)
    {
        int sectionIndex = highlightedSectionIndex;
        if (sectionIndex < 0)
        {
            return;
        }

        var pen = new Pen(new SolidColorBrush(Color.Parse("#F6FAFF"), 0.38), 9.0, lineCap: PenLineCap.Round);
        IReadOnlyList<TrackViewportSample> samples = snapshot.Samples;
        for (int index = 1; index < samples.Count; index++)
        {
            if (samples[index].SectionIndex == sectionIndex)
            {
                context.DrawLine(pen, ToScreen(samples[index - 1].Position), ToScreen(samples[index].Position));
            }
        }
    }

    private void DrawFrames(DrawingContext context)
    {
        IReadOnlyList<TrackViewportSample> samples = snapshot.Samples;
        int stride = System.Math.Max(1, samples.Count / 36);
        double axisLength = System.Math.Clamp(snapshot.TotalLength * 0.018, 1.5, 5.0);
        var normalPen = new Pen(new SolidColorBrush(Color.Parse("#66E08A"), 0.86), 1.4);
        var binormalPen = new Pen(new SolidColorBrush(Color.Parse("#5BA7FF"), 0.86), 1.4);

        for (int index = 0; index < samples.Count; index += stride)
        {
            TrackViewportSample sample = samples[index];
            Point origin = ToScreen(sample.Position);
            context.DrawLine(normalPen, origin, ToScreen(sample.Position + (sample.Normal * axisLength)));
            context.DrawLine(binormalPen, origin, ToScreen(sample.Position + (sample.Binormal * axisLength)));
        }
    }

    private void DrawSelectedSample(DrawingContext context)
    {
        int sampleIndex = selection?.SampleIndex ?? -1;
        if (sampleIndex < 0 || sampleIndex >= snapshot.Samples.Count)
        {
            return;
        }

        Point point = ToScreen(snapshot.Samples[sampleIndex].Position);
        context.DrawEllipse(
            new SolidColorBrush(Color.Parse("#FFFFFF")),
            new Pen(new SolidColorBrush(Color.Parse("#0B1016")), 2.0),
            point,
            6.0,
            6.0);
    }

    private void DrawStationCursor(DrawingContext context)
    {
        if (stationCursorSampleIndex < 0 || stationCursorSampleIndex >= snapshot.Samples.Count)
        {
            return;
        }

        Point point = ToScreen(snapshot.Samples[stationCursorSampleIndex].Position);
        var cursorPen = new Pen(new SolidColorBrush(Color.Parse("#F4D35E")), 2.0);
        context.DrawEllipse(null, cursorPen, point, 8.0, 8.0);
        context.DrawLine(cursorPen, new Point(point.X - 12.0, point.Y), new Point(point.X + 12.0, point.Y));
        context.DrawLine(cursorPen, new Point(point.X, point.Y - 12.0), new Point(point.X, point.Y + 12.0));
    }

    private bool TryFindNearestSample(Point pointer, out TrackViewportSample sample)
    {
        const double hitRadiusSquared = 14.0 * 14.0;
        double bestDistanceSquared = hitRadiusSquared;
        int bestIndex = -1;

        for (int index = 0; index < snapshot.Samples.Count; index++)
        {
            Point point = ToScreen(snapshot.Samples[index].Position);
            Vector delta = pointer - point;
            double distanceSquared = (delta.X * delta.X) + (delta.Y * delta.Y);
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestIndex = index;
            }
        }

        if (bestIndex >= 0)
        {
            sample = snapshot.Samples[bestIndex];
            return true;
        }

        sample = default;
        return false;
    }

    private void SetPointerSection(int sectionIndex)
    {
        if (pointerSectionIndex == sectionIndex)
        {
            return;
        }

        pointerSectionIndex = sectionIndex;
        SectionPointerChanged?.Invoke(
            this,
            new SectionPointerChangedEventArgs(sectionIndex >= 0 ? sectionIndex : null));
    }

    private void CalculateFit()
    {
        IReadOnlyList<TrackViewportSample> samples = snapshot.Samples;
        if (samples.Count == 0 || Bounds.Width <= 1.0 || Bounds.Height <= 1.0)
        {
            return;
        }

        Point first = Project(samples[0].Position);
        double minX = first.X;
        double maxX = first.X;
        double minY = first.Y;
        double maxY = first.Y;
        for (int index = 1; index < samples.Count; index++)
        {
            Point point = Project(samples[index].Position);
            minX = System.Math.Min(minX, point.X);
            maxX = System.Math.Max(maxX, point.X);
            minY = System.Math.Min(minY, point.Y);
            maxY = System.Math.Max(maxY, point.Y);
        }

        worldCenter = new Point((minX + maxX) * 0.5, (minY + maxY) * 0.5);
        double width = System.Math.Max(maxX - minX, 1.0);
        double height = System.Math.Max(maxY - minY, 1.0);
        scale = System.Math.Min(
            System.Math.Max(1.0, Bounds.Width - 96.0) / width,
            System.Math.Max(1.0, Bounds.Height - 96.0) / height);
        pan = default;
        fitPending = false;
    }

    private Point ToScreen(Vector3d vector)
    {
        Point projected = Project(vector);
        Point center = Bounds.Center;
        return new Point(
            center.X + pan.X + ((projected.X - worldCenter.X) * scale),
            center.Y + pan.Y - ((projected.Y - worldCenter.Y) * scale));
    }

    private Point Project(Vector3d vector)
    {
        return projection switch
        {
            TrackViewportProjection.Top => new Point(vector.X, vector.Z),
            TrackViewportProjection.Side => new Point(vector.X, vector.Y),
            _ => new Point(
                vector.X - (vector.Z * 0.65),
                vector.Y + ((vector.X + vector.Z) * 0.18))
        };
    }
}
