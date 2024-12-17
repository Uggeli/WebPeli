import { mat4 } from 'gl-matrix';

export class WebGPURenderer {
    constructor(canvas, grid_size) {
        this.canvas = canvas;
        this.grid_size = grid_size;
        this.total_tiles = grid_size * grid_size;
        
        // Static camera setup for isometric view
        this.viewMatrix = mat4.create();
        this.projMatrix = mat4.create();
        
        // Set up static projection matrix
        mat4.perspective(
            this.projMatrix,
            60 * Math.PI / 180,
            canvas.width / canvas.height,
            0.1,
            100.0
        );

        // Set up static view matrix for isometric view
        mat4.lookAt(
            this.viewMatrix,
            [grid_size / 2, -grid_size, grid_size / 2],  // Position camera behind and above grid
            [grid_size / 2, grid_size / 2, 0],           // Look at center of grid
            [0, 0, 1]                                    // Up vector along Z axis
        );
    }

    isSupported() {
        if (!navigator.gpu) {
            return false;
        }
        if (!navigator.gpu.requestAdapter) {
            return false;
        }
        return true;
    }


    async init() {
        if (!navigator.gpu) {
            throw new Error("WebGPU not supported on this browser.");
        }

        const adapter = await navigator.gpu.requestAdapter();
        if (!adapter) {
            throw new Error("No appropriate GPUAdapter found.");
        }

        this.device = await adapter.requestDevice();
        this.context = this.canvas.getContext("webgpu");
        this.canvasFormat = navigator.gpu.getPreferredCanvasFormat();
        
        this.context.configure({
            device: this.device,
            format: this.canvasFormat,
        });
    }

    async setup(atlasTexture) {
        await this.init();
        await this.createBuffers();
        await this.createTextureAtlas(atlasTexture);
        await this.createPipeline();
    }

    async createTextureAtlas(atlasImage) {
        // Create texture sampler
        this.sampler = this.device.createSampler({
            magFilter: 'nearest',
            minFilter: 'nearest',
        });

        // Create texture from atlas image
        this.texture = this.device.createTexture({
            size: [atlasImage.width, atlasImage.height, 1],
            format: 'rgba8unorm',
            usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST | GPUTextureUsage.RENDER_ATTACHMENT,
        });

        // Copy image data to texture
        this.device.queue.copyExternalImageToTexture(
            { source: atlasImage },
            { texture: this.texture },
            [atlasImage.width, atlasImage.height]
        );
    }

