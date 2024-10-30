namespace WebPeli.GameEngine.Managers;



public record ViewportData
{
    required public byte[,] TileGrid { get; init; }
    // Later: public Dictionary<(int x, int y), EntityRenderData> Entities { get; init; }
}

public class ViewportManager : BaseManager
{
    private readonly MapManager _mapManager;
    // Later: private readonly EntityManager _entityManager;

    public ViewportManager(MapManager mapManager)
    {
        _mapManager = mapManager;
        EventManager.RegisterListener<ViewportRequest>(this);
    }

    public override void HandleMessage(IEvent evt)
    {
        if (evt is ViewportRequest req)
        {
            var tileGrid = _mapManager.GetTilesInArea(
                req.CameraX,
                req.CameraY,
                req.ViewportWidth,
                req.ViewportHeight,
                req.WorldWidth,
                req.WorldHeight
            );

            var viewportData = new ViewportData
            {
                TileGrid = tileGrid,
                // Later: Entities = _entityManager.GetEntitiesInArea(...)
            };

            EventManager.EmitCallback(req.CallbackId, viewportData);
        }
    }

    public override void Init()
    {
        // Nothing to initialize yet
    }

    public override void Destroy()
    {
        EventManager.UnregisterListener<ViewportRequest>(this);
    }
}