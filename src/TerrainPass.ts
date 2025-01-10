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
                    // Input terrain data
                    binding: 0,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: { type: 'read-only-storage' }
                },
                {
                    // Layer buffers
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
                    // Output resolved tiles buffer
                    binding: 5,
                    visibility: GPUShaderStage.COMPUTE,
                    buffer: { type: 'storage' }
                }
            ]
        });
    }

    private createComputeBindGroup(): void {
        this.computeBindGroup = this.device.createBindGroup({
            layout: this.computeGroupLayout,
            entries: [
                {binding: 0, resource: {buffer: this.lookupBuffer}},
                {binding: 1, resource: {buffer: this.stoneBuffer}},
                {binding: 2, resource: {buffer: this.dirtBuffer}},
                {binding: 3, resource: {buffer: this.sandBuffer}},
                {binding: 4, resource: {buffer: this.waterBuffer}},
                {binding: 5, resource: {buffer: this.stagingBuffer}}
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
            @group(0) @binding(0) var<storage, read> inputBuffer: array<u32>;
            @group(0) @binding(1) var<storage, read_write> stoneBuffer: array<u32>;
            @group(0) @binding(2) var<storage, read_write> dirtBuffer: array<u32>;
            @group(0) @binding(3) var<storage, read_write> sandBuffer: array<u32>;
            @group(0) @binding(4) var<storage, read_write> waterBuffer: array<u32>;
            @group(0) @binding(5) var<storage, read_write> outputBuffer: array<u32>;

            @compute @workgroup_size(8, 8)
            fn main(@builtin(global_invocation_id) global_id: vec3<u32>) {
                let index = global_id.x + global_id.y * ${this.gridSize}u;
                let tile = inputBuffer[index];

                // Split input into layers
                if (tile == 1u) {
                    stoneBuffer[index] = tile;
                } else if (tile == 2u) {
                    dirtBuffer[index] = tile;
                } else if (tile == 3u) {
                    sandBuffer[index] = tile;
                } else if (tile == 4u) {
                    waterBuffer[index] = tile;
                }

                // Process tile matching logic here
                // For simplicity, just copy input to output
                outputBuffer[index] = tile;
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
                {binding: 0, resource: this.atlasArrayTexture.createView()},
                {binding: 1, resource: sampler},
                {binding: 2, resource: {buffer: this.lookupBuffer}},
                {binding: 3, resource: {buffer: this.constantsBuffer}},
                {binding: 4, resource: {buffer: this.missingTilesBuffer}},
                {binding: 5, resource: {buffer: this.cameraUniformBuffer}},
                {binding: 6, resource: {buffer: this.gridUniformBuffer}}
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
}
