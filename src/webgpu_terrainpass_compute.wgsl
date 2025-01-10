struct GridUniforms {
    gridSize: u32,
    maxIterations: u32,
}

@group(0) @binding(0) var<uniform> grid: GridUniforms;
@group(0) @binding(1) var<storage, read_write> stoneBuffer: array<u32>;
@group(0) @binding(2) var<storage, read_write> dirtBuffer: array<u32>;
@group(0) @binding(3) var<storage, read_write> sandBuffer: array<u32>;
@group(0) @binding(4) var<storage, read_write> waterBuffer: array<u32>;
@group(0) @binding(5) var<storage, read_write> outputBuffer: array<u32>;

const EDGE_TOP_LEFT: u32     = 0x01u;
const EDGE_TOP: u32         = 0x02u;
const EDGE_TOP_RIGHT: u32   = 0x04u;
const EDGE_RIGHT: u32       = 0x08u;
const EDGE_BOTTOM_RIGHT: u32 = 0x10u;
const EDGE_BOTTOM: u32      = 0x20u;
const EDGE_BOTTOM_LEFT: u32 = 0x40u;
const EDGE_LEFT: u32        = 0x80u;

const TERRAIN_EMPTY: u32 = 0u;
const TERRAIN_STONE: u32 = 1u;
const TERRAIN_DIRT: u32  = 2u;
const TERRAIN_SAND: u32  = 3u;
const TERRAIN_WATER: u32 = 4u;

fn getIndex(x: u32, y: u32) -> u32 {
    return y * grid.gridSize + x;
}

fn isInBounds(x: i32, y: i32) -> bool {
    return x >= 0 && x < i32(grid.gridSize) && 
        y >= 0 && y < i32(grid.gridSize);
}

fn getTerrainAt(x: i32, y: i32) -> u32 {
    if (!isInBounds(x, y)) {
        return TERRAIN_EMPTY;
    }
    return outputBuffer[getIndex(u32(x), u32(y))];
}

fn calculateBitmask(x: u32, y: u32, terrainType: u32) -> u32 {
    var bitmask: u32 = 0u;
    let pos_x = i32(x);
    let pos_y = i32(y);
    
    if (getTerrainAt(pos_x - 1, pos_y - 1) == terrainType) {
        bitmask |= EDGE_TOP_LEFT;
    }
    if (getTerrainAt(pos_x, pos_y - 1) == terrainType) {
        bitmask |= EDGE_TOP;
    }
    if (getTerrainAt(pos_x + 1, pos_y - 1) == terrainType) {
        bitmask |= EDGE_TOP_RIGHT;
    }
    if (getTerrainAt(pos_x + 1, pos_y) == terrainType) {
        bitmask |= EDGE_RIGHT;
    }
    if (getTerrainAt(pos_x + 1, pos_y + 1) == terrainType) {
        bitmask |= EDGE_BOTTOM_RIGHT;
    }
    if (getTerrainAt(pos_x, pos_y + 1) == terrainType) {
        bitmask |= EDGE_BOTTOM;
    }
    if (getTerrainAt(pos_x - 1, pos_y + 1) == terrainType) {
        bitmask |= EDGE_BOTTOM_LEFT;
    }
    if (getTerrainAt(pos_x - 1, pos_y) == terrainType) {
        bitmask |= EDGE_LEFT;
    }
    
    return bitmask;
}

fn calculateTransitionBitmask(x: u32, y: u32, currentType: u32, targetType: u32) -> u32 {
    var bitmask: u32 = 0u;
    let pos_x = i32(x);
    let pos_y = i32(y);
    
    for (var dx = -1; dx <= 1; dx++) {
        for (var dy = -1; dy <= 1; dy++) {
            if (dx == 0 && dy == 0) { continue; }
            
            let checkX = pos_x + dx;
            let checkY = pos_y + dy;
            
            if (!isInBounds(checkX, checkY)) { continue; }
            
            let neighborTerrain = getTerrainAt(checkX, checkY);
            if (neighborTerrain == targetType) {
                let bit = getBitForDirection(dx, dy);
                bitmask |= bit;
            }
        }
    }
    
    return bitmask;
}

fn getBitForDirection(dx: i32, dy: i32) -> u32 {
    if (dx == -1 && dy == -1) { return EDGE_TOP_LEFT; }
    if (dx == 0  && dy == -1) { return EDGE_TOP; }
    if (dx == 1  && dy == -1) { return EDGE_TOP_RIGHT; }
    if (dx == 1  && dy == 0)  { return EDGE_RIGHT; }
    if (dx == 1  && dy == 1)  { return EDGE_BOTTOM_RIGHT; }
    if (dx == 0  && dy == 1)  { return EDGE_BOTTOM; }
    if (dx == -1 && dy == 1)  { return EDGE_BOTTOM_LEFT; }
    if (dx == -1 && dy == 0)  { return EDGE_LEFT; }
    return 0u;
}

@compute @workgroup_size(8, 8)
fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
    let x = global_id.x;
    let y = global_id.y;
    
    if (x >= grid.gridSize || y >= grid.gridSize) {
        return;
    }
    
    let index = getIndex(x, y);
    let currentTerrain = outputBuffer[index];
    
    switch(currentTerrain) {
        case TERRAIN_STONE: {
            stoneBuffer[index] = 1u;
        }
        case TERRAIN_DIRT: {
            dirtBuffer[index] = 1u;
        }
        case TERRAIN_SAND: {
            sandBuffer[index] = 1u;
        }
        case TERRAIN_WATER: {
            waterBuffer[index] = 1u;
        }
        default: {}
    }
    
    var finalBitmask: u32 = 0u;
    
    finalBitmask = calculateBitmask(x, y, currentTerrain);
    
    if (currentTerrain == TERRAIN_STONE) {
        finalBitmask |= calculateTransitionBitmask(x, y, TERRAIN_STONE, TERRAIN_DIRT);
    } else if (currentTerrain == TERRAIN_DIRT) {
        finalBitmask |= calculateTransitionBitmask(x, y, TERRAIN_DIRT, TERRAIN_SAND);
    } else if (currentTerrain == TERRAIN_SAND) {
        finalBitmask |= calculateTransitionBitmask(x, y, TERRAIN_SAND, TERRAIN_WATER);
    }
    
    outputBuffer[index] = (finalBitmask << 8u) | currentTerrain;
}
