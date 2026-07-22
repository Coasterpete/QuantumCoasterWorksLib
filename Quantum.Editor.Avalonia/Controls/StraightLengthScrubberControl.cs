using Avalonia;
using Avalonia.Automation;
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

internal sealed class StraightLengthKeyboardIncrementEventArgs : EventArgs
{
    internal StraightLengthKeyboardIncrementEventArgs(int direction, KeyModifiers modifiers)
    {
        Direction = direction;
        Modifiers = modifiers;
    }

    internal int Direction { get; }

    internal KeyModifiers Modifiers { get; }
}

internal sealed class StraightLengthScrubCommitEventArgs : EventArgs
{
    internal StraightLengthScrubCommitEventArgs(bool wasKeyboardGesture)
    {
        WasKeyboardGesture = wasKeyboardGesture;
    }

    internal bool WasKeyboardGesture { get; }
}

internal sealed class StraightLengthScrubCancelEventArgs : EventArgs
{
    internal StraightLengthScrubCancelEventArgs(bool wasKeyboardGesture)
    {
        WasKeyboardGesture = wasKeyboardGesture;
    }

    internal bool WasKeyboardGesture { get; }
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
    private ScrubInputKind inputKind;

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
        ToolTip.SetTip(
            this,
            "Drag horizontally or use Left/Right to preview Length. Enter commits; Escape cancels. Shift: fine, Ctrl: coarse.");
        AutomationProperties.SetAutomationId(this, "straightLengthLiveEditor");
        AutomationProperties.SetName(this, "Straight section length live editor");
        AutomationProperties.SetHelpText(
            this,
            "Use Left or Right Arrow to adjust length. Shift is fine, Control is coarse, Enter commits, and Escape cancels.");
        LostFocus += (_, _) =>
        {
            if (IsKeyboardScrubbing)
            {
                Cancel();
            }
        };
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

    internal event EventHandler<StraightLengthKeyboardIncrementEventArgs>?
        KeyboardIncrementRequested;

    internal event EventHandler<StraightLengthScrubCommitEventArgs>? CommitRequested;

    internal event EventHandler<StraightLengthScrubCancelEventArgs>? CancelRequested;

    internal bool IsScrubbing { get; private set; }

    internal bool IsKeyboardScrubbing =>
        IsScrubbing && inputKind == ScrubInputKind.Keyboard;

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
        inputKind = ScrubInputKind.Pointer;
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
        if (!IsScrubbing || inputKind != ScrubInputKind.Pointer)
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
        if (!IsScrubbing || inputKind != ScrubInputKind.Pointer ||
            eventArgs.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        EndScrub();
        IsEnabled = false;
        CommitRequested?.Invoke(
            this,
            new StraightLengthScrubCommitEventArgs(wasKeyboardGesture: false));
        eventArgs.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Key == Key.Escape && IsScrubbing)
        {
            Cancel();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key == Key.Enter && IsKeyboardScrubbing)
        {
            EndScrub();
            IsEnabled = false;
            CommitRequested?.Invoke(
                this,
                new StraightLengthScrubCommitEventArgs(wasKeyboardGesture: true));
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key != Key.Left && eventArgs.Key != Key.Right)
        {
            return;
        }

        if (!IsScrubbing)
        {
            IsScrubbing = true;
            inputKind = ScrubInputKind.Keyboard;
            ScrubStarted?.Invoke(this, EventArgs.Empty);
        }

        if (IsKeyboardScrubbing)
        {
            KeyboardIncrementRequested?.Invoke(
                this,
                new StraightLengthKeyboardIncrementEventArgs(
                    eventArgs.Key == Key.Right ? 1 : -1,
                    eventArgs.KeyModifiers));
            eventArgs.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs eventArgs)
    {
        base.OnPointerCaptureLost(eventArgs);
        if (IsScrubbing && inputKind == ScrubInputKind.Pointer && !releasingCapture)
        {
            IsScrubbing = false;
            inputKind = ScrubInputKind.None;
            capturedPointer = null;
            CancelRequested?.Invoke(
                this,
                new StraightLengthScrubCancelEventArgs(wasKeyboardGesture: false));
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

        bool wasKeyboardGesture = IsKeyboardScrubbing;
        EndScrub();
        CancelRequested?.Invoke(
            this,
            new StraightLengthScrubCancelEventArgs(wasKeyboardGesture));
    }

    internal void ReleasePointerCapture()
    {
        capturedPointer?.Capture(null);
    }

    private void EndScrub()
    {
        if (!IsScrubbing)
        {
            return;
        }

        releasingCapture = true;
        try
        {
            IsScrubbing = false;
            inputKind = ScrubInputKind.None;
            capturedPointer?.Capture(null);
            capturedPointer = null;
        }
        finally
        {
            releasingCapture = false;
        }
    }

    private enum ScrubInputKind
    {
        None,
        Pointer,
        Keyboard
    }
}
