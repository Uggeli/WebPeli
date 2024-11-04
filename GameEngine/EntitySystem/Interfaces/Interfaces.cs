namespace WebPeli.GameEngine.EntitySystem.Interfaces;

public interface IMetabolism
{
    // 0-64, rest is for severerity levels like hungry, starving, etc.
    byte Hunger { get; set;} 
    byte Thirst { get; set;}
    byte Fatigue { get; set;} 
}
public interface IHealth
{
    byte Health { get; set;}
    byte MaxHealth { get; set;}
}


