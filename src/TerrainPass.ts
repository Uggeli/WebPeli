import { AtlasMetadata } from "./interfaces/IRenderer";
import { TileLookupData } from "./WEBGPURenderer";

export class TerrainPass extends EventTarget {
    private device: GPUDevice;
    private format: GPUTextureFormat;
    private gridSize: number;

    private atlasArrayTexture!: GPUTexture;
    private lookupBuffer!: GPUBuffer; // For the variable per-atlas data
    private constantsBuffer!: GPUBuffer; // For the shared values
    private missingTilesBuffer!: GPUBuffer;
    private stagingBuffer!: GPUBuffer;

    private atlasRegistry: { name: string; index: number; }[] = [];
    private constantsInitialized = false;

    private renderGroupLayout!: GPUBindGroupLayout;
    private renderBindGroup!: GPUBindGroup;
    private renderPipelineLayout!: GPUPipelineLayout;
    private renderPipeline!: GPURenderPipeline;

    private computeGroupLayout!: GPUBindGroupLayout;
    private computeBindGroup!: GPUBindGroup;
    private computePipelineLayout!: GPUPipelineLayout;
    private computePipeline!: GPUComputePipeline;

    // Common buffers
    private cameraUniformBuffer: GPUBuffer;
    private vertexBuffer: GPUBuffer;
    private gridUniformBuffer: GPUBuffer;

    // Layer buffers
    private stoneBuffer!: GPUBuffer;
    private dirtBuffer!: GPUBuffer;
    private sandBuffer!: GPUBuffer;
    private waterBuffer!: GPUBuffer;

    constructor(device: GPUDevice, format: GPUTextureFormat, gridSize: number,
        commonBuffers: { cameraUniformBuffer: GPUBuffer, vertexBuffer: GPUBuffer, gridUniformBuffer: GPUBuffer }) {
        super();
        this.device = device;
        this.format = format;
        this.gridSize = gridSize;

        this.cameraUniformBuffer = commonBuffers.cameraUniformBuffer;
        this.vertexBuffer = commonBuffers.vertexBuffer;
        this.gridUniformBuffer = commonBuffers.gridUniformBuffer;

        this.constantsBuffer = this.device.createBuffer({
            label: 'Terrain Constants',
            size: 4 * 4,
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
        });

        this.initializeTerrainBuffers();
        this.setupRenderPipeline();
        this.setupComputePipeline();

    }

    private initializeTerrainBuffers(): void {
        this.createLayerStorageBuffers();
        this.createSharedUniformBuffers();
        this.createMissingTileTracking();
    }

    private createLayerStorageBuffers(): void {
        this.stoneBuffer = this.createLayerStorageBuffer('Stone Layer');
        this.dirtBuffer = this.createLayerStorageBuffer('Dirt Layer');
        this.sandBuffer = this.createLayerStorageBuffer('Sand Layer');
        this.waterBuffer = this.createLayerStorageBuffer('Water Layer');
    }

    private createLayerStorageBuffer(label: string): GPUBuffer {
        return this.device.createBuffer({
            label: label,
            size: this.gridSize * this.gridSize,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST,
        });
    }

    private createSharedUniformBuffers(): void {
        this.lookupBuffer = this.device.createBuffer({
            label: 'Tile Lookup',
            size: 1024,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST
        });
    }

    private createMissingTileTracking(): void {
        this.missingTilesBuffer = this.device.createBuffer({
            label: 'Missing Tiles',
            size: 32,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST | GPUBufferUsage.COPY_SRC
        });

        this.stagingBuffer = this.device.createBuffer({
            label: 'Staging Buffer',
            size: 32,
            usage: GPUBufferUsage.MAP_READ | GPUBufferUsage.COPY_DST
        });
    }

    private setupRenderPipeline(): void {
        this.createRenderBindGroupLayout();
        this.createRenderBindGroup();
        this.createRenderPipelineLayout();
        this.createRenderPipeline();
    }

    private setupComputePipeline(): void {
        this.createComputeBindGroupLayout();
        this.createComputeBindGroup();
        this.createComputePipelineLayout();
        this.createComputePipeline();
    }

    public async loadTerrainAtlas(name: string, image: ImageBitmap, metadata: AtlasMetadata): Promise<void> {
        if (this.atlasRegistry.length === 0) {
            this.initializeAtlasConstants(metadata);
        }
        await this.addAtlasToArray(name, image);
        this.updateAtlasLookupTable(metadata);
        this.recreateBindGroups();
        return Promise.resolve();
    }

