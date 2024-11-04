# WebPeli Development TODOs

## Priority 1: Entity System Implementation
- [x] Define core entity interfaces (IEntity, IHealth, ICombat, etc.)
- [x] Implement EntityChunk storage structure
- [x] Create position-based lookup system
- [x] Set up entity lifecycle management
- [x] Integrate with existing event system
- [x] Add entity spawning/removal events
- [x] Implement basic entity types
- [ ] Implement entity type templates
- [ ] Add spawn properties and configuration
- [ ] Implement lifecycle state tracking

## Priority 2: Core Systems Enhancement

### Chunk System Refactoring
- [ ] Split tile data into purpose-specific bytes:
  - Properties byte (type, traversable, transparent)
  - Texture byte (for rendering)
  - Future expansion byte (TBD)
- [ ] Move chunk connectivity to MapManager level
- [ ] Optimize pathfinding implementation
  - [ ] Custom fixed-size priority queue
  - [ ] Use `stackalloc` for temp buffers
  - [ ] Pre-compute common paths
  - [ ] More efficient exit building

### Texture System Enhancements
- [ ] Implement layered texture system:
  - [ ] Base terrain textures
  - [ ] Doodad/decoration layer
  - [ ] Environmental effect layer (wet, burning, etc)
  - [ ] Overlay layer (snow, blood, etc)
  - [ ] Support for dynamic lighting with layered textures
- [ ] Add texture variation system for natural look
- [ ] Support for seasonal changes
- [ ] Damage/state visualization

## Priority 3: Game Systems
- [ ] CombatSystem implementation
- [ ] MovementSystem with collision
- [ ] ResourceSystem for gathering/harvesting
- [ ] Basic AI system
- [ ] Interaction system
- [ ] Implement needs system based on physiological needs
- [ ] Implement goal system tied to needs
- [ ] Implement basic world interaction for AI
- [ ] Implement core decision-making logic for AI

## Priority 4: AI Systems
- [ ] Phase 1: Foundation
  - [ ] Basic needs system (physiological needs)
  - [ ] Simple goal selection based on needs
  - [ ] Core decision-making logic
  - [ ] Basic world interaction capabilities
- [ ] Phase 2: Enhancement
  - [ ] Implement memory system (episodic, semantic, procedural)
  - [ ] Add learning capabilities
  - [ ] Integrate personality traits into AI behavior
  - [ ] Enhance social interaction between AI actors
- [ ] Phase 3: Advanced Features
  - [ ] Develop emotion system
  - [ ] Implement complex emotional modeling
  - [ ] Support long-term planning
  - [ ] Introduce group dynamics and cooperation

## Priority 5: Network & Client Updates
- [x] Entity state synchronization protocol
- [x] Enhanced viewport data format for entities
- [x] Client-side entity rendering
- [x] Network optimization for entity updates

## Priority 6: Performance Optimization
- [ ] Profile and optimize pathfinding
- [ ] Implement spatial partitioning for entities
- [ ] Memory usage optimization
- [ ] Consider parallel processing where beneficial
- [ ] Optimize update scheduling for AI calculations
- [ ] Implement decision caching for AI systems

## Priority 7: Future Features
- [ ] Save/load system
- [ ] Enhanced environmental effects
- [ ] More entity types and interactions
- [ ] Design modding support and extensibility features
- [ ] Implement advanced AI behaviors
- [ ] Implement learning and adaptation in AI

## Questions & Decisions Needed
- How to handle entity state serialization?
- Best approach for entity-terrain interaction?
- Strategy for handling large numbers of entities?
- How to manage texture asset loading?
- Approach for handling cross-chunk entity pathfinding?
- Optimal update frequency for different systems?
- How to balance realism and performance in AI calculations?
- Best approach for knowledge representation in AI?
- How to handle cross-actor learning?
- Serialization strategy for AI state?

## Notes & Ideas
- Consider using bitfields for texture layers to pack more info
- May need to implement texture caching for performance
- Look into using object pools for frequently created/destroyed entities
- Consider implementing debug visualization tools
- Think about how to handle dynamic lighting with layered textures
- Start simple with AI systems and add complexity gradually
- Focus on measurable behaviors in AI development
- Keep performance in mind when designing AI systems
- Document all subsystems clearly