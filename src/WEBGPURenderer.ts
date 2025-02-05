import { IRenderer, RendererStatus, RendererConfig, AtlasMetadata, LayerConfig, RendererError } from "./interfaces/IRenderer";
import { mat4 } from "gl-matrix";
import { TerrainPass } from "./TerrainPass";
import { SurfacePass } from "./SurfacePass";
import { EntityPass } from "./EntityPass";
import { LightingPass } from "./LightingPass";
import { PostProcessPass } from "./PostProcessPass";

export class WebGPURenderer implements IRenderer {
    private device!: GPUDevice;
    private context!: GPUCanvasContext;
    private canvas!: HTMLCanvasElement;

    // Passes
    private terrainPass!: TerrainPass;
    private surfacePass!: SurfacePass;
    private entityPass!: EntityPass;
    private lightingPass!: LightingPass;
    private postProcessPass!: PostProcessPass;

    private gridSize: number;
    private tileSize: number;

    // Common buffers
    private cameraUniformBuffer!: GPUBuffer;
    private vertexBuffer!: GPUBuffer;
    private gridUniformBuffer!: GPUBuffer;

    private viewMatrix: mat4;
    private projectionMatrix: mat4;
    

    constructor(gridSize: number, tileSize: number, canvas: HTMLCanvasElement, ) {
        // Initialize the renderer
        this.status = RendererStatus.Uninitialized;
        this.lastError = null;
        this.gridSize = gridSize;
        this.tileSize = tileSize;
        this.canvas = canvas;

        this.viewMatrix = mat4.create();
        this.projectionMatrix = mat4.create();

        const cameraDistance = 1;
        const cameraHeight = 5;

        mat4.lookAt(
            this.viewMatrix,
            [gridSize / 2, -cameraDistance, cameraHeight],
            [gridSize / 2, gridSize / 2, 0],
            [0, 0, 1]
        );

        mat4.perspective(
            this.projectionMatrix,
            45 * Math.PI / 180,
            this.canvas.width / this.canvas.height,
            0.1,
            cameraDistance
        );
    }

    updateTileMaterialData(data: Uint8Array): void {
        throw new Error("Method not implemented.");
    }
    updateTileSurfaceData(data: Uint8Array): void {
        throw new Error("Method not implemented.");
    }
    updateEntityData(data: Uint8Array): void {
        throw new Error("Method not implemented.");
    }

    async initDevice(): Promise<GPUDevice> {
        try {
            const adapter = await navigator.gpu.requestAdapter({
                powerPreference: 'high-performance'
            });
            if (!adapter) {
                throw new Error('No GPU adapter found');
            }
            return await adapter.requestDevice();
        } catch (error) {
            throw error;
        }
    }

    async init(): Promise<void> {
        try {
            this.status = RendererStatus.Initializing;

            // Initialize GPU device
            this.device = await this.initDevice();
            this.context = this.canvas.getContext('webgpu') as GPUCanvasContext;

            // Configure canvas
            const canvasFormat = navigator.gpu.getPreferredCanvasFormat();
            this.context.configure({
                device: this.device,
                format: canvasFormat,
                alphaMode: 'premultiplied',
            });

            // Initialize passes
            this.terrainPass = new TerrainPass(this.device, canvasFormat, this.gridSize,
                { cameraUniformBuffer: this.cameraUniformBuffer, vertexBuffer: this.vertexBuffer, gridUniformBuffer: this.gridUniformBuffer }
            );
            this.surfacePass = new SurfacePass(this.device, canvasFormat, this.gridSize);
            this.entityPass = new EntityPass(this.device, canvasFormat, this.gridSize);
            this.lightingPass = new LightingPass(this.device, canvasFormat, this.gridSize);
            this.postProcessPass = new PostProcessPass(this.device, canvasFormat, this.gridSize);
            this.status = RendererStatus.Ready;

            const vertices = new Float32Array([
                0.0, 0.0, 0.0,  // Bottom left
                1.0, 0.0, 0.0,  // Bottom right
                1.0, 1.0, 0.0,  // Top right
                0.0, 0.0, 0.0,  // Bottom left
                1.0, 1.0, 0.0,  // Top right
                0.0, 1.0, 0.0   // Top left
            ]);

            this.vertexBuffer = this.device.createBuffer({
                label: "Cell vertices",
                size: vertices.byteLength,
                usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
                mappedAtCreation: true,
            });
            new Float32Array(this.vertexBuffer.getMappedRange()).set(vertices);
            this.vertexBuffer.unmap();

            const cameraUniformBufferSize = 2 * 4 * 16;
            this.cameraUniformBuffer = this.device.createBuffer({
                label: "Camera Uniform Buffer",
                size: cameraUniformBufferSize,
                usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST
            });
            this.device.queue.writeBuffer(this.cameraUniformBuffer, 0, this.viewMatrix as Float32Array);
            this.device.queue.writeBuffer(this.cameraUniformBuffer, 64, this.projectionMatrix as Float32Array);

            const uniformArray = new Float32Array([this.gridSize, this.tileSize]);
            this.gridUniformBuffer = this.device.createBuffer({
                label: "Grid Uniform Buffer",
                size: uniformArray.byteLength,
                usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST
            });
            this.device.queue.writeBuffer(this.gridUniformBuffer, 0, uniformArray);

            // Initialize the new compute pipeline in TerrainPass
            this.terrainPass.setupComputePipeline();
        } catch (error) {
            this.status = RendererStatus.Failed;
            this.lastError = {
                code: 1000,
                message: 'Failed to initialize WebGPU renderer',
                details: error
            };
            throw error;
        }
    }