    async createBuffers() {
        // Create vertex buffer for a single cell
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
        });
        this.device.queue.writeBuffer(this.vertexBuffer, 0, vertices);

        // Create camera uniform buffer with static matrices
        this.cameraUniformBuffer = this.device.createBuffer({
            size: 2 * 4 * 16, // Space for view and projection matrices
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
        });

        // Initialize camera uniform buffer with static matrices
        this.device.queue.writeBuffer(this.cameraUniformBuffer, 0, this.viewMatrix);
        this.device.queue.writeBuffer(this.cameraUniformBuffer, 64, this.projMatrix);

        // Create grid uniform buffer
        const uniformArray = new Float32Array([this.grid_size, this.grid_size]);
        this.uniformBuffer = this.device.createBuffer({
            label: "Grid Uniforms",
            size: uniformArray.byteLength,
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
        });
        this.device.queue.writeBuffer(this.uniformBuffer, 0, uniformArray);

        // Initialize tile data buffer
        this.tileGrid = new Uint8Array(this.total_tiles);
        this.tileDataBuffer = this.device.createBuffer({
            size: this.tileGrid.byteLength,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST,
        });
    }

    async createPipeline() {
        // Create bind group layout with texture and sampler
        this.bindGroupLayout = this.device.createBindGroupLayout({
            entries: [
                {
                    binding: 0,
                    visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
                    buffer: { type: "uniform" }
                },
                {
                    binding: 1,
                    visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
                    buffer: { type: "uniform" }
                },
                {
                    binding: 2,
                    visibility: GPUShaderStage.VERTEX | GPUShaderStage.FRAGMENT,
                    buffer: { type: "storage" }
                },
                {
                    binding: 3,
                    visibility: GPUShaderStage.FRAGMENT,
                    texture: {}
                },
                {
                    binding: 4,
                    visibility: GPUShaderStage.FRAGMENT,
                    sampler: {}
                }
            ]
        });

        // Create pipeline layout
        const pipelineLayout = this.device.createPipelineLayout({
            bindGroupLayouts: [this.bindGroupLayout]
        });

        // Create shader with texture support
        const cellShaderModule = this.device.createShaderModule({
            label: "Cell shader",
            code: `
                struct VertexOutput {
                    @builtin(position) position: vec4f,
                    @location(0) texCoord: vec2f,
                    @location(1) tileId: u32,
                };

                struct CameraUniform {
                    view: mat4x4<f32>,
                    proj: mat4x4<f32>
                };

                @group(0) @binding(0) var<uniform> grid: vec2f;
                @group(0) @binding(1) var<uniform> camera: CameraUniform;
                @group(0) @binding(2) var<storage> tileData: array<u32>;
                @group(0) @binding(3) var atlas: texture_2d<f32>;
                @group(0) @binding(4) var atlasSampler: sampler;

                @vertex
                fn vertexMain(@location(0) position: vec3f,
                            @builtin(instance_index) instance: u32) -> VertexOutput {
                    let i = f32(instance);
                    let x = i % grid.x;
                    let y = floor(i / grid.x);
                    
                    let worldPos = vec3f(
                        position.x + x,
                        position.y + y,
                        0.0
                    );
                    
                    var output: VertexOutput;
                    output.position = camera.proj * camera.view * vec4f(worldPos, 1.0);
                    
                    // Get tile ID from storage buffer
                    let tileId = tileData[instance];
                    output.tileId = tileId;
                    
                    // Calculate texture coordinates based on tile ID
                    let atlasSize = 4.0;  // 4x4 texture atlas
                    let tileX = f32(tileId % 4u);
                    let tileY = f32(tileId / 4u);
                    
                    output.texCoord = vec2f(
                        (tileX + position.x) / atlasSize,
                        (tileY + position.y) / atlasSize
                    );
                    
                    return output;
                }

                @fragment
                fn fragmentMain(input: VertexOutput) -> @location(0) vec4f {
                    return textureSample(atlas, atlasSampler, input.texCoord);
                }
            `
        });

        // Create render pipeline
        this.cellPipeline = this.device.createRenderPipeline({
            label: "Cell pipeline",
            layout: pipelineLayout,
            vertex: {
                module: cellShaderModule,
                entryPoint: "vertexMain",
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
                module: cellShaderModule,
                entryPoint: "fragmentMain",
                targets: [{
                    format: this.canvasFormat
                }]
            }
        });

        // Create bind group with textures and samplers
        this.bindGroup = this.device.createBindGroup({
            layout: this.bindGroupLayout,
            entries: [
                {
                    binding: 0,
                    resource: { buffer: this.uniformBuffer }
                },
                {
                    binding: 1,
                    resource: { buffer: this.cameraUniformBuffer }
                },
                {
                    binding: 2,
                    resource: { buffer: this.tileDataBuffer }
                },
                {
                    binding: 3,
                    resource: this.texture.createView()
                },
                {
                    binding: 4,
                    resource: this.sampler
                }
            ]
        });
    }

    handleResize() {
        const displayWidth = this.canvas.clientWidth;
        const displayHeight = this.canvas.clientHeight;
        
        if (this.canvas.width !== displayWidth || this.canvas.height !== displayHeight) {
            this.canvas.width = displayWidth;
            this.canvas.height = displayHeight;
            
            // Update projection matrix for new aspect ratio
            mat4.perspective(
                this.projMatrix,
                60 * Math.PI / 180,
                displayWidth / displayHeight,
                0.1,
                100.0
            );
            
            // Update projection matrix in GPU buffer
            this.device.queue.writeBuffer(
                this.cameraUniformBuffer,
                64,
                this.projMatrix
            );
            
            // Reconfigure canvas
            this.context.configure({
                device: this.device,
                format: this.canvasFormat,
                size: [displayWidth, displayHeight]
            });
        }
    }

    updateGridData(data) {
        // Update tile data buffer with new grid data
        this.device.queue.writeBuffer(this.tileDataBuffer, 0, data);
    }

    draw() {
        // Create command encoder for this frame
        const encoder = this.device.createCommandEncoder();
        const pass = encoder.beginRenderPass({
            colorAttachments: [{
                view: this.context.getCurrentTexture().createView(),
                loadOp: "clear",
                clearValue: { r: 0, g: 0, b: 0, a: 1.0 },
                storeOp: "store",
            }]
        });

        // Set up render pass
        pass.setPipeline(this.cellPipeline);
        pass.setBindGroup(0, this.bindGroup);
        pass.setVertexBuffer(0, this.vertexBuffer);
        
        // Draw grid cells
        pass.draw(6, this.total_tiles);
        pass.end();

        // Submit commands to GPU
        this.device.queue.submit([encoder.finish()]);
    }

    dispose() {
        // Clean up WebGPU resources
        this.vertexBuffer.destroy();
        this.uniformBuffer.destroy();
        this.cameraUniformBuffer.destroy();
        this.tileDataBuffer.destroy();
        this.texture.destroy();
    }
}