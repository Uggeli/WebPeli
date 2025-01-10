import { RendererStatus } from "./IRenderer";
export class WebGPURenderer {
    constructor(gridSize, tileSize) {
        // Initialize the renderer
        this.status = RendererStatus.Uninitialized;
        this.lastError = null;
        this.gridSize = gridSize;
        this.tileSize = tileSize;
    }
    updateTileMaterialData(data) {
        throw new Error("Method not implemented.");
    }
    updateTileSurfaceData(data) {
        throw new Error("Method not implemented.");
    }
    updateEntityData(data) {
        throw new Error("Method not implemented.");
    }
    async initDevice() {
        try {
            const adapter = await navigator.gpu.requestAdapter({
                powerPreference: 'high-performance'
            });
            if (!adapter) {
                throw new Error('No GPU adapter found');
            }
            return await adapter.requestDevice();
        }
        catch (error) {
            throw error;
        }
    }
    async init() {
        try {
            this.status = RendererStatus.Initializing;
            // Initialize GPU device
            this.device = await this.initDevice();
            this.canvas = document.querySelector('canvas');
            this.context = this.canvas.getContext('webgpu');
            // Configure canvas
            const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
            this.context.configure({
                device: this.device,
                format: canvasFormat,
                alphaMode: 'premultiplied',
            });
            // Initialize passes
            this.terrainPass = new TerrainPass(this.device, canvasFormat, this.gridSize);
            this.surfacePass = new SurfacePass(this.device, canvasFormat, this.gridSize);
            this.entityPass = new EntityPass(this.device, canvasFormat, this.gridSize);
            this.lightingPass = new LightingPass(this.device, canvasFormat, this.gridSize);
            this.postProcessPass = new PostProcessPass(this.device, canvasFormat, this.gridSize);
            this.status = RendererStatus.Ready;
        }
        catch (error) {
            this.status = RendererStatus.Failed;
            this.lastError = {
                code: 1000,
                message: 'Failed to initialize WebGPU renderer',
                details: error
            };
            throw error;
        }
    }
    dispose() {
        throw new Error("Method not implemented.");
    }
    setup(config) {
        throw new Error("Method not implemented.");
    }
    loadAtlas(name, image, metadata) {
        throw new Error("Method not implemented.");
    }
    unloadAtlas(name) {
        throw new Error("Method not implemented.");
    }
    addLayer(layerId, config) {
        throw new Error("Method not implemented.");
    }
    removeLayer(layerId) {
        throw new Error("Method not implemented.");
    }
    setLayerOrder(layerId, order) {
        throw new Error("Method not implemented.");
    }
    draw() {
        throw new Error("Method not implemented.");
    }
    handleResize() {
        throw new Error("Method not implemented.");
    }
    on(event, callback) {
        throw new Error("Method not implemented.");
    }
    off(event, callback) {
        throw new Error("Method not implemented.");
    }
}
class TerrainPass extends EventTarget {
    constructor(device, format, gridSize) {
        super();
        this.atlasRegistry = [];
        this.constantsInitialized = false;
        this.device = device;
        this.format = format;
        this.gridSize = gridSize;
        this.constantsBuffer = this.device.createBuffer({
            size: 4 * 4,
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST
        });
        this.lookupBuffer = this.device.createBuffer({
            size: 1024, // ought to be enough for anyone 640k bits should be enough for anyone!! 
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST
        });
        ;
        this.missingTilesBuffer = device.createBuffer({
            size: 32, // 256 bits = 32 bytes
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST | GPUBufferUsage.COPY_SRC
        });
        this.stagingBuffer = device.createBuffer({
            size: 32,
            usage: GPUBufferUsage.MAP_READ | GPUBufferUsage.COPY_DST
        });
        this.bindGroupLayout = device.createBindGroupLayout({
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
                }
            ]
        });
    }
    initializeConstantsBuffer(metadata) {
        if (!this.constantsInitialized) {
            this.device.queue.writeBuffer(this.constantsBuffer, 0, new Float32Array([
                metadata.base.tileWidth,
                metadata.base.tileHeight,
                metadata.base.textureWidth,
                metadata.base.textureHeight,
            ]));
            this.constantsInitialized = true;
        }
    }
    createLookupBuffer(size) {
        // Each tile entry is 4 bytes (8 bits * 4)
        const bufferSize = size * 4;
        return this.device.createBuffer({
            size: bufferSize,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST
        });
    }
    packTileLookupData(tile) {
        // Pack into a single 32-bit value
        const data = new Uint8Array(4);
        data[0] = tile.bitmask;
        data[1] = tile.atlasIndex;
        data[2] = tile.x;
        data[3] = tile.y;
        return data;
    }
    loadAtlas(name, image, metadata) {
        if (this.atlasRegistry.length === 0) {
            this.initializeConstantsBuffer(metadata);
        }
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
                    commandEncoder.copyTextureToTexture({ texture: this.atlasArrayTexture, origin: [0, 0, i] }, { texture: newTexture, origin: [0, 0, i] }, [image.width, image.height, 1]);
                }
                this.atlasArrayTexture.destroy();
            }
            this.atlasArrayTexture = newTexture;
        }
        // Add new atlas data
        this.device.queue.copyExternalImageToTexture({ source: image }, { texture: this.atlasArrayTexture, origin: [0, 0, atlasIndex] }, [image.width, image.height]);
        // Update lookup buffer with new tile data
        const tileData = metadata.tiles.map(tile => this.packTileLookupData({
            bitmask: tile.bitmask,
            atlasIndex: atlasIndex,
            x: tile.x,
            y: tile.y
        }));
        // Convert the array of tile data into a single Uint8Array
        const flattenedData = new Uint8Array(tileData.length * 4);
        tileData.forEach((value, index) => {
            flattenedData.set(new Uint8Array(value), index * 4);
        });
        // Write to lookup buffer
        this.device.queue.writeBuffer(this.lookupBuffer, atlasIndex * metadata.tiles.length * 4, // offset for this atlas
        flattenedData);
        this.atlasRegistry.push({ name, index: atlasIndex });
        this.createBindGroup();
        return Promise.resolve();
    }
    async checkMissingTilesAfterRender() {
        const commandEncoder = this.device.createCommandEncoder();
        commandEncoder.copyBufferToBuffer(this.missingTilesBuffer, 0, this.stagingBuffer, 0, 32);
        this.device.queue.submit([commandEncoder.finish()]);
        await this.stagingBuffer.mapAsync(GPUMapMode.READ);
        const data = new Uint8Array(this.stagingBuffer.getMappedRange());
        // If we find any bits set...
        if (data.some(byte => byte !== 0)) {
            const missingTiles = [];
            for (let byte = 0; byte < 32; byte++) {
                for (let bit = 0; bit < 8; bit++) {
                    if (data[byte] & (1 << bit)) {
                        missingTiles.push(byte * 8 + bit);
                    }
                }
            }
            // Emit event with missing tiles
            this.dispatchEvent(new CustomEvent('missingtiles', {
                detail: missingTiles
            }));
            // Clear the buffer for next frame
            this.device.queue.writeBuffer(this.missingTilesBuffer, 0, new Uint8Array(32) // Zeros
            );
        }
        this.stagingBuffer.unmap();
    }
    createBindGroup() {
        // Create a sampler if you haven't already
        const sampler = this.device.createSampler({
            magFilter: 'nearest',
            minFilter: 'nearest',
            mipmapFilter: 'nearest',
        });
        this.bindGroup = this.device.createBindGroup({
            layout: this.bindGroupLayout,
            entries: [
                {
                    binding: 0,
                    resource: this.atlasArrayTexture.createView()
                },
                {
                    binding: 1,
                    resource: sampler
                },
                {
                    binding: 2,
                    resource: {
                        buffer: this.lookupBuffer
                    }
                },
                {
                    binding: 3,
                    resource: {
                        buffer: this.constantsBuffer
                    }
                },
                {
                    binding: 4,
                    resource: {
                        buffer: this.missingTilesBuffer
                    }
                }
            ]
        });
    }
}
class SurfacePass {
    constructor(device, format, gridSize) {
    }
}
class EntityPass {
    constructor(device, format, gridSize) {
    }
}
class LightingPass {
    constructor(device, format, gridSize) {
    }
}
class PostProcessPass {
    constructor(device, format, gridSize) {
    }
}