    dispose(): void {
        throw new Error("Method not implemented.");
    }
    setup(config: RendererConfig): Promise<void> {
        throw new Error("Method not implemented.");
    }

    loadAtlas(name: string, image: ImageBitmap, metadata: AtlasMetadata): Promise<void> {
        return this.terrainPass.loadTerrainAtlas(name, image, metadata);
    }

    unloadAtlas(name: string): void {
        throw new Error("Method not implemented.");
    }

    addLayer(layerId: string, config: LayerConfig): void {
        throw new Error("Method not implemented.");
    }

    removeLayer(layerId: string): void {
        throw new Error("Method not implemented.");
    }

    setLayerOrder(layerId: string, order: number): void {
        throw new Error("Method not implemented.");
    }

    draw(): void {
        const commandEncoder = this.device.createCommandEncoder();
        const passEncoder = commandEncoder.beginRenderPass({
            colorAttachments: [{
                view: this.context.getCurrentTexture().createView(),
                loadOp: 'clear',
                clearValue: { r: 0, g: 0, b: 0, a: 1.0 },
                storeOp: 'store',
            }]
        });

        this.terrainPass.safeRender(passEncoder);

        passEncoder.end();
        this.device.queue.submit([commandEncoder.finish()]);

        // Run compute pass for terrain transitions
        const computeEncoder = this.device.createCommandEncoder();
        const computePass = computeEncoder.beginComputePass();

        computePass.setPipeline(this.terrainPass.computePipeline);
        computePass.setBindGroup(0, this.terrainPass.computeBindGroup);

        computePass.dispatchWorkgroups(Math.ceil(this.gridSize / 8), Math.ceil(this.gridSize / 8));

        computePass.end();
        this.device.queue.submit([computeEncoder.finish()]);
    }

    handleResize(): void {
        const displayWidth = this.canvas.clientWidth;
        const displayHeight = this.canvas.clientHeight;
        
        if (this.canvas.width !== displayWidth || this.canvas.height !== displayHeight) {
            this.canvas.width = displayWidth;
            this.canvas.height = displayHeight;
            
            mat4.perspective(
                this.projectionMatrix,
                60 * Math.PI / 180,
                displayWidth / displayHeight,
                0.1,
                100.0
            );
            
            if (this.device && this.cameraUniformBuffer) {
                this.device.queue.writeBuffer(
                    this.cameraUniformBuffer,
                    64,
                    this.projectionMatrix
                );
                
                this.context?.configure({
                    device: this.device,
                    format: this.context.getPreferredFormat(this.device.adapter),
                    alphaMode: 'premultiplied',
                });
            }
        }
    }

    status: RendererStatus;
    lastError: RendererError | null;

    on(event: "error" | "ready" | "disposed", callback: (data?: any) => void): void {
        throw new Error("Method not implemented.");
    }

    off(event: string, callback: (data?: any) => void): void {
        throw new Error("Method not implemented.");
    }

}

export interface TileLookupData {
    bitmask: number;     // 8 bits
    atlasIndex: number;  // 8 bits
    x: number;          // 8 bits
    y: number;          // 8 bits
}
