using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Quantum.Editor.Avalonia.Services.Plots;
using Quantum.Track.Authoring;

namespace Quantum.Editor.Avalonia.Controls;

public sealed class EngineeringPlotWorkspaceControl : Control
{
    private const double LeftMargin = 104.0;
    private const double RightMargin = 78.0;
    private const double TopMargin = 8.0;
    private const double AxisHeight = 30.0;
    private const double PlotSpacing = 5.0;
    private static readonly Typeface PlotTypeface = new(FontFamily.Default);
    private static readonly EngineeringPlotKind[] PlotOrder =
    {
        EngineeringPlotKind.Elevation,
        EngineeringPlotKind.Curvature,
        EngineeringPlotKind.Roll,
        EngineeringPlotKind.Pitch,
        EngineeringPlotKind.Yaw
    };

    private EngineeringSnapshot? snapshot;
    private EngineeringPlotKind enabledPlots = EngineeringPlotKind.All;
    private int cursorSampleIndex = -1;

    public EngineeringPlotWorkspaceControl()
    {
        ClipToBounds = true;
    }

    public event EventHandler<EngineeringStationChangedEventArgs>? StationChanged;

    public EngineeringSnapshot? Snapshot
    {
        get => snapshot;
        set
        {
            if (ReferenceEquals(snapshot, value))
            {
                return;
            }

            snapshot = value;
            if (cursorSampleIndex >= (snapshot?.SampleCount ?? 0))
            {
                cursorSampleIndex = -1;
            }

            InvalidateVisual();
        }
    }

    public EngineeringPlotKind EnabledPlots => enabledPlots;

    public int CursorSampleIndex
    {
        get => cursorSampleIndex;
        set
        {
            int replacement = value;
            if (replacement < 0 || replacement >= (snapshot?.SampleCount ?? 0))
            {
                replacement = -1;
            }

            if (cursorSampleIndex == replacement)
            {
                return;
            }

            cursorSampleIndex = replacement;
            InvalidateVisual();
        }
    }

    public void SetPlotEnabled(EngineeringPlotKind plot, bool isEnabled)
    {
        if (!PlotOrder.Contains(plot))
        {
            throw new ArgumentOutOfRangeException(nameof(plot), plot, "A single engineering plot is required.");
        }

        EngineeringPlotKind replacement = isEnabled
            ? enabledPlots | plot
            : enabledPlots & ~plot;
        if (replacement == enabledPlots)
        {
            return;
        }

        enabledPlots = replacement;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brush("#0F151C"), Bounds);

        EngineeringSnapshot? currentSnapshot = snapshot;
        EngineeringPlotKind[] visiblePlots = PlotOrder
            .Where(plot => enabledPlots.HasFlag(plot))
            .ToArray();
        if (currentSnapshot is null || currentSnapshot.SampleCount == 0)
        {
            DrawText(context, "No engineering snapshot", new Point(16.0, 14.0), "#7F94A8", 12.0);
            return;
        }

        if (visiblePlots.Length == 0)
        {
            DrawText(context, "Enable at least one plot", new Point(16.0, 14.0), "#7F94A8", 12.0);
            return;
        }

        Rect stationBounds = GetStationBounds();
        if (stationBounds.Width <= 1.0 || stationBounds.Height <= 1.0)
        {
            return;
        }

        double plotHeight = (stationBounds.Height - (PlotSpacing * (visiblePlots.Length - 1))) /
            visiblePlots.Length;
        if (plotHeight <= 1.0)
        {
            return;
        }

        for (int plotIndex = 0; plotIndex < visiblePlots.Length; plotIndex++)
        {
            double top = stationBounds.Top + (plotIndex * (plotHeight + PlotSpacing));
            DrawPlot(
                context,
                currentSnapshot,
                visiblePlots[plotIndex],
                new Rect(stationBounds.Left, top, stationBounds.Width, plotHeight));
        }

        DrawStationAxis(context, currentSnapshot, stationBounds);
        DrawSynchronizedCursor(context, currentSnapshot, stationBounds);
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        EngineeringSnapshot? currentSnapshot = snapshot;
        if (currentSnapshot is null || enabledPlots == EngineeringPlotKind.None)
        {
            return;
        }

        Rect stationBounds = GetStationBounds();
        Point pointer = eventArgs.GetPosition(this);
        if (!stationBounds.Contains(pointer) || stationBounds.Width <= 0.0)
        {
            return;
        }

        double fraction = System.Math.Clamp(
            (pointer.X - stationBounds.Left) / stationBounds.Width,
            0.0,
            1.0);
        double requestedStation = currentSnapshot.TotalLength * fraction;
        int sampleIndex = EngineeringPlotProjection.FindNearestSampleIndex(
            currentSnapshot,
            requestedStation);
        if (sampleIndex < 0 || sampleIndex == cursorSampleIndex)
        {
            return;
        }

        cursorSampleIndex = sampleIndex;
        InvalidateVisual();
        StationChanged?.Invoke(
            this,
            new EngineeringStationChangedEventArgs(
                sampleIndex,
                currentSnapshot.StationGrid[sampleIndex]));
        eventArgs.Handled = true;
    }

    private void DrawPlot(
        DrawingContext context,
        EngineeringSnapshot currentSnapshot,
        EngineeringPlotKind plot,
        Rect plotBounds)
    {
        context.FillRectangle(Brush("#121B24"), plotBounds);
        var borderPen = Pen("#2A3948", 1.0);
        context.DrawRectangle(null, borderPen, plotBounds);

        (double minimum, double maximum) = FindRange(currentSnapshot, plot);
        DrawPlotGrid(context, plotBounds, minimum, maximum);
        DrawSectionBoundaries(context, currentSnapshot, plotBounds);
        DrawSeries(context, currentSnapshot, plot, plotBounds, minimum, maximum);

        (string title, string unit, string color) = Describe(plot);
        DrawText(context, title, new Point(10.0, plotBounds.Top + 4.0), color, 11.0);
        DrawText(context, unit, new Point(10.0, plotBounds.Top + 19.0), "#758A9E", 10.0);
        DrawText(
            context,
            maximum.ToString(ValueFormat(plot), CultureInfo.InvariantCulture),
            new Point(plotBounds.Right + 7.0, plotBounds.Top - 1.0),
            "#71869A",
            9.0);
        DrawText(
            context,
            minimum.ToString(ValueFormat(plot), CultureInfo.InvariantCulture),
            new Point(plotBounds.Right + 7.0, plotBounds.Bottom - 12.0),
            "#71869A",
            9.0);

        if (cursorSampleIndex >= 0)
        {
            double? value = EngineeringPlotProjection.GetValue(
                currentSnapshot,
                plot,
                cursorSampleIndex);
            if (value.HasValue)
            {
                string readout = value.Value.ToString(ValueFormat(plot), CultureInfo.InvariantCulture);
                FormattedText text = CreateText(readout, color, 10.0);
                context.DrawText(
                    text,
                    new Point(
                        System.Math.Max(4.0, plotBounds.Left - text.Width - 8.0),
                        plotBounds.Bottom - 15.0));
            }
        }
    }

    private static void DrawPlotGrid(
        DrawingContext context,
        Rect plotBounds,
        double minimum,
        double maximum)
    {
        var gridPen = Pen("#22303D", 1.0);
        for (int division = 1; division < 4; division++)
        {
            double x = plotBounds.Left + (plotBounds.Width * division / 4.0);
            double y = plotBounds.Top + (plotBounds.Height * division / 4.0);
            context.DrawLine(gridPen, new Point(x, plotBounds.Top), new Point(x, plotBounds.Bottom));
            context.DrawLine(gridPen, new Point(plotBounds.Left, y), new Point(plotBounds.Right, y));
        }

        if (minimum < 0.0 && maximum > 0.0)
        {
            double zeroY = MapValue(0.0, minimum, maximum, plotBounds);
            context.DrawLine(
                Pen("#405469", 1.0),
                new Point(plotBounds.Left, zeroY),
                new Point(plotBounds.Right, zeroY));
        }
    }

    private static void DrawSectionBoundaries(
        DrawingContext context,
        EngineeringSnapshot currentSnapshot,
        Rect plotBounds)
    {
        var boundaryPen = new Pen(
            Brush("#9A7EDB", 0.62),
            1.0,
            DashStyle.Dash);
        foreach (EngineeringSectionBoundaryMetadata boundary in currentSnapshot.SectionBoundaries)
        {
            double x = MapStation(boundary.Station, currentSnapshot.TotalLength, plotBounds);
            context.DrawLine(
                boundaryPen,
                new Point(x, plotBounds.Top),
                new Point(x, plotBounds.Bottom));
        }
    }

    private static void DrawSeries(
        DrawingContext context,
        EngineeringSnapshot currentSnapshot,
        EngineeringPlotKind plot,
        Rect plotBounds,
        double minimum,
        double maximum)
    {
        (_, _, string color) = Describe(plot);
        var seriesPen = Pen(color, 1.8);
        Point? previous = null;
        for (int sampleIndex = 0; sampleIndex < currentSnapshot.SampleCount; sampleIndex++)
        {
            double? value = EngineeringPlotProjection.GetValue(
                currentSnapshot,
                plot,
                sampleIndex);
            if (!value.HasValue || !double.IsFinite(value.Value))
            {
                previous = null;
                continue;
            }

            var current = new Point(
                MapStation(
                    currentSnapshot.StationGrid[sampleIndex],
                    currentSnapshot.TotalLength,
                    plotBounds),
                MapValue(value.Value, minimum, maximum, plotBounds));
            if (previous.HasValue)
            {
                context.DrawLine(seriesPen, previous.Value, current);
            }

            previous = current;
        }
    }

    private static void DrawStationAxis(
        DrawingContext context,
        EngineeringSnapshot currentSnapshot,
        Rect stationBounds)
    {
        var axisPen = Pen("#405469", 1.0);
        double axisY = stationBounds.Bottom + 4.0;
        context.DrawLine(
            axisPen,
            new Point(stationBounds.Left, axisY),
            new Point(stationBounds.Right, axisY));

        const int tickCount = 5;
        for (int tickIndex = 0; tickIndex < tickCount; tickIndex++)
        {
            double fraction = tickIndex / (double)(tickCount - 1);
            double x = stationBounds.Left + (stationBounds.Width * fraction);
            context.DrawLine(axisPen, new Point(x, axisY), new Point(x, axisY + 4.0));

            string label = (currentSnapshot.TotalLength * fraction)
                .ToString("F1", CultureInfo.InvariantCulture);
            FormattedText text = CreateText(label, "#8195A8", 9.0);
            context.DrawText(
                text,
                new Point(x - (text.Width * 0.5), axisY + 5.0));
        }

        FormattedText axisLabel = CreateText("station (m)", "#9CB0C2", 10.0);
        context.DrawText(
            axisLabel,
            new Point(
                stationBounds.Left + ((stationBounds.Width - axisLabel.Width) * 0.5),
                axisY + 17.0));
    }

    private void DrawSynchronizedCursor(
        DrawingContext context,
        EngineeringSnapshot currentSnapshot,
        Rect stationBounds)
    {
        if (cursorSampleIndex < 0 || cursorSampleIndex >= currentSnapshot.SampleCount)
        {
            return;
        }

        double x = MapStation(
            currentSnapshot.StationGrid[cursorSampleIndex],
            currentSnapshot.TotalLength,
            stationBounds);
        var cursorPen = Pen("#F4D35E", 1.4);
        context.DrawLine(
            cursorPen,
            new Point(x, stationBounds.Top),
            new Point(x, stationBounds.Bottom));
    }

    private Rect GetStationBounds()
    {
        return new Rect(
            LeftMargin,
            TopMargin,
            System.Math.Max(0.0, Bounds.Width - LeftMargin - RightMargin),
            System.Math.Max(0.0, Bounds.Height - TopMargin - AxisHeight));
    }

    private static (double Minimum, double Maximum) FindRange(
        EngineeringSnapshot currentSnapshot,
        EngineeringPlotKind plot)
    {
        double minimum = double.PositiveInfinity;
        double maximum = double.NegativeInfinity;
        for (int sampleIndex = 0; sampleIndex < currentSnapshot.SampleCount; sampleIndex++)
        {
            double? value = EngineeringPlotProjection.GetValue(
                currentSnapshot,
                plot,
                sampleIndex);
            if (!value.HasValue || !double.IsFinite(value.Value))
            {
                continue;
            }

            minimum = System.Math.Min(minimum, value.Value);
            maximum = System.Math.Max(maximum, value.Value);
        }

        if (!double.IsFinite(minimum) || !double.IsFinite(maximum))
        {
            return (-1.0, 1.0);
        }

        double span = maximum - minimum;
        if (span <= 1e-12)
        {
            double padding = System.Math.Max(1.0, System.Math.Abs(maximum) * 0.05);
            return (minimum - padding, maximum + padding);
        }

        double rangePadding = span * 0.08;
        return (minimum - rangePadding, maximum + rangePadding);
    }

    private static double MapStation(double station, double totalLength, Rect plotBounds)
    {
        if (totalLength <= 0.0)
        {
            return plotBounds.Left;
        }

        return plotBounds.Left + (plotBounds.Width * station / totalLength);
    }

    private static double MapValue(
        double value,
        double minimum,
        double maximum,
        Rect plotBounds)
    {
        return plotBounds.Bottom -
            (plotBounds.Height * (value - minimum) / (maximum - minimum));
    }

    private static (string Title, string Unit, string Color) Describe(EngineeringPlotKind plot)
    {
        return plot switch
        {
            EngineeringPlotKind.Elevation => ("ELEVATION", "m", "#66C7F2"),
            EngineeringPlotKind.Curvature => ("CURVATURE", "1/m", "#8DDB8A"),
            EngineeringPlotKind.Roll => ("ROLL", "deg", "#F4C95D"),
            EngineeringPlotKind.Pitch => ("PITCH", "deg", "#F28C8C"),
            EngineeringPlotKind.Yaw => ("YAW", "deg", "#C7A6FF"),
            _ => throw new ArgumentOutOfRangeException(nameof(plot), plot, null)
        };
    }

    private static string ValueFormat(EngineeringPlotKind plot)
    {
        return plot == EngineeringPlotKind.Curvature ? "F5" : "F2";
    }

    private static Pen Pen(string color, double thickness)
    {
        return new Pen(Brush(color), thickness);
    }

    private static SolidColorBrush Brush(string color, double opacity = 1.0)
    {
        return new SolidColorBrush(Color.Parse(color), opacity);
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        Point origin,
        string color,
        double fontSize)
    {
        context.DrawText(CreateText(text, color, fontSize), origin);
    }

    private static FormattedText CreateText(
        string text,
        string color,
        double fontSize)
    {
        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            PlotTypeface,
            fontSize,
            Brush(color));
    }
}
