

@group(0) @binding(6) var<uniform> gridUniforms: GridUniforms;  
@group(0) @binding(2) var<storage> lookupBuffer: array<u32>;   



@compute
fn SplitLayers(@buildin())