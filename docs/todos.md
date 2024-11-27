# WebPeli Development TODOs:


# General TODOs
## High priority


## Low priority
- add shutdown thingies for server that terminates all websocket connections and shutdowns all systems and managers in orderly manner
- move enums to file of their own
- add multitile entity pathfinding


# Roadmap:

# Milestone: World.API
  TODO:
    Spatial Queries:
    - GetEntitiesInArea(Position, radius) -> List<int>
    - GetEntitiesInChunk(Position) -> List<int>
    - GetResourcePointsInArea(Position, radius) -> List<ResourcePoint>
    - CanPlaceEntity(Position, volume) -> bool

    Environmental:
    - GetWeatherAt(Position) -> WeatherState
    - GetLightLevelAt(Position) -> byte
    - GetTemperatureAt(Position) -> float

  Done:
    Core Space/Bounds:
    - IsInWorldBounds(Position)
    - IsInChunkBounds(Position)

    Tile/Ground:
    - GetTileAt(Position) -> (material, surface, properties)
    - SetTileAt(Position, material, surface, properties)
    - GetTilesInArea(Position, width, height) -> byte[,]
    - GetGroundProperties(Position) -> (moisture, fertility, etc)
    - SetGroundProperties(Position, properties)

    Chunk Management:
    - GetChunk(Position) -> Chunk?
    - SetChunk(Position, Chunk)

    Entity Spatial:
    - AddEntity(id, pos)
    - RemoveEntity(id)
    - MoveEntity(id, Position[])

    Pathfinding:
    - GetPath(start, end) -> Position[]
    - CanReachPosition(start, end) -> bool

    Zone Operations: (tho not exposed outside world data)
    - GetZonesAt(Position) -> List<Zone>
    - GetZonesInArea(Position, radius) -> List<Zone>
    - UpdateZone(Zone)


# Milestone: Flora and Vegetation
 Goal: Add flora and vegetation to the game world

# Milestone: rendering update

# Milestone: Entities starve and find food, the great famine milestone
 Goal: Entities starve and find food, impliment basic maslow's hierarchy of needs limitied to food and hunger for now

## High priority
- Add food entities
- Add hunger to entities
- Add food consumption
- Add food spawning
- Add food consumption

## Low priority
- Separete entity state to different entries in world.cs


## Done milestones:
# Milestone: Basic stuff
  Goal: Get Entities moving