    private async addAtlasToArray(name: string, image: ImageBitmap): Promise<void> {
        const atlasIndex = this.atlasRegistry.length;
        // Grow the texture array if needed
        if (!this.atlasArrayTexture || atlasIndex >= this.atlasArrayTexture.depthOrArrayLayers) {
            const newSize = Math.max(1, this.atlasRegistry.length + 1);

            // Create new larger texture array
            const newTexture = this.device.createTexture({
                size: [image.width, image.height, newSize],
                format: this.format,
                dimension: '2d',
                usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST
            });
            // Create a command encoder to copy existing textures
            const commandEncoder = this.device.createCommandEncoder();
            // Copy existing textures if any
            if (this.atlasArrayTexture) {
                for (let i = 0; i < this.atlasRegistry.length; i++) {
                    commandEncoder.copyTextureToTexture(
                        { texture: this.atlasArrayTexture, origin: [0, 0, i] },
                        { texture: newTexture, origin: [0, 0, i] },
                        [image.width, image.height, 1]
                    );
                }
                this.atlasArrayTexture.destroy();
            }

            this.atlasArrayTexture = newTexture;
        }

        // Add new atlas data
        this.device.queue.copyExternalImageToTexture(
            { source: image },
            { texture: this.atlasArrayTexture, origin: [0, 0, atlasIndex] },
            [image.width, image.height]
        );

        this.atlasRegistry.push({ name, index: atlasIndex });
    }

    private updateAtlasLookupTable(metadata: AtlasMetadata): void {
        // Update lookup buffer with new tile data
        const tileData = metadata.tiles.map(tile => this.createTileLookupEntry({
            bitmask: tile.bitmask,
            atlasIndex: this.atlasRegistry.length - 1,
            x: tile.x,
            y: tile.y
        })
        );

        // Convert the array of tile data into a single Uint8Array
        const flattenedData = new Uint8Array(tileData.length * 4);
        tileData.forEach((value, index) => {
            flattenedData.set(new Uint8Array(value), index * 4);
        });

        // Write to lookup buffer
        this.device.queue.writeBuffer(
            this.lookupBuffer!,
            (this.atlasRegistry.length - 1) * metadata.tiles.length * 4, // offset for this atlas
            flattenedData
        );
    }

    // Tile processing methods
    private createTileLookupEntry(tile: TileLookupData): Uint8Array {
        const data = new Uint8Array(4);
        data[0] = tile.bitmask;
        data[1] = tile.atlasIndex;
        data[2] = tile.x;
        data[3] = tile.y;
        return data;
    }

    private recreateBindGroups(): void {
        this.createRenderBindGroup();
        this.createComputeBindGroup();
    }

    private initializeAtlasConstants(metadata: AtlasMetadata): void {
        if (!this.constantsInitialized) {
            const constants = new Float32Array([
                metadata.base.tileWidth,
                metadata.base.tileHeight,
                metadata.base.textureWidth,
                metadata.base.textureHeight,
            ]);
            this.device.queue.writeBuffer(this.constantsBuffer, 0, constants);
            this.constantsInitialized = true;
        }
    }

    public async detectMissingTiles(): Promise<void> {
        await this.copyMissingTilesToStaging();
        const missingTiles = await this.readMissingTilesFromStaging();
        if (missingTiles.length > 0) {
            this.notifyMissingTiles(missingTiles);
            this.clearMissingTilesBuffer();
        }
    }

    private async readMissingTilesFromStaging(): Promise<number[]> {
        await this.stagingBuffer.mapAsync(GPUMapMode.READ);
        const data = new Uint8Array(this.stagingBuffer.getMappedRange());
        const missingTiles: number[] = [];
        for (let byte = 0; byte < 32; byte++) {
            for (let bit = 0; bit < 8; bit++) {
                if (data[byte] & (1 << bit)) {
                    missingTiles.push(byte * 8 + bit);
                }
            }
        }
        this.stagingBuffer.unmap();
        return missingTiles;
    }

    private notifyMissingTiles(missingTiles: number[]): void {
        this.dispatchEvent(new CustomEvent('missingtiles', {
            detail: missingTiles
        }));
    }

