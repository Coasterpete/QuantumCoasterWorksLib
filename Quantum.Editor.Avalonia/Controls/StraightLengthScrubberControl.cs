using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Quantum.Editor.Avalonia.Controls;

internal sealed class StraightLengthScrubDeltaEventArgs : EventArgs
{
    internal StraightLengthScrubDeltaEventArgs(double totalHorizontalDelta, KeyModifiers modifiers)
    {
        TotalHorizontalDelta = totalHorizontalDelta;
        Modifiers = modifiers;
    }

    internal double TotalHorizontalDelta { get; }

    internal KeyModifiers Modifiers { get; }
}

/// <summary>
/// Avalonia-only pointer-capture surface for the first live authoring gesture.
/// It reports total displacement; absolute-value derivation remains with the Inspector.
/// </summary>
internal sealed class StraightLengthScrubberControl : Border
{
    private Point startPosition;
    private IPointer? capturedPointer;
    private bool releasingCapture;

    internal StraightLengthScrubberControl()
    {
        Focusable = true;
        Width = 34;
        MinHeight = 28;
        Padding = new Thickness(7, 3);
        CornerRadius = new CornerRadius(4);
        Background = Brush.Parse("#203140");
        BorderBrush = Brush.Parse("#45657D");
        BorderThickness = new Thickness(1);
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
        ToolTip.SetTip(this, "Drag horizontally to preview Length. Shift: fine, Ctrl: coarse.");
        Child = new TextBlock
        {
            Text = "↔",
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brush.Parse("#B9D8ED"),
            FontWeight = FontWeight.Bold
        };
    }

    internal event EventHandler? ScrubStarted;

    internal event EventHandler<StraightLengthScrubDeltaEventArgs>? ScrubDelta;

    internal event EventHandler? CommitRequested;

    internal event EventHandler? CancelRequested;

    internal bool IsScrubbing { get; private set; }

    internal bool IsInvalid
    {
        set
        {
            BorderBrush = Brush.Parse(value ? "#FF6B6B" : "#45657D");
            Background = Brush.Parse(value ? "#48252A" : "#203140");
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        PointerPoint point = eventArgs.GetCurrentPoint(this);
        if (IsScrubbing || !point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        IsScrubbing = true;
        startPosition = point.Position;
        capturedPointer = eventArgs.Pointer;
        Focus();
        eventArgs.Pointer.Capture(this);
        ScrubStarted?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (!IsScrubbing)
        {
            return;
        }

        Point position = eventArgs.GetPosition(this);
        ScrubDelta?.Invoke(
            this,
            new StraightLengthScrubDeltaEventArgs(
                position.X - startPosition.X,
                eventArgs.KeyModifiers));
        eventArgs.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (!IsScrubbing || eventArgs.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        EndCapture();
        CommitRequested?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (IsScrubbing && eventArgs.Key == Key.Escape)
        {
            Cancel();
            eventArgs.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs eventArgs)
    {
        base.OnPointerCaptureLost(eventArgs);
        if (IsScrubbing && !releasingCapture)
        {
            IsScrubbing = false;
            capturedPointer = null;
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        if (IsScrubbing)
        {
            Cancel();
        }

        base.OnDetachedFromVisualTree(eventArgs);
    }

    internal void Cancel()
    {
        if (!IsScrubbing)
        {
            return;
        }

        EndCapture();
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    internal void ReleasePointerCapture()
    {
        capturedPointer?.Capture(null);
    }

    private void EndCapture()
    {
        if (!IsScrubbing)
        {
            return;
        }

        releasingCapture = true;
        try
        {
            IsScrubbing = false;
            capturedPointer?.Capture(null);
            capturedPointer = null;
        }
        finally
        {
            releasingCapture = false;
        }
    }
}
