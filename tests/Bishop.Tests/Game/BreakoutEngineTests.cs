using Bishop.Game;
using FluentAssertions;

namespace Bishop.Tests.Game;

public class BreakoutEngineTests
{
    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void New_HasThreeLives() =>
        new BreakoutEngine().Snapshot.Lives.Should().Be(3);

    [Fact]
    public void New_HasZeroScore() =>
        new BreakoutEngine().Snapshot.Score.Should().Be(0);

    [Fact]
    public void New_IsInWaitingToLaunchState() =>
        new BreakoutEngine().Snapshot.State.Should().Be(GameState.WaitingToLaunch);

    [Fact]
    public void New_HasFortyBricks() =>
        new BreakoutEngine().Snapshot.Bricks.Should().HaveCount(40);

    [Fact]
    public void New_NoBricksDestroyed() =>
        new BreakoutEngine().Snapshot.Bricks.Should().AllSatisfy(b => b.IsDestroyed.Should().BeFalse());

    [Fact]
    public void New_BricksCarryTagNamesAndHexColours()
    {
        var bricks = new BreakoutEngine().Snapshot.Bricks;
        bricks.Should().Contain(b => b.TagName == "feature" && b.HexColour == "#7fa87a");
        bricks.Should().Contain(b => b.TagName == "bug"     && b.HexColour == "#c97a8a");
        bricks.Should().Contain(b => b.TagName == "arch"    && b.HexColour == "#6b8caf");
        bricks.Should().Contain(b => b.TagName == "docs"    && b.HexColour == "#5fa89c");
        bricks.Should().Contain(b => b.TagName == "test"    && b.HexColour == "#c4a85f");
    }

    // ── LaunchBall ────────────────────────────────────────────────────────────

    [Fact]
    public void LaunchBall_TransitionsToPlaying()
    {
        var engine = new BreakoutEngine();
        engine.LaunchBall();
        engine.Snapshot.State.Should().Be(GameState.Playing);
    }

    [Fact]
    public void LaunchBall_WhenAlreadyPlaying_DoesNotChangeState()
    {
        var engine = new BreakoutEngine();
        engine.LaunchBall();
        engine.LaunchBall();
        engine.Snapshot.State.Should().Be(GameState.Playing);
    }

    [Theory]
    [InlineData(GameState.Paused)]
    [InlineData(GameState.GameOver)]
    [InlineData(GameState.LevelComplete)]
    public void LaunchBall_WhenNotWaitingToLaunch_StateUnchanged(GameState state)
    {
        var engine = InState(state);

        engine.LaunchBall();

        engine.Snapshot.State.Should().Be(state);
    }

    // ── TogglePause ───────────────────────────────────────────────────────────

    [Fact]
    public void TogglePause_WhenPlaying_TransitionsToPaused()
    {
        var engine = new BreakoutEngine();
        engine.LaunchBall();
        engine.TogglePause();
        engine.Snapshot.State.Should().Be(GameState.Paused);
    }

    [Fact]
    public void TogglePause_WhenPaused_TransitionsToPlaying()
    {
        var engine = new BreakoutEngine();
        engine.LaunchBall();
        engine.TogglePause();
        engine.TogglePause();
        engine.Snapshot.State.Should().Be(GameState.Playing);
    }

    // ── Pause halts advance ───────────────────────────────────────────────────

    [Fact]
    public void Tick_WhenPaused_DoesNotMoveBall()
    {
        var engine = new BreakoutEngine();
        engine.LaunchBall();
        engine.TogglePause();
        var before = engine.Snapshot.Ball;
        engine.Tick(1.0);
        var after = engine.Snapshot.Ball;
        after.X.Should().Be(before.X);
        after.Y.Should().Be(before.Y);
    }

    [Fact]
    public void Tick_WhenWaitingToLaunch_DoesNotMoveBall()
    {
        var engine = new BreakoutEngine();
        var before = engine.Snapshot.Ball;
        engine.Tick(1.0);
        var after = engine.Snapshot.Ball;
        after.X.Should().Be(before.X);
        after.Y.Should().Be(before.Y);
    }

    // ── Wall collisions ───────────────────────────────────────────────────────

    [Fact]
    public void Tick_WhenBallReachesLeftWall_ReflectsDxPositive()
    {
        var engine = new BreakoutEngine();
        engine.SetBallForTest(x: 5f, y: 300f, dx: -200f, dy: 0f);
        engine.Tick(1.0 / 60.0);
        engine.BallDxForTest.Should().BePositive();
    }

    [Fact]
    public void Tick_WhenBallReachesRightWall_ReflectsDxNegative()
    {
        var engine = new BreakoutEngine();
        engine.SetBallForTest(x: BreakoutEngine.FieldWidth - 5f, y: 300f, dx: 200f, dy: 0f);
        engine.Tick(1.0 / 60.0);
        engine.BallDxForTest.Should().BeNegative();
    }

    [Fact]
    public void Tick_WhenBallReachesTopWall_ReflectsDyPositive()
    {
        var engine = new BreakoutEngine();
        engine.SetBallForTest(x: 400f, y: 5f, dx: 0f, dy: -200f);
        engine.Tick(1.0 / 60.0);
        engine.BallDyForTest.Should().BePositive();
    }

    // ── Paddle collision ──────────────────────────────────────────────────────

    [Fact]
    public void Tick_WhenBallHitsPaddleCentre_DyBecomesNegative()
    {
        var engine = new BreakoutEngine();
        var paddle = engine.Snapshot.Paddle;
        float ballRadius = engine.Snapshot.Ball.Radius;
        engine.SetBallForTest(
            x: paddle.X + paddle.Width / 2f,
            y: paddle.Y - ballRadius - 1f,
            dx: 0f,
            dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.BallDyForTest.Should().BeNegative();
    }

    [Fact]
    public void Tick_WhenBallHitsPaddleRightEdge_DxIsPositive()
    {
        var engine = new BreakoutEngine();
        var paddle = engine.Snapshot.Paddle;
        float ballRadius = engine.Snapshot.Ball.Radius;
        engine.SetBallForTest(
            x: paddle.X + paddle.Width - 5f,
            y: paddle.Y - ballRadius - 1f,
            dx: 0f,
            dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.BallDxForTest.Should().BePositive();
    }

    [Fact]
    public void Tick_WhenBallHitsPaddleLeftEdge_DxIsNegative()
    {
        var engine = new BreakoutEngine();
        var paddle = engine.Snapshot.Paddle;
        float ballRadius = engine.Snapshot.Ball.Radius;
        engine.SetBallForTest(
            x: paddle.X + 5f,
            y: paddle.Y - ballRadius - 1f,
            dx: 0f,
            dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.BallDxForTest.Should().BeNegative();
    }

    [Fact]
    public void Tick_WhenBallMovingUpward_DoesNotCollideWithPaddle()
    {
        var engine = new BreakoutEngine();
        var paddle = engine.Snapshot.Paddle;
        float ballRadius = engine.Snapshot.Ball.Radius;
        engine.SetBallForTest(
            x: paddle.X + paddle.Width / 2f,
            y: paddle.Y - ballRadius - 1f,
            dx: 0f,
            dy: -200f); // moving upward — _ballDy > 0f guard should reject collision

        engine.Tick(1.0 / 60.0);

        engine.BallDyForTest.Should().BeNegative(); // dy unchanged; no paddle bounce
    }

    // ── Brick collision ───────────────────────────────────────────────────────

    [Fact]
    public void Tick_WhenBallHitsBrickFromAbove_DestroysBrick()
    {
        var engine = new BreakoutEngine();
        var brick = engine.Snapshot.Bricks[0];
        float ballRadius = engine.Snapshot.Ball.Radius;
        engine.SetBallForTest(
            x: brick.X + brick.Width / 2f,
            y: brick.Y - ballRadius - 1f,
            dx: 0f,
            dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.Snapshot.Bricks[0].IsDestroyed.Should().BeTrue();
    }

    [Fact]
    public void Tick_WhenBallHitsBrick_ReflectsDy()
    {
        var engine = new BreakoutEngine();
        var brick = engine.Snapshot.Bricks[0];
        float ballRadius = engine.Snapshot.Ball.Radius;
        engine.SetBallForTest(
            x: brick.X + brick.Width / 2f,
            y: brick.Y - ballRadius - 1f,
            dx: 0f,
            dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.BallDyForTest.Should().BeNegative();
    }

    [Fact]
    public void Tick_WhenBallHitsBrick_IncrementsScoreByTen()
    {
        var engine = new BreakoutEngine();
        var brick = engine.Snapshot.Bricks[0];
        float ballRadius = engine.Snapshot.Ball.Radius;
        engine.SetBallForTest(
            x: brick.X + brick.Width / 2f,
            y: brick.Y - ballRadius - 1f,
            dx: 0f,
            dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.Snapshot.Score.Should().Be(10);
    }

    [Fact]
    public void Tick_WhenBallHitsBrickFromSide_ReflectsDx()
    {
        var engine = new BreakoutEngine();
        var brick = engine.Snapshot.Bricks[0];
        float ballRadius = engine.Snapshot.Ball.Radius;
        engine.SetBallForTest(
            x: brick.X - ballRadius - 1f,
            y: brick.Y + brick.Height / 2f,
            dx: 200f,
            dy: 0f);
        engine.Tick(1.0 / 60.0);
        engine.BallDxForTest.Should().BeNegative();
    }

    [Fact]
    public void Tick_WhenBrickWasDestroyedInPreviousTick_SkipsItWithoutScoring()
    {
        var engine = new BreakoutEngine();
        var brick = engine.Snapshot.Bricks[0];
        float ballRadius = engine.Snapshot.Ball.Radius;
        engine.SetBallForTest(
            x: brick.X + brick.Width / 2f,
            y: brick.Y - ballRadius - 1f,
            dx: 0f,
            dy: 200f);
        engine.Tick(1.0 / 60.0);

        engine.SetBallForTest(
            x: brick.X + brick.Width / 2f,
            y: brick.Y - ballRadius - 1f,
            dx: 0f,
            dy: 200f);
        engine.Tick(1.0 / 60.0);

        engine.Snapshot.Score.Should().Be(10);
    }

    [Fact]
    public void Tick_WhenBallOverlapsTwoBricksSimultaneously_DestroysOnlyOnePerTick()
    {
        var engine = new BreakoutEngine();
        var brick0 = engine.Snapshot.Bricks[0];
        var brick1 = engine.Snapshot.Bricks[1];
        float gapCenterX = (brick0.X + brick0.Width + brick1.X) / 2f;
        float brickCenterY = brick0.Y + brick0.Height / 2f;
        engine.SetBallForTest(x: gapCenterX, y: brickCenterY, dx: 0f, dy: 0f);

        engine.Tick(1.0 / 60.0);

        engine.Snapshot.Bricks.Count(b => b.IsDestroyed).Should().Be(1);
        engine.Snapshot.Score.Should().Be(10);
    }

    // ── Lives and GameOver ────────────────────────────────────────────────────

    [Fact]
    public void Tick_WhenBallExitsBottom_DecrementsLives()
    {
        var engine = new BreakoutEngine();
        engine.SetBallForTest(x: 400f, y: BreakoutEngine.FieldHeight - 1f, dx: 0f, dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.Snapshot.Lives.Should().Be(2);
    }

    [Fact]
    public void Tick_WhenBallExitsBottomWithLivesRemaining_TransitionsToWaitingToLaunch()
    {
        var engine = new BreakoutEngine();
        engine.SetBallForTest(x: 400f, y: BreakoutEngine.FieldHeight - 1f, dx: 0f, dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.Snapshot.State.Should().Be(GameState.WaitingToLaunch);
    }

    [Fact]
    public void Tick_WhenLastLifeLost_TransitionsToGameOver()
    {
        var engine = new BreakoutEngine();
        engine.SetLivesForTest(1);
        engine.SetBallForTest(x: 400f, y: BreakoutEngine.FieldHeight - 1f, dx: 0f, dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.Snapshot.State.Should().Be(GameState.GameOver);
    }

    [Fact]
    public void Tick_WhenLastLifeLost_LivesIsZero()
    {
        var engine = new BreakoutEngine();
        engine.SetLivesForTest(1);
        engine.SetBallForTest(x: 400f, y: BreakoutEngine.FieldHeight - 1f, dx: 0f, dy: 200f);
        engine.Tick(1.0 / 60.0);
        engine.Snapshot.Lives.Should().Be(0);
    }

    [Fact]
    public void Tick_WhenBallIsAtExactlyFieldHeight_DoesNotDecrementLives()
    {
        var engine = new BreakoutEngine();
        engine.SetBallForTest(x: 400f, y: BreakoutEngine.FieldHeight, dx: 0f, dy: 0f);

        engine.Tick(1.0 / 60.0);

        engine.Snapshot.Lives.Should().Be(3);
    }

    // ── LevelComplete ─────────────────────────────────────────────────────────

    [Fact]
    public void Tick_WhenAllBricksDestroyed_TransitionsToLevelComplete()
    {
        var engine = new BreakoutEngine();
        engine.SetBallForTest(x: 400f, y: 300f, dx: 0f, dy: 0f);
        engine.DestroyAllBricksForTest();
        engine.Tick(1.0 / 60.0);
        engine.Snapshot.State.Should().Be(GameState.LevelComplete);
    }

    // ── MovePaddle ────────────────────────────────────────────────────────────

    [Fact]
    public void MovePaddle_Right_MovesPaddleRight()
    {
        var engine = new BreakoutEngine();
        float before = engine.Snapshot.Paddle.X;
        engine.MovePaddle(PaddleDirection.Right);
        engine.Snapshot.Paddle.X.Should().BeGreaterThan(before);
    }

    [Fact]
    public void MovePaddle_Left_MovesPaddleLeft()
    {
        var engine = new BreakoutEngine();
        float before = engine.Snapshot.Paddle.X;
        engine.MovePaddle(PaddleDirection.Left);
        engine.Snapshot.Paddle.X.Should().BeLessThan(before);
    }

    [Fact]
    public void MovePaddle_WhenWaitingToLaunch_BallFollowsPaddle()
    {
        var engine = new BreakoutEngine();
        float before = engine.Snapshot.Ball.X;
        engine.MovePaddle(PaddleDirection.Right);
        engine.Snapshot.Ball.X.Should().BeGreaterThan(before);
    }

    [Fact]
    public void MovePaddle_Right_WhenAtRightEdge_PaddlePositionUnchanged()
    {
        var engine = new BreakoutEngine();
        for (int i = 0; i < 100; i++) engine.MovePaddle(PaddleDirection.Right);
        float atEdge = engine.Snapshot.Paddle.X;

        engine.MovePaddle(PaddleDirection.Right);

        engine.Snapshot.Paddle.X.Should().Be(atEdge);
    }

    [Fact]
    public void MovePaddle_Left_WhenAtLeftEdge_PaddlePositionUnchanged()
    {
        var engine = new BreakoutEngine();
        for (int i = 0; i < 100; i++) engine.MovePaddle(PaddleDirection.Left);
        float atEdge = engine.Snapshot.Paddle.X;

        engine.MovePaddle(PaddleDirection.Left);

        engine.Snapshot.Paddle.X.Should().Be(atEdge);
    }

    [Theory]
    [InlineData(GameState.Paused)]
    [InlineData(GameState.GameOver)]
    [InlineData(GameState.LevelComplete)]
    public void MovePaddle_WhenInNonMovableState_PaddlePositionUnchanged(GameState state)
    {
        var engine = InState(state);
        float before = engine.Snapshot.Paddle.X;

        engine.MovePaddle(PaddleDirection.Right);

        engine.Snapshot.Paddle.X.Should().Be(before);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_AfterGameOver_RestoresInitialState()
    {
        var engine = new BreakoutEngine();
        engine.SetLivesForTest(1);
        engine.SetBallForTest(x: 400f, y: BreakoutEngine.FieldHeight - 1f, dx: 0f, dy: 200f);
        engine.Tick(1.0 / 60.0); // triggers GameOver
        engine.Reset();
        var snap = engine.Snapshot;
        snap.State.Should().Be(GameState.WaitingToLaunch);
        snap.Lives.Should().Be(3);
        snap.Score.Should().Be(0);
        snap.Bricks.Should().AllSatisfy(b => b.IsDestroyed.Should().BeFalse());
    }

    [Fact]
    public void Reset_WhenPlaying_RestoresInitialState()
    {
        var engine = new BreakoutEngine();
        engine.LaunchBall();

        engine.Reset();

        var snap = engine.Snapshot;
        snap.State.Should().Be(GameState.WaitingToLaunch);
        snap.Lives.Should().Be(3);
        snap.Score.Should().Be(0);
        snap.Bricks.Should().AllSatisfy(b => b.IsDestroyed.Should().BeFalse());
    }

    [Fact]
    public void Reset_WhenPaused_RestoresInitialState()
    {
        var engine = PausedEngine();

        engine.Reset();

        var snap = engine.Snapshot;
        snap.State.Should().Be(GameState.WaitingToLaunch);
        snap.Lives.Should().Be(3);
        snap.Score.Should().Be(0);
        snap.Bricks.Should().AllSatisfy(b => b.IsDestroyed.Should().BeFalse());
    }

    // ── Degenerate delta ──────────────────────────────────────────────────────

    [Fact]
    public void Tick_WithZeroDelta_DoesNotMoveBall()
    {
        var engine = new BreakoutEngine();
        engine.LaunchBall();
        var before = engine.Snapshot.Ball;

        engine.Tick(0.0);

        var after = engine.Snapshot.Ball;
        after.X.Should().Be(before.X);
        after.Y.Should().Be(before.Y);
    }

    // ── Snapshot ──────────────────────────────────────────────────────────────

    [Fact]
    public void Snapshot_IsValueCopy_UnaffectedBySubsequentStateChange()
    {
        var engine = new BreakoutEngine();
        var snap1 = engine.Snapshot;

        engine.LaunchBall();
        engine.Tick(1.0 / 60.0);

        snap1.State.Should().Be(GameState.WaitingToLaunch);
        engine.Snapshot.State.Should().Be(GameState.Playing);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BreakoutEngine InState(GameState state) => state switch
    {
        GameState.Paused        => PausedEngine(),
        GameState.GameOver      => GameOverEngine(),
        GameState.LevelComplete => LevelCompleteEngine(),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

    private static BreakoutEngine PausedEngine()
    {
        var engine = new BreakoutEngine();
        engine.LaunchBall();
        engine.TogglePause();
        return engine;
    }

    private static BreakoutEngine GameOverEngine()
    {
        var engine = new BreakoutEngine();
        engine.SetLivesForTest(1);
        engine.SetBallForTest(x: 400f, y: BreakoutEngine.FieldHeight - 1f, dx: 0f, dy: 200f);
        engine.Tick(1.0 / 60.0);
        return engine;
    }

    private static BreakoutEngine LevelCompleteEngine()
    {
        var engine = new BreakoutEngine();
        engine.SetBallForTest(x: 400f, y: 300f, dx: 0f, dy: 0f);
        engine.DestroyAllBricksForTest();
        engine.Tick(1.0 / 60.0);
        return engine;
    }
}
