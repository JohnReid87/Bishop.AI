namespace Bishop.Game;

public enum GameState { WaitingToLaunch, Playing, Paused, GameOver, LevelComplete }

public enum PaddleDirection { Left, Right }

public sealed record BrickView(float X, float Y, float Width, float Height, string TagName, string HexColour, bool IsDestroyed);

public sealed record PaddleView(float X, float Y, float Width, float Height);

public sealed record BallView(float X, float Y, float Radius);

public sealed record GameSnapshot(
    GameState State,
    int Score,
    int Lives,
    PaddleView Paddle,
    BallView Ball,
    IReadOnlyList<BrickView> Bricks);
