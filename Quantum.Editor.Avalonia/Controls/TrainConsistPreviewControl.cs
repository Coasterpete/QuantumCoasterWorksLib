using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Quantum.Editor.Avalonia.Models;
using Quantum.Track;

namespace Quantum.Editor.Avalonia.Controls;

/// <summary>
/// Deterministic top-view schematic of a backend train consist definition.
/// </summary>
public sealed class TrainConsistPreviewControl : Control
{
    private TrainConsistDefinition? definition;

    public TrainConsistPreviewControl()
    {
        ClipToBounds = true;
        MinHeight = 180;
    }

    public TrainConsistDefinition? Definition
    {
        get => definition;
        set
        {
            definition = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brush("#0F151C"), Bounds);
        DrawGrid(context);
        if (definition is null || Bounds.Width <= 40.0 || Bounds.Height <= 40.0)
        {
            return;
        }

        TrainConsistPresentation presentation = TrainConsistPresentation.Create(definition);
        double padding = 34.0;
        double usableWidth = global::System.Math.Max(1.0, Bounds.Width - (padding * 2.0));
        double usableHeight = global::System.Math.Max(1.0, Bounds.Height - (padding * 2.0));
        double scale = global::System.Math.Min(
            usableWidth / presentation.ApproximateTotalLength,
            usableHeight / definition.CarWidth);
        double drawingWidth = presentation.ApproximateTotalLength * scale;
        double bodyHeight = definition.CarWidth * scale;
        double originX = (Bounds.Width - drawingWidth) * 0.5;
        double originY = (Bounds.Height - bodyHeight) * 0.5;
        double firstStart = presentation.Cars[0].Start;
        var bodyFill = Brush("#245477");
        var bodyOutline = new Pen(Brush("#7DD3FC"), 1.5);
        var bogiePen = new Pen(Brush("#F4D35E"), 2.0);

        foreach (TrainCarSchematic car in presentation.Cars)
        {
            double bodyX = originX + ((car.Start - firstStart) * scale);
            var body = new Rect(bodyX, originY, definition.CarLength * scale, bodyHeight);
            context.DrawRectangle(bodyFill, bodyOutline, body);
            DrawBogie(context, bogiePen, originX, originY, bodyHeight, firstStart, car.RearBogieCenter, scale);
            DrawBogie(context, bogiePen, originX, originY, bodyHeight, firstStart, car.FrontBogieCenter, scale);
        }

        double centerY = Bounds.Height * 0.5;
        context.DrawLine(
            new Pen(Brush("#90A4B8"), 1.0),
            new Point(padding * 0.5, centerY),
            new Point(Bounds.Width - (padding * 0.5), centerY));
    }

    private void DrawGrid(DrawingContext context)
    {
        var pen = new Pen(Brush("#1B2733"), 1.0);
        for (double x = 20.0; x < Bounds.Width; x += 20.0)
        {
            context.DrawLine(pen, new Point(x, 0.0), new Point(x, Bounds.Height));
        }

        for (double y = 20.0; y < Bounds.Height; y += 20.0)
        {
            context.DrawLine(pen, new Point(0.0, y), new Point(Bounds.Width, y));
        }
    }

    private static void DrawBogie(
        DrawingContext context,
        Pen pen,
        double originX,
        double originY,
        double bodyHeight,
        double firstStart,
        double bogieCenter,
        double scale)
    {
        double x = originX + ((bogieCenter - firstStart) * scale);
        context.DrawLine(
            pen,
            new Point(x, originY + 5.0),
            new Point(x, originY + bodyHeight - 5.0));
    }

    private static IBrush Brush(string value) =>
        new SolidColorBrush(Color.Parse(value));
}