    private clearMissingTilesBuffer(): void {
        this.device.queue.writeBuffer(
            this.missingTilesBuffer,
            0,
            new Uint8Array(32)
        );
    }


    private async copyMissingTilesToStaging(): Promise<void> {
        const commandEncoder = this.device.createCommandEncoder();
        commandEncoder.copyBufferToBuffer(
            this.missingTilesBuffer, 0,
            this.stagingBuffer, 0,
            32
        );
        this.device.queue.submit([commandEncoder.finish()]);
    }

    private createComputeBindGroupLayout(): void {
        this.computeGroupLayout = this.device.createBindGroupLayout({
            entries: [
                {
                    // Grid uniform buffer (contains terrain data)
                    binding: 0,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: { type: 'uniform' }
                },
                {
                    // Layer output buffers
                    binding: 1,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: { type: 'storage' }  // stone
                },
                {
                    binding: 2,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: { type: 'storage' }  // dirt
                },
                {
                    binding: 3,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: { type: 'storage' }  // sand
                },
                {
                    binding: 4,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: { type: 'storage' }  // water
                },
                {
                    binding: 5,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: { type: 'storage' }  // output buffer for results
                },
                {
                    binding: 6,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: {}
                }
            ]
        });
    }

    private createComputeBindGroup(): void {
        this.computeBindGroup = this.device.createBindGroup({
            layout: this.computeGroupLayout,
            entries: [
                { binding: 0, resource: { buffer: this.lookupBuffer } },
                { binding: 1, resource: { buffer: this.stoneBuffer } },
                { binding: 2, resource: { buffer: this.dirtBuffer } },
                { binding: 3, resource: { buffer: this.sandBuffer } },
                { binding: 4, resource: { buffer: this.waterBuffer } },
                { binding: 5, resource: { buffer: this.stagingBuffer } }
            ]
        });
    }

    private createComputePipelineLayout(): void {
        this.computePipelineLayout = this.device.createPipelineLayout({
            bindGroupLayouts: [this.computeGroupLayout]
        });
    }

