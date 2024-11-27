namespace WebPeli.GameEngine.Util;

public static class Tools
{
    public readonly static Random Random = new();
}

public enum CurrentAction : byte
{
    Idle,
    Moving,
    Attacking,
}

public enum Direction : byte
{
    Up = 0,
    North = Up,
    Right = 1,
    East = Right,
    Down = 2,
    South = Down,
    Left = 3,
    West = Left,
    None = 4
}
// Movement system:
// Ai checks available moves and then selects move it wants to perform and sends MoveEntityRequest to MovementManager
// MovementManager checks if the move is valid and then moves the entity and sends event to AnimationManager
// Move takes time and entity can't move again until the move is completed
public enum MovementType : byte
{
    Walk = 0,
    Run = 1,
    Sneak = 2,
    jump = 3,
    climb = 4,
    swim = 5,
}