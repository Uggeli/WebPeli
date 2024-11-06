namespace WebPeli.GameEngine.EntitySystem;

public interface IEntity {
    Guid Id { get; }
} // Marker interface
public readonly record struct EntityPosition(byte X, byte Y);


