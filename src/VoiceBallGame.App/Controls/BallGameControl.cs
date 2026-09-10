using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VoiceBallGame.Core.Engine;

namespace VoiceBallGame.App.Controls;

/// <summary>
/// Draws the ball and its goal box, running its own animation loop.
/// </summary>
/// <remarks>
/// The engine produces a frame every 50 ms. Binding the ball's position straight to that, as the
/// WPF version did, makes the motion visibly step at 20 Hz. Instead this control keeps the two
/// most recent frames and redraws on every display frame: the rest position and goal box are
/// interpolated between engine frames, while the oscillation is evaluated from its amplitude,
/// frequency and phase at the actual render time. Evaluating the sine rather than interpolating
/// it matters. Linear interpolation between two points of a 2 Hz sine sampled at 20 Hz visibly
/// flattens the peaks of the swing.
/// </remarks>
public class BallGameControl : Control
{
    private const double TwoPi = Math.PI * 2;

    public static readonly StyledProperty<GameFrame?> FrameProperty =
        AvaloniaProperty.Register<BallGameControl, GameFrame?>(nameof(Frame));

    public static readonly StyledProperty<double> BallSizeProperty =
        AvaloniaProperty.Register<BallGameControl, double>(nameof(BallSize), 30.0);

    /// <summary>The most recent engine frame. Set this from the view model on every frame.</summary>
    public GameFrame? Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public double BallSize
    {
        get => GetValue(BallSizeProperty);
        set => SetValue(BallSizeProperty, value);
    }

    private GameFrame? _previous;
    private GameFrame? _current;
    private TimeSpan _currentArrived;
    private TimeSpan _previousArrived;
    private TimeSpan _now;
    private bool _animating;

    private readonly IBrush _goalBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
    private readonly IPen _goalPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 2);
    private readonly IPen _ballPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), 1);

    static BallGameControl()
    {
        AffectsRender<BallGameControl>(FrameProperty, BallSizeProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != FrameProperty) return;

        if (change.GetNewValue<GameFrame?>() is not { } frame) return;

        _previous = _current;
        _previousArrived = _currentArrived;
        _current = frame;
        _currentArrived = _now;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RequestNextFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animating = false;
    }

    private void RequestNextFrame()
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            _animating = false;
            return;
        }

        _animating = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan time)
    {
        _now = time;

        if (!_animating) return;

        InvalidateVisual();
        RequestNextFrame();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // The control paints its own background so the ball's color reads consistently rather
        // than against whatever theme the operator's desktop happens to use.
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

        if (_current is not { } frame) return;

        double centerX = Bounds.Width / 2;
        double t = InterpolationFactor();

        double goalCenter = Lerp(_previous?.Ball.GoalCenter, frame.Ball.GoalCenter, t);
        double goalHeight = Lerp(_previous?.Ball.GoalHeight, frame.Ball.GoalHeight, t);
        double restPosition = Lerp(_previous?.Ball.BallRestPosition, frame.Ball.BallRestPosition, t);
        double amplitude = Lerp(_previous?.Ball.Amplitude, frame.Ball.Amplitude, t);

        // Carry the oscillation forward from the phase the engine reported, so the swing runs at
        // the display's frame rate instead of the engine's.
        double sinceFrame = Math.Max(0, (_now - _currentArrived).TotalSeconds);
        double phase = frame.Ball.Phase + TwoPi * frame.Ball.FrequencyHz * sinceFrame;
        double ballY = restPosition - amplitude * Math.Cos(phase);

        double goalWidth = Math.Max(60, Bounds.Width * 0.45);
        var goalRect = new Rect(
            centerX - goalWidth / 2,
            goalCenter - goalHeight / 2,
            goalWidth,
            Math.Max(0, goalHeight));

        context.DrawRectangle(_goalBrush, _goalPen, goalRect, 4, 4);

        double radius = BallSize / 2;
        var color = frame.BallColor;
        var ballBrush = new SolidColorBrush(Color.FromRgb(color.R8, color.G8, color.B8));

        context.DrawEllipse(ballBrush, _ballPen, new Point(centerX, ballY), radius, radius);
    }

    /// <summary>
    /// How far the render time has progressed from the previous engine frame to the current one.
    /// </summary>
    private double InterpolationFactor()
    {
        if (_previous is null) return 1.0;

        double span = (_currentArrived - _previousArrived).TotalSeconds;
        if (span <= 0) return 1.0;

        // Interpolation runs one engine frame behind so that the value being approached is one
        // that has already arrived, rather than extrapolating past it. Clamped so a late frame
        // leaves the ball at its last known position instead of overshooting.
        return Math.Clamp((_now - _currentArrived).TotalSeconds / span, 0.0, 1.0);
    }

    private static double Lerp(double? from, double to, double t) =>
        from is { } start ? start + (to - start) * t : to;
}
