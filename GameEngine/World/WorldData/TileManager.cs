namespace WebPeli.GameEngine.World.WorldData;

public static class TileManager
{
    /// <summary>
    /// Combines multiple TileProperties into a single byte representation.
    /// </summary>
    /// <param name="properties">The collection of TileProperties to combine.</param>
    /// <returns>A byte representing the combined TileProperties.</returns>
    public static byte CreateTileProperties(IEnumerable<TileProperties> properties)
    {
        byte result = 0;
        foreach (var property in properties)
        {
            result |= (byte)property;
        }
        return result;
    }

    public static IEnumerable<TileProperties> GetTileProperties(byte properties)
    {
        foreach (TileProperties property in Enum.GetValues<TileProperties>())
        {
            if ((properties & (byte)property) == (byte)property)
            {
                yield return property;
            }
        }
    }    

    /// <summary>
    /// Combines multiple TileSurface values into a single byte representation.
    /// </summary>
    /// <param name="surfaces">The collection of TileSurface values to combine.</param>
    /// <returns>A byte representing the combined TileSurface values.</returns>
    public static byte CreateTileSurface(IEnumerable<TileSurface> surfaces)
    {
        byte result = 0;
        foreach (var surface in surfaces)
        {
            result |= (byte)surface;
        }
        return result;
    }

    public static IEnumerable<TileSurface> GetTileSurfaces(byte surfaces)
    {
        foreach (TileSurface surface in Enum.GetValues<TileSurface>())
        {
            if ((surfaces & (byte)surface) == (byte)surface)
            {
                yield return surface;
            }
        }
    }

    public static (byte material, byte surface, byte properties) CreateTile(TileMaterial material, IEnumerable<TileSurface> surfaces, IEnumerable<TileProperties> properties)
    {
        return ((byte)material, CreateTileSurface(surfaces), CreateTileProperties(properties));
    }

    // Properties
    public static bool HasProperty(TileProperties properties, TileProperties property) => (properties & property) == property;
    public static void SetProperty(ref TileProperties properties, TileProperties property) => properties |= property;
    public static void RemoveProperty(ref TileProperties properties, TileProperties property) => properties &= ~property;
    public static bool IsWalkable(TileProperties properties) => (properties & TileProperties.Walkable) == TileProperties.Walkable;
    public static bool BlocksLight(TileProperties properties) => (properties & TileProperties.BlocksLight) == TileProperties.BlocksLight;
    public static bool IsTransparent(TileProperties properties) => (properties & TileProperties.Transparent) == TileProperties.Transparent;
    public static bool BlocksProjectiles(TileProperties properties) => (properties & TileProperties.BlocksProjectiles) == TileProperties.BlocksProjectiles;
    public static bool IsSolid(TileProperties properties) => (properties & TileProperties.Solid) == TileProperties.Solid;
    public static bool IsInteractive(TileProperties properties) => (properties & TileProperties.Interactive) == TileProperties.Interactive;
    public static bool IsBreakable(TileProperties properties) => (properties & TileProperties.Breakable) == TileProperties.Breakable;
    // Surface and Material stuff
    public static void SetMaterial(ref byte material, TileMaterial materialType) => material = (byte)materialType;
    public static bool IsMaterial(byte material, TileMaterial materialType) => material == (byte)materialType;
    public static bool HasSurface(TileSurface surface, TileSurface surfaceType) => (surface & surfaceType) == surfaceType;
    public static void AddSurface(ref TileSurface surface, TileSurface surfaceType) => surface |= surfaceType;
    public static void RemoveSurface(ref TileSurface surface, TileSurface surfaceType) => surface &= ~surfaceType;
    public static void ClearSurface(ref TileSurface surface) => surface = 0;

    private static readonly Dictionary<TileMaterial,(byte Capacity, byte Absorption)> _moistureProperties = new()
    {
        { TileMaterial.Sand, (50, 10) },
        { TileMaterial.Dirt, (80, 5) },
        { TileMaterial.Stone, (0, 0) },
        { TileMaterial.Water, (255, 0) },
    };

    public static (byte Capacity, byte Absorption) GetMaterialMoistureProperties(TileMaterial material)
    {
        if (_moistureProperties.TryGetValue(material, out var props))
            return props;
        return (0, 0); // Default for unknown materials
    }
}