    private createComputePipeline(): void {
        const computeShaderCode = `
            // Define our structures and bindings
            struct TerrainConstants {
                gridSize: u32,
                maxIterations: u32,
            }

            @group(0) @binding(0) var<uniform> grid: GridUniforms;
            @group(0) @binding(1) var<storage, read_write> stoneBuffer: array<u32>;
            @group(0) @binding(2) var<storage, read_write> dirtBuffer: array<u32>;
            @group(0) @binding(3) var<storage, read_write> sandBuffer: array<u32>;
            @group(0) @binding(4) var<storage, read_write> waterBuffer: array<u32>;
            @group(0) @binding(5) var<storage, read_write> outputBuffer: array<u32>;

            // Bitmask constants for edge detection
            const EDGE_TOP_LEFT: u32     = 0x01u;  // 0b00000001
            const EDGE_TOP: u32         = 0x02u;  // 0b00000010
            const EDGE_TOP_RIGHT: u32   = 0x04u;  // 0b00000100
            const EDGE_RIGHT: u32       = 0x08u;  // 0b00001000
            const EDGE_BOTTOM_RIGHT: u32 = 0x10u;  // 0b00010000
            const EDGE_BOTTOM: u32      = 0x20u;  // 0b00100000
            const EDGE_BOTTOM_LEFT: u32 = 0x40u;  // 0b01000000
            const EDGE_LEFT: u32        = 0x80u;  // 0b10000000

            // Terrain type constants
            const TERRAIN_EMPTY: u32 = 0u;
            const TERRAIN_STONE: u32 = 1u;
            const TERRAIN_DIRT: u32  = 2u;
            const TERRAIN_SAND: u32  = 3u;
            const TERRAIN_WATER: u32 = 4u;

            // Helper function to get grid index
            fn getIndex(x: u32, y: u32) -> u32 {
                return y * constants.gridSize + x;
            }

            // Check if coordinates are within grid bounds
            fn isInBounds(x: i32, y: i32) -> bool {
                return x >= 0 && x < i32(constants.gridSize) && 
                    y >= 0 && y < i32(constants.gridSize);
            }

            // Get terrain type at specific coordinates
            fn getTerrainAt(x: i32, y: i32) -> u32 {
                if (!isInBounds(x, y)) {
                    return TERRAIN_EMPTY;
                }
                return inputBuffer[getIndex(u32(x), u32(y))];
            }

            // Calculate bitmask for a cell based on surrounding terrain
            fn calculateBitmask(x: u32, y: u32, terrainType: u32) -> u32 {
                var bitmask: u32 = 0u;
                let pos_x = i32(x);
                let pos_y = i32(y);
                
                // Check each neighboring cell
                // Top-left
                if (getTerrainAt(pos_x - 1, pos_y - 1) == terrainType) {
                    bitmask |= EDGE_TOP_LEFT;
                }
                // Top
                if (getTerrainAt(pos_x, pos_y - 1) == terrainType) {
                    bitmask |= EDGE_TOP;
                }
                // Top-right
                if (getTerrainAt(pos_x + 1, pos_y - 1) == terrainType) {
                    bitmask |= EDGE_TOP_RIGHT;
                }
                // Right
                if (getTerrainAt(pos_x + 1, pos_y) == terrainType) {
                    bitmask |= EDGE_RIGHT;
                }
                // Bottom-right
                if (getTerrainAt(pos_x + 1, pos_y + 1) == terrainType) {
                    bitmask |= EDGE_BOTTOM_RIGHT;
                }
                // Bottom
                if (getTerrainAt(pos_x, pos_y + 1) == terrainType) {
                    bitmask |= EDGE_BOTTOM;
                }
                // Bottom-left
                if (getTerrainAt(pos_x - 1, pos_y + 1) == terrainType) {
                    bitmask |= EDGE_BOTTOM_LEFT;
                }
                // Left
                if (getTerrainAt(pos_x - 1, pos_y) == terrainType) {
                    bitmask |= EDGE_LEFT;
                }
                
                return bitmask;
            }

            // Handle transitions between different terrain types
            fn calculateTransitionBitmask(x: u32, y: u32, currentType: u32, targetType: u32) -> u32 {
                var bitmask: u32 = 0u;
                let pos_x = i32(x);
                let pos_y = i32(y);
                
                // Check each direction for the target terrain type
                // This creates smooth transitions between different terrain types
                for (var dx = -1; dx <= 1; dx++) {
                    for (var dy = -1; dy <= 1; dy++) {
                        if (dx == 0 && dy == 0) { continue; }
                        
                        let checkX = pos_x + dx;
                        let checkY = pos_y + dy;
                        
                        if (!isInBounds(checkX, checkY)) { continue; }
                        
                        let neighborTerrain = getTerrainAt(checkX, checkY);
                        if (neighborTerrain == targetType) {
                            // Calculate bit position based on direction
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

            // Main compute shader
            @compute @workgroup_size(8, 8)
            fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
                let x = global_id.x;
                let y = global_id.y;
                
                // Check bounds
                if (x >= constants.gridSize || y >= constants.gridSize) {
                    return;
                }
                
                let index = getIndex(x, y);
                let currentTerrain = inputBuffer[index];
                
                // First pass: Separate into layers
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
                
                // Second pass: Calculate bitmasks for each layer
                var finalBitmask: u32 = 0u;
                
                // Base terrain bitmask
                finalBitmask = calculateBitmask(x, y, currentTerrain);
                
                // Calculate transition bitmasks for adjacent terrain types
                if (currentTerrain == TERRAIN_STONE) {
                    finalBitmask |= calculateTransitionBitmask(x, y, TERRAIN_STONE, TERRAIN_DIRT);
                } else if (currentTerrain == TERRAIN_DIRT) {
                    finalBitmask |= calculateTransitionBitmask(x, y, TERRAIN_DIRT, TERRAIN_SAND);
                } else if (currentTerrain == TERRAIN_SAND) {
                    finalBitmask |= calculateTransitionBitmask(x, y, TERRAIN_SAND, TERRAIN_WATER);
                }
                
                // Store final result
                // Pack terrain type and bitmask into a single u32
                // Lower 8 bits: terrain type
                // Upper 24 bits: bitmask
                outputBuffer[index] = (finalBitmask << 8u) | currentTerrain;
            }
        `;

        const computeShaderModule = this.device.createShaderModule({
            code: computeShaderCode
        });

        this.computePipeline = this.device.createComputePipeline({
            layout: this.computePipelineLayout,
            compute: {
                module: computeShaderModule,
                entryPoint: 'main'
            }
        });
    }

    private createRenderBindGroupLayout(): void {
        this.renderGroupLayout = this.device.createBindGroupLayout({
            label: 'TerrainPass BindGroupLayout',
            entries: [
                {
                    // Atlas texture array
                    binding: 0,
                    visibility: GPUShaderStage.FRAGMENT,
                    texture: {
                        sampleType: 'float',
                        viewDimension: '2d-array',
                    }
                },
                {
                    // Sampler for the texture
                    binding: 1,
                    visibility: GPUShaderStage.FRAGMENT,
                    sampler: {
                        type: 'filtering'
                    }
                },
                {
                    // Lookup buffer (storage)
                    binding: 2,
                    visibility: GPUShaderStage.FRAGMENT,
                    buffer: {
                        type: 'read-only-storage'
                    }
                },
                {
                    // Constants buffer (uniforms)
                    binding: 3,
                    visibility: GPUShaderStage.FRAGMENT,
                    buffer: {
                        type: 'uniform'
                    }
                },
                {
                    // Missing tiles buffer (storage)
                    binding: 4,
                    visibility: GPUShaderStage.FRAGMENT,
                    buffer: {
                        type: 'storage'
                    }
                },
                {
                    // Camera uniform buffer
                    binding: 5,
                    visibility: GPUShaderStage.VERTEX,
                    buffer: {
                        type: 'uniform'
                    }
                },
                {
                    // Grid uniform buffer
                    binding: 6,
                    visibility: GPUShaderStage.FRAGMENT,
                    buffer: {
                        type: 'uniform'
                    }
                }
            ]
        }) as GPUBindGroupLayout;
    }

    private createRenderBindGroup(): void {
        const sampler = this.device.createSampler({
            magFilter: 'nearest',
            minFilter: 'nearest',
            mipmapFilter: 'nearest',
        });

        this.renderBindGroup = this.device.createBindGroup({
            label: 'TerrainPass BindGroup',
            layout: this.renderGroupLayout,
            entries: [
                { binding: 0, resource: this.atlasArrayTexture.createView() },
                { binding: 1, resource: sampler },
                { binding: 2, resource: { buffer: this.lookupBuffer } },
                { binding: 3, resource: { buffer: this.constantsBuffer } },
                { binding: 4, resource: { buffer: this.missingTilesBuffer } },
                { binding: 5, resource: { buffer: this.cameraUniformBuffer } },
                { binding: 6, resource: { buffer: this.gridUniformBuffer } }
            ]
        });
    }

    private createRenderPipelineLayout(): void {
        this.renderPipelineLayout = this.device.createPipelineLayout({
            bindGroupLayouts: [this.renderGroupLayout]
        });
    }

    private createRenderPipeline(): void {
        const vertexShaderCode = `
            struct VertexOutput {
                @builtin(position) position: vec4f,
                @location(0) texCoord: vec2f,
                @location(1) @interpolate(flat) tileId: u32,
            };

            struct CameraUniform {
                view: mat4x4<f32>,
                proj: mat4x4<f32>
            };

            @group(0) @binding(0) var<uniform> grid: vec2f;
            @group(0) @binding(1) var<uniform> camera: CameraUniform;
            @group(0) @binding(2) var<storage, read> tileData: array<u32>;
            @group(0) @binding(3) var atlas: texture_2d<f32>;
            @group(0) @binding(4) var atlasSampler: sampler;

            @vertex
            fn vertexMain(
                @location(0) position: vec3f,
                @builtin(instance_index) instance: u32
            ) -> VertexOutput {
                var output: VertexOutput;
                
                let x = f32(instance % u32(grid.x));
                let y = f32(instance / u32(grid.x));
                
                let worldPos = vec3f(
                    position.x + x,
                    position.y + y,
                    0.0
                );
                
                output.position = camera.proj * camera.view * vec4f(worldPos, 1.0);
                output.tileId = tileData[instance];
                
                let atlasSize = 4.0;
                let tileX = f32(output.tileId % 4u);
                let tileY = f32(output.tileId / 4u);
                
                output.texCoord = vec2f(
                    (tileX + position.x) / atlasSize,
                    (tileY + position.y) / atlasSize
                );
                
                return output;
            }
        `;

        const fragmentShaderCode = `
            @fragment
            fn fragmentMain(input: VertexOutput) -> @location(0) vec4f {
                return textureSample(atlas, atlasSampler, input.texCoord);
            }
        `;

        const vertexShaderModule = this.device.createShaderModule({
            code: vertexShaderCode
        });

        const fragmentShaderModule = this.device.createShaderModule({
            code: fragmentShaderCode
        });

        this.renderPipeline = this.device.createRenderPipeline({
            layout: this.renderPipelineLayout,
            vertex: {
                module: vertexShaderModule,
                entryPoint: 'vertexMain',
                buffers: [{
                    arrayStride: 12,
                    attributes: [{
                        format: "float32x3",
                        offset: 0,
                        shaderLocation: 0,
                    }]
                }]
            },
            fragment: {
                module: fragmentShaderModule,
                entryPoint: 'fragmentMain',
                targets: [{
                    format: this.format,
                    blend: {
                        color: {
                            srcFactor: 'src-alpha',
                            dstFactor: 'one-minus-src-alpha',
                            operation: 'add',
                        },
                        alpha: {
                            srcFactor: 'one',
                            dstFactor: 'one-minus-src-alpha',
                            operation: 'add',
                        },
                    },
                }]
            },
            primitive: {
                topology: 'triangle-list',
                cullMode: 'none',
            },
        });
    }

