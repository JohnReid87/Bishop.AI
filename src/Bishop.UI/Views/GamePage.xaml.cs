using Bishop.Game;
using Bishop.UI.Converters;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Windows.UI;

namespace Bishop.UI.Views;

public sealed partial class GamePage : Page
{
    private readonly BreakoutEngine _engine = new();
    private DispatcherTimer? _timer;
    private readonly Dictionary<int, Border> _brickVisuals = new();
    private Ellipse? _ballVisual;
    private Rectangle? _paddleVisual;
    private bool _leftPressed;
    private bool _rightPressed;
    private DateTime _lastTick;

    public GamePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (App.MainWindow is not null)
            App.MainWindow.Activated += OnWindowActivated;
        if (Frame is { } frame)
            frame.Navigating += OnFrameNavigating;

        Loaded += OnPageLoaded;

        BuildScene();

        _lastTick = DateTime.UtcNow;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        Loaded -= OnPageLoaded;
        StopTimer();
        if (App.MainWindow is not null)
            App.MainWindow.Activated -= OnWindowActivated;
        if (Frame is { } frame)
            frame.Navigating -= OnFrameNavigating;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnPageLoaded;
        RootGrid.Focus(FocusState.Programmatic);
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        => RootGrid.Focus(FocusState.Pointer);

    private void BuildScene()
    {
        Playfield.Children.Clear();
        _brickVisuals.Clear();

        _engine.Reset();
        var snap = _engine.Snapshot;

        _ballVisual = new Ellipse
        {
            Width = snap.Ball.Radius * 2,
            Height = snap.Ball.Radius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(255, 232, 232, 232))
        };
        Playfield.Children.Add(_ballVisual);

        _paddleVisual = new Rectangle
        {
            Width = snap.Paddle.Width,
            Height = snap.Paddle.Height,
            Fill = new SolidColorBrush(Color.FromArgb(255, 232, 232, 232)),
            RadiusX = 2,
            RadiusY = 2
        };
        Playfield.Children.Add(_paddleVisual);

        for (int i = 0; i < snap.Bricks.Count; i++)
        {
            var brick = snap.Bricks[i];
            var visual = CreateBrickVisual(brick);
            Canvas.SetLeft(visual, brick.X);
            Canvas.SetTop(visual, brick.Y);
            Playfield.Children.Add(visual);
            _brickVisuals[i] = visual;
        }

        UpdateVisuals(snap);
    }

    private static Border CreateBrickVisual(BrickView brick)
    {
        HexColorToBrushConverter.TryParseHex(brick.HexColour, out var color);
        return new Border
        {
            Width = brick.Width,
            Height = brick.Height,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(color),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = brick.TagName,
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Black),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
    }

    private void OnTick(object? sender, object e)
    {
        var now = DateTime.UtcNow;
        var dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        if (_leftPressed) _engine.MovePaddle(PaddleDirection.Left);
        if (_rightPressed) _engine.MovePaddle(PaddleDirection.Right);

        _engine.Tick(dt);
        UpdateVisuals(_engine.Snapshot);
    }

    private void UpdateVisuals(GameSnapshot snap)
    {
        if (_ballVisual is not null)
        {
            Canvas.SetLeft(_ballVisual, snap.Ball.X - snap.Ball.Radius);
            Canvas.SetTop(_ballVisual, snap.Ball.Y - snap.Ball.Radius);
        }

        if (_paddleVisual is not null)
        {
            Canvas.SetLeft(_paddleVisual, snap.Paddle.X);
            Canvas.SetTop(_paddleVisual, snap.Paddle.Y);
        }

        for (int i = 0; i < snap.Bricks.Count; i++)
        {
            if (snap.Bricks[i].IsDestroyed && _brickVisuals.TryGetValue(i, out var visual))
            {
                Playfield.Children.Remove(visual);
                _brickVisuals.Remove(i);
            }
        }

        ScoreText.Text = $"Score: {snap.Score}";
        LivesText.Text = $"Lives: {snap.Lives}";
        PauseText.Visibility = snap.State == GameState.Paused ? Visibility.Visible : Visibility.Collapsed;
        LaunchHintText.Visibility = snap.State == GameState.WaitingToLaunch ? Visibility.Visible : Visibility.Collapsed;

        if (snap.State is GameState.GameOver or GameState.LevelComplete)
        {
            OverlayTitle.Text = snap.State == GameState.GameOver ? "GAME OVER" : "LEVEL COMPLETE";
            OverlayScore.Text = $"Score: {snap.Score}";
            OverlayGrid.Visibility = Visibility.Visible;
        }
        else
        {
            OverlayGrid.Visibility = Visibility.Collapsed;
        }
    }

    private void RootGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
                _leftPressed = true;
                e.Handled = true;
                break;
            case VirtualKey.Right:
                _rightPressed = true;
                e.Handled = true;
                break;
            case VirtualKey.Space:
                _engine.LaunchBall();
                e.Handled = true;
                break;
            case VirtualKey.Escape:
            case VirtualKey.P:
                _engine.TogglePause();
                e.Handled = true;
                break;
        }
    }

    private void RootGrid_PreviewKeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
                _leftPressed = false;
                e.Handled = true;
                break;
            case VirtualKey.Right:
                _rightPressed = false;
                e.Handled = true;
                break;
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (_engine.Snapshot.State == GameState.Playing)
                _engine.TogglePause();
        }
    }

    private void OnFrameNavigating(object sender, NavigatingCancelEventArgs e)
    {
        if (_engine.Snapshot.State == GameState.Playing)
            _engine.TogglePause();
    }

    private void PlayAgain_Click(object sender, RoutedEventArgs e)
    {
        BuildScene();
        RootGrid.Focus(FocusState.Programmatic);
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }
}
