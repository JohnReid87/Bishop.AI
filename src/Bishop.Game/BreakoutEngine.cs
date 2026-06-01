namespace Bishop.Game;

public sealed class BreakoutEngine
{
    public const float FieldWidth = 800f;
    public const float FieldHeight = 600f;

    private const float PaddleWidth = 100f;
    private const float PaddleHeight = 12f;
    private const float PaddleY = 550f;
    private const float PaddleStep = 15f;

    private const float BallRadius = 8f;
    private const float BallLaunchSpeed = 420f;

    private const float BrickWidth = 88f;
    private const float BrickHeight = 22f;
    private const float BrickGapX = 4f;
    private const float BrickGapY = 6f;
    private const int BrickColumns = 8;
    private const int BrickRows = 5;

    // One row per entry; uses 5 of the 8 brand tag palette entries.
    private static readonly (string Tag, string Hex)[] RowTags =
    [
        ("feature",  "#7fa87a"),
        ("bug",      "#c97a8a"),
        ("arch",     "#6b8caf"),
        ("docs",     "#5fa89c"),
        ("test",     "#c4a85f"),
    ];

    private float _paddleX;
    private float _ballX;
    private float _ballY;
    private float _ballDx;
    private float _ballDy;
    private int _score;
    private int _lives;
    private GameState _state;
    private readonly Brick[] _bricks;
    private int _destroyedBrickCount;

    public BreakoutEngine()
    {
        _bricks = CreateBricks();
        Reset();
    }

    public GameSnapshot Snapshot => new(
        _state,
        _score,
        _lives,
        new PaddleView(_paddleX, PaddleY, PaddleWidth, PaddleHeight),
        new BallView(_ballX, _ballY, BallRadius),
        _bricks.Select(b => new BrickView(b.X, b.Y, BrickWidth, BrickHeight, b.TagName, b.HexColour, b.IsDestroyed)).ToList()
    );

    public void Tick(double deltaSeconds)
    {
        if (_state != GameState.Playing) return;

        float dt = (float)deltaSeconds;
        _ballX += _ballDx * dt;
        _ballY += _ballDy * dt;

        HandleWallCollisions();

        if (_ballY > FieldHeight)
        {
            LoseBall();
            return;
        }

        HandlePaddleCollision();
        HandleBrickCollisions();

        if (_destroyedBrickCount == _bricks.Length)
            _state = GameState.LevelComplete;
    }

    public void MovePaddle(PaddleDirection direction)
    {
        if (_state is not (GameState.Playing or GameState.WaitingToLaunch)) return;

        float dx = direction == PaddleDirection.Left ? -PaddleStep : PaddleStep;
        _paddleX = Math.Clamp(_paddleX + dx, 0f, FieldWidth - PaddleWidth);

        // Ball sits on paddle until launched
        if (_state == GameState.WaitingToLaunch)
            _ballX = _paddleX + PaddleWidth / 2f;
    }

    public void LaunchBall()
    {
        if (_state != GameState.WaitingToLaunch) return;
        _ballDx = 168f;
        _ballDy = -BallLaunchSpeed;
        _state = GameState.Playing;
    }

    public void TogglePause()
    {
        if (_state == GameState.Playing) _state = GameState.Paused;
        else if (_state == GameState.Paused) _state = GameState.Playing;
    }

    public void Reset()
    {
        _paddleX = (FieldWidth - PaddleWidth) / 2f;
        _score = 0;
        _lives = 3;
        _destroyedBrickCount = 0;
        _state = GameState.WaitingToLaunch;
        ResetBall();
        for (int i = 0; i < _bricks.Length; i++)
            _bricks[i].IsDestroyed = false;
    }

    // ── Test helpers (visible to Bishop.Tests via InternalsVisibleTo) ──────────

    internal void SetBallForTest(float x, float y, float dx, float dy)
    {
        _ballX = x;
        _ballY = y;
        _ballDx = dx;
        _ballDy = dy;
        if (_state == GameState.WaitingToLaunch) _state = GameState.Playing;
    }

    internal void SetLivesForTest(int lives) => _lives = lives;

    internal void DestroyAllBricksForTest()
    {
        for (int i = 0; i < _bricks.Length; i++)
            _bricks[i].IsDestroyed = true;
        _destroyedBrickCount = _bricks.Length;
    }

