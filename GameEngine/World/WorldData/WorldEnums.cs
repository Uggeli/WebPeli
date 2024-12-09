namespace WebPeli.GameEngine.World.WorldData;

/// <summary>
/// Represents the connection between two chunks.
/// </summary>
[Flags]
public enum ChunkConnection : byte
{
    None = 0,
    NorthSouth = 1 << 0,  // 1 can go from north to south and vice versa
    NorthEast  = 1 << 1,  // 2 can go from north to east and vice versa
    NorthWest  = 1 << 2,  // 4 can go from north to west and vice versa
    SouthEast  = 1 << 3,  // 8 can go from south to east and vice versa
    SouthWest  = 1 << 4,  // 16 can go from south to west and vice versa
    EastWest   = 1 << 5,  // 32 can go from east to west and vice versa
    
    // Common combinations
    AllNorth = NorthSouth | NorthEast | NorthWest,     // 7  
    AllSouth = NorthSouth | SouthEast | SouthWest,     // 25
    AllEast = NorthEast | SouthEast | EastWest,        // 42
    AllWest = NorthWest | SouthWest | EastWest,        // 52
    All = AllNorth | AllSouth | EastWest               // 63
}


// Tile Consists of 3 bytes
// 1. Material
// 2. Surface <- whatever is on top of the material, rain, snow, blood, etc.
// 3. Properties <- can we walk on it, is it solid, does it block light, etc.

/// <summary>
/// Represents the properties of a tile.
/// </summary>
[Flags]
public enum TileProperties : byte
{
    None = 0,
    Walkable = 1 << 0,        // 1
    BlocksLight = 1 << 1,     // 2
    Transparent = 1 << 2,     // 4
    BlocksProjectiles = 1 << 3,// 8
    Solid = 1 << 4,           // 16
    Interactive = 1 << 5,     // 32
    Breakable = 1 << 6,       // 64
    Reserved = 1 << 7         // 128
}

public enum TileMaterial : byte
{
    None = 0,
    Dirt = 1,
    Stone = 2,
    Wood = 3,
    Metal = 4,
    Ice = 5,
    Sand = 6,
    Water = 8,
    Lava = 9,
    Snow = 10,
    Blood = 12,  // Tile made of blood, Fucking metal ,\m/
    Mud = 13,
}

[Flags]
public enum TileSurface : byte
{
    None = 0,
    ShortGrass = 1 << 0,
    TallGrass = 1 << 6,
    Snow = 1 << 1,       // Snow covering grass
    Moss = 1 << 2,       // Moss growing alongside grass
    Water = 1 << 3,      // Puddle on grass
    Blood = 1 << 4,      // Blood stains on snow
    Mud = 1 << 5,        // Mud mixed with grass
    Flowers = 1 << 7
}

[Flags]
public enum ZoneEdge : byte
{
    None = 0,
    // Edges within chunk
    North = 1 << 0,     // 1
    South = 1 << 1,     // 2
    East = 1 << 2,      // 4
    West = 1 << 3,      // 8
    // Chunk boundary edges
    ChunkNorth = 1 << 4, // 16
    ChunkSouth = 1 << 5, // 32
    ChunkEast = 1 << 6,  // 64
    ChunkWest = 1 << 7   // 128
}


