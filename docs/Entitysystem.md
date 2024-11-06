# Entity System Design Document

## Core Concepts

### Entities
- Based on interfaces for capabilities (IEntity, IHealth, ICombat, etc.)
- Implemented as record structs for value semantics and performance
- Uses byte flags and simple data types for memory efficiency
- Each entity type only implements interfaces it needs
- No inheritance between entities

### Storage Structure
- EntityChunk as main storage unit
- Position-based lookup system for spatial queries
- Separate type-specific storage arrays for efficient access
- Direct memory access through refs
- Slot management for entity allocation/deallocation
- Index tracking for quick entity lookups

### Event System
- Uses existing EventManager
- Granular, specific events for different actions
- Event queuing without priorities
- All system communication happens through events
- Systems process their own event queues

## Systems

### EntityManager
- Core entity lifecycle management
- Position and spatial tracking
- Entity storage and retrieval
- No game logic
- Handles spawn/remove requests through events (future)

### CombatSystem
- Handles damage calculations
- Manages entity health
- Detects and announces deaths
- Combat state tracking

### MovementSystem 
- Position update validation
- Collision detection
- Path validation
- Movement state management

### ResourceSystem
- Resource state tracking
- Harvest processing
- Resource regeneration
- Availability management

## Design Principles

### Single Responsibility
- Each system handles one specific aspect
- Clear boundaries between systems
- No cross-system logic
- Systems only modify their own data

### Event-Driven Architecture
- All system communication through events
- No direct system-to-system calls
- Events are specific and granular
- Systems react to relevant events only

### State Independence
- Systems maintain only their own state
- No shared state between systems
- Clean separation of concerns
- Data accessed through appropriate interfaces

## Future Considerations

### Entity Lifecycle Events
- Spawn requests through events
- Remove requests through events
- Entity type templates
- Spawn properties and configuration
- Lifecycle state tracking

### Parallel Processing (Optional)
- Worker pool for event processing
- System-level parallelization
- Lock-free event queues
- Performance monitoring

### System Extensions
- New entity types and capabilities
- Additional specialized systems
- Extended event types
- Enhanced interaction patterns

## Integration Points

### Chunk System
- Mirrors existing spatial chunk structure
- Compatible with viewport system
- Efficient spatial queries
- Consistent coordinate system usage

### Network Protocol
- Minimal data transfer
- Entity state synchronization
- Client viewport updates
- Event propagation where needed

## Open Questions & Decisions

### Storage Structure
- Should positions dictionary use IEntity or be type-specific?
- How to handle entities that span multiple positions?
- What's the optimal array size for different entity types?
- Should we pool arrays for reuse?

### Entity Types
- Should EntityType be enum or byte?
- How to handle entity type versioning for future changes?
- Do we need subtyping (like different kinds of resources)?
- How to handle entity type-specific configuration?

### Event Handling
- How to handle event ordering when it matters?
- Should some events carry response expectations?
- How to handle failed entity spawns?
- Should events have validation?

### Performance Considerations
- When should we implement parallel processing?
- How to handle hot spots in spatial queries?
- What's our target entity count per chunk?
- Memory vs CPU tradeoffs in current design?

### Error Cases
- How to handle entity lookup failures?
- What happens to events targeting non-existent entities?
- How to handle invalid position requests?
- Recovery strategies for corrupted entity states?

### Future Expansion
- How to add new entity types without breaking changes?
- Strategy for adding new capabilities/interfaces?
- How to handle saving/loading entity state?
- Migration path for entity format changes?