    public async render(passEncoder: GPURenderPassEncoder): Promise<void> {
        // Step 1: Run compute pass for layer separation and tile processing
        const computeEncoder = this.device.createCommandEncoder();
        const computePass = computeEncoder.beginComputePass();

        computePass.setPipeline(this.computePipeline);
        computePass.setBindGroup(0, this.computeBindGroup);

        this.processLayers(computePass);

        computePass.end();
        this.device.queue.submit([computeEncoder.finish()]);

        // Step 2: Run render pass for terrain visualization
        passEncoder.setPipeline(this.renderPipeline);
        passEncoder.setBindGroup(0, this.renderBindGroup);
        passEncoder.setVertexBuffer(0, this.vertexBuffer);

        // Draw instanced quads - 6 vertices per quad (2 triangles), one quad per tile
        passEncoder.draw(6, this.gridSize * this.gridSize, 0, 0);

        // Step 3: Check for missing tiles after rendering
        await this.detectMissingTiles();
    }

    // Helper method to handle layers in order
    private processLayers(computePass: GPUComputePassEncoder): void {
        // Process each terrain layer in order: stone -> dirt -> sand -> water
        // This ordering matters for proper blending and transitions
        computePass.setBindGroup(1, this.createLayerBindGroup('stone'));
        computePass.dispatchWorkgroups(Math.ceil(this.gridSize / 8), Math.ceil(this.gridSize / 8));

        computePass.setBindGroup(1, this.createLayerBindGroup('dirt'));
        computePass.dispatchWorkgroups(Math.ceil(this.gridSize / 8), Math.ceil(this.gridSize / 8));

        computePass.setBindGroup(1, this.createLayerBindGroup('sand'));
        computePass.dispatchWorkgroups(Math.ceil(this.gridSize / 8), Math.ceil(this.gridSize / 8));

        computePass.setBindGroup(1, this.createLayerBindGroup('water'));
        computePass.dispatchWorkgroups(Math.ceil(this.gridSize / 8), Math.ceil(this.gridSize / 8));
    }

    // Helper to create layer-specific bind groups
    private createLayerBindGroup(layerType: 'stone' | 'dirt' | 'sand' | 'water'): GPUBindGroup {
        const buffer = {
            'stone': this.stoneBuffer,
            'dirt': this.dirtBuffer,
            'sand': this.sandBuffer,
            'water': this.waterBuffer
        }[layerType];

        return this.device.createBindGroup({
            layout: this.computeGroupLayout,
            entries: [
                { binding: 0, resource: { buffer } }
                // Add other bindings as needed for layer-specific processing
            ]
        });
    }

    // Error handling wrapper for render method
    public async safeRender(passEncoder: GPURenderPassEncoder): Promise<void> {
        try {
            await this.render(passEncoder);
        } catch (error) {
            console.error('Error during terrain pass render:', error);
            // Emit error event for higher-level handling
            this.dispatchEvent(new CustomEvent('error', {
                detail: {
                    message: 'Terrain pass render failed',
                    error
                }
            }));
            throw error; // Re-throw for pipeline error handling
        }
    }
}
