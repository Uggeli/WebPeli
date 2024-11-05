namespace WebPeli.GameEngine.EntitySystem.Interfaces;

public interface IMetabolism
{
    public int State { get; set; }
}
public interface IHealth
{
    byte Health { get; set;}
    byte MaxHealth { get; set;}
}

public interface IPosition
{
    public int X { get; set; }
    public int Y { get; set; }
}

public interface IRenderable
{
    public byte TextureId { get; set; }
    public byte Facing { get; set; }
    public string CurrentAction { get; set; }
}