    internal float BallDxForTest => _ballDx;
    internal float BallDyForTest => _ballDy;

    // ── Private helpers ────────────────────────────────────────────────────────

    private void HandleWallCollisions()
    {
        if (_ballX - BallRadius < 0f)
        {
            _ballX = BallRadius;
            _ballDx = MathF.Abs(_ballDx);
        }
        else if (_ballX + BallRadius > FieldWidth)
        {
            _ballX = FieldWidth - BallRadius;
            _ballDx = -MathF.Abs(_ballDx);
        }

        if (_ballY - BallRadius < 0f)
        {
            _ballY = BallRadius;
            _ballDy = MathF.Abs(_ballDy);
        }
    }

    private void HandlePaddleCollision()
    {
        if (!BallTouchesPaddle()) return;

        float offset = (_ballX - (_paddleX + PaddleWidth / 2f)) / (PaddleWidth / 2f);
        _ballDx = offset * 400f;
        _ballDy = -MathF.Abs(_ballDy);
        _ballY = PaddleY - BallRadius;
    }

    private bool BallTouchesPaddle() =>
        _ballDy > 0f &&
        _ballX + BallRadius > _paddleX &&
        _ballX - BallRadius < _paddleX + PaddleWidth &&
        _ballY + BallRadius > PaddleY &&
        _ballY - BallRadius < PaddleY + PaddleHeight;

    private void HandleBrickCollisions()
    {
        for (int i = 0; i < _bricks.Length; i++)
        {
            ref Brick brick = ref _bricks[i];
            if (brick.IsDestroyed) continue;
            if (!BallOverlapsBrick(_ballX, _ballY, BallRadius, ref brick)) continue;

            brick.IsDestroyed = true;
            _score += 10;
            _destroyedBrickCount++;
            ReflectBallOffBrick(brick.X, brick.Y);
            break;
        }
    }

    private void ReflectBallOffBrick(float brickX, float brickY)
    {
        float overlapX = MathF.Min(
            _ballX + BallRadius - brickX,
            brickX + BrickWidth - (_ballX - BallRadius));
        float overlapY = MathF.Min(
            _ballY + BallRadius - brickY,
            brickY + BrickHeight - (_ballY - BallRadius));

        if (overlapX < overlapY)
            _ballDx = -_ballDx;
        else
            _ballDy = -_ballDy;
    }

    private void ResetBall()
    {
        _ballX = _paddleX + PaddleWidth / 2f;
        _ballY = PaddleY - BallRadius - 1f;
        _ballDx = 0f;
        _ballDy = 0f;
    }

    private void LoseBall()
    {
        _lives--;
        if (_lives <= 0)
        {
            _lives = 0;
            _state = GameState.GameOver;
        }
        else
        {
            ResetBall();
            _state = GameState.WaitingToLaunch;
        }
    }

    private static bool BallOverlapsBrick(float ballX, float ballY, float radius, ref Brick brick)
    {
        float closestX = Math.Clamp(ballX, brick.X, brick.X + BrickWidth);
        float closestY = Math.Clamp(ballY, brick.Y, brick.Y + BrickHeight);
        float dx = ballX - closestX;
        float dy = ballY - closestY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static Brick[] CreateBricks()
    {
        float totalWidth = BrickColumns * BrickWidth + (BrickColumns - 1) * BrickGapX;
        float startX = (FieldWidth - totalWidth) / 2f;
        const float startY = 60f;

        var bricks = new Brick[BrickRows * BrickColumns];
        int idx = 0;
        for (int row = 0; row < BrickRows; row++)
        {
            (string tag, string hex) = RowTags[row];
            for (int col = 0; col < BrickColumns; col++)
            {
                float x = startX + col * (BrickWidth + BrickGapX);
                float y = startY + row * (BrickHeight + BrickGapY);
                bricks[idx++] = new Brick(x, y, tag, hex);
            }
        }
        return bricks;
    }

    private struct Brick(float x, float y, string tagName, string hexColour)
    {
        public readonly float X = x;
        public readonly float Y = y;
        public readonly string TagName = tagName;
        public readonly string HexColour = hexColour;
        public bool IsDestroyed;
    }
}
