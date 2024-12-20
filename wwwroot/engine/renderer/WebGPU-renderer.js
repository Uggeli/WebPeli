import { mat4 } from 'gl-matrix';

export class WebGPURenderer {
    constructor(canvas, grid_size) {
        this.canvas = canvas;
        this.grid_size = grid_size;
        this.total_tiles = grid_size * grid_size;
        // this.grid_size = grid_size;
        
        // Static camera setup for isometric view
        this.viewMatrix = mat4.create();
        this.projMatrix = mat4.create();
        
        // Initialize canvas size
        this.handleResize();
        
        // Adjust these values to move camera closer/further:
        const cameraDistance = 1;  // Reduced from 1.5 to 0.8 to move closer
        const cameraHeight = 5;    // Adjusted height for better viewing angle
        
        // Position camera for isometric view
        mat4.lookAt(
            this.viewMatrix,
            [
                grid_size / 2,      // Center X
                -cameraDistance,    // Distance back (smaller value = closer)
                cameraHeight        // Height above grid
            ],
            [grid_size / 2, grid_size / 2, 0],  // Look at center of grid
            [0, 0, 1]                           // Up vector
        );
        
        // Adjust field of view for perspective
        mat4.perspective(
            this.projMatrix,
            45 * Math.PI / 180,     // Field of view (smaller = more zoomed in)
            canvas.width / canvas.height,
            0.1,
            cameraDistance
        );
    }

    static async isSupported() {
        if (!navigator.gpu) return false;
        
        try {
            const adapter = await navigator.gpu.requestAdapter();
            return !!adapter;
        } catch (e) {
            console.error('WebGPU support check failed:', e);
            return false;
        }
    }

    async init() {
        if (!navigator.gpu) {
            throw new Error("WebGPU not supported on this browser.");
        }

        const adapter = await navigator.gpu.requestAdapter({
            powerPreference: 'high-performance'
        });
        
        if (!adapter) {
            throw new Error("No appropriate GPUAdapter found.");
        }

        this.device = await adapter.requestDevice({
            requiredFeatures: [],
            requiredLimits: {
                maxStorageBufferBindingSize: this.total_tiles * 4
            }
        });

        // Add error handling
        this.device.lost.then((info) => {
            console.error('WebGPU device was lost:', info);
            this.handleDeviceLost();
        });

        this.context = this.canvas.getContext("webgpu");
        this.canvasFormat = navigator.gpu.getPreferredCanvasFormat();
        
        this.context.configure({
            device: this.device,
            format: this.canvasFormat,
            alphaMode: 'premultiplied',
            usage: GPUTextureUsage.RENDER_ATTACHMENT,
        });
    }

    async handleDeviceLost() {
        // Clean up existing resources
        this.dispose();
        
        // Attempt to reinitialize
        try {
            await this.init();
            await this.setup(this.lastAtlasTexture);
        } catch (e) {
            console.error('Failed to recover from device loss:', e);
        }
    }

    async setup(atlasTexture) {
        this.lastAtlasTexture = atlasTexture; // Store for potential device loss recovery
        await this.init();
        await this.createBuffers();
        await this.createTextureAtlas(atlasTexture);
        await this.createPipeline();
        
        // Add resize observer
        this.resizeObserver = new ResizeObserver(() => this.handleResize());
        this.resizeObserver.observe(this.canvas);
    }

    async createTextureAtlas(atlasImage) {
        // Create texture sampler
        this.sampler = this.device.createSampler({
            magFilter: 'nearest',
            minFilter: 'nearest',
            mipmapFilter: 'nearest',
            maxAnisotropy: 1,
        });

        // Create texture from atlas image
        this.texture = this.device.createTexture({
            label: 'Texture Atlas',
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
            mappedAtCreation: true,
        });
        new Float32Array(this.vertexBuffer.getMappedRange()).set(vertices);
        this.vertexBuffer.unmap();

        // Create camera uniform buffer
        const cameraUniformBufferSize = 2 * 4 * 16; // 2 mat4s
        this.cameraUniformBuffer = this.device.createBuffer({
            label: "Camera Uniforms",
            size: cameraUniformBufferSize,
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
        });

        // Initialize camera uniform buffer
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
        this.tileGrid = new Uint32Array(this.total_tiles);
        this.tileDataBuffer = this.device.createBuffer({
            label: "Tile Data",
            size: this.tileGrid.byteLength,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST,
        });
    }

    async createPipeline() {
        this.bindGroupLayout = this.device.createBindGroupLayout({
            label: "Bind Group Layout",
            entries: [
                {
                    binding: 0,
                    visibility: GPUShaderStage.VERTEX,
                    buffer: { type: "uniform" }
                },
                {
                    binding: 1, 
                    visibility: GPUShaderStage.VERTEX,
                    buffer: { type: "uniform" }
                },
                {
                    binding: 2,
                    visibility: GPUShaderStage.VERTEX,
                    buffer: { 
                        type: "read-only-storage",
                        minBindingSize: this.total_tiles
                    }
                },
                {
                    binding: 3,
                    visibility: GPUShaderStage.FRAGMENT,
                    texture: {
                        sampleType: 'float',
                        viewDimension: '2d',
                        multisampled: false
                    }
                },
                {
                    binding: 4, 
                    visibility: GPUShaderStage.FRAGMENT,
                    sampler: {
                        type: 'filtering'
                    }
                }
            ]
        });

        const pipelineLayout = this.device.createPipelineLayout({
            label: "Pipeline Layout",
            bindGroupLayouts: [this.bindGroupLayout]
        });

        // Updated shader with better error handling and precision
        const cellShaderModule = this.device.createShaderModule({
            label: "Cell shader",
            code: `
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
    
                @fragment
                fn fragmentMain(input: VertexOutput) -> @location(0) vec4f {
                    return textureSample(atlas, atlasSampler, input.texCoord);
                }
            `
        });

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
                    format: this.canvasFormat,
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

        this.bindGroup = this.device.createBindGroup({
            label: "Cell bind group",
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
            
            if (this.device && this.cameraUniformBuffer) {
                // Update projection matrix in GPU buffer
                this.device.queue.writeBuffer(
                    this.cameraUniformBuffer,
                    64,
                    this.projMatrix
                );
                
                // Reconfigure canvas
                this.context?.configure({
                    device: this.device,
                    format: this.canvasFormat,
                    alphaMode: 'premultiplied',
                });
            }
        }
    }

    updateGridData(data) {
        if (!this.device || !this.tileDataBuffer) return;
        console.log('Updating grid data:', data);
        // Ensure data is Uint32Array
        const uint32Data = data instanceof Uint32Array ? data : new Uint32Array(data);
        if (uint32Data.length !== this.total_tiles) {
            console.error('Invalid grid data size');
            return;
        }
        
        this.device.queue.writeBuffer(this.tileDataBuffer, 0, uint32Data);
    }

    draw() {
        if (!this.device || !this.context || !this.cellPipeline || !this.bindGroup) return;

        // Handle canvas resize before drawing
        this.handleResize();

        // Create command encoder
        const encoder = this.device.createCommandEncoder({
            label: "Cell renderer"
        });

        const pass = encoder.beginRenderPass({
            label: "Cell render pass",
            colorAttachments: [{
                view: this.context.getCurrentTexture().createView(),
                loadOp: "clear",
                clearValue: { r: 0, g: 0, b: 0, a: 1.0 },
                storeOp: "store",
            }]
        });

        pass.setPipeline(this.cellPipeline);
        pass.setBindGroup(0, this.bindGroup);
        pass.setVertexBuffer(0, this.vertexBuffer);
        pass.draw(6, this.total_tiles, 0, 0);
        pass.end();

        this.device.queue.submit([encoder.finish()]);
    }

    dispose() {
        try {
            // Remove resize observer
            if (this.resizeObserver) {
                this.resizeObserver.disconnect();
                this.resizeObserver = null;
            }
            
            // Destroy buffers
            const buffers = [
                'vertexBuffer',
                'uniformBuffer',
                'cameraUniformBuffer',
                'tileDataBuffer'
            ];
            
            buffers.forEach(buffer => {
                if (this[buffer]) {
                    this[buffer].destroy();
                    this[buffer] = null;
                }
            });
            
            // Destroy texture resources
            if (this.texture) {
                this.texture.destroy();
                this.texture = null;
            }
            
            // Clear pipeline and bind group
            // Note: These don't need explicit destruction in WebGPU
            this.bindGroup = null;
            this.bindGroupLayout = null;
            this.cellPipeline = null;
            
            // Clear the GPUCanvas context if it exists
            if (this.context && this.device) {
                const commandEncoder = this.device.createCommandEncoder();
                const textureView = this.context.getCurrentTexture().createView();
                
                const renderPass = commandEncoder.beginRenderPass({
                    colorAttachments: [{
                        view: textureView,
                        clearValue: { r: 0, g: 0, b: 0, a: 1 },
                        loadOp: 'clear',
                        storeOp: 'store',
                    }]
                });
                renderPass.end();
                this.device.queue.submit([commandEncoder.finish()]);
            }
            
            // Clear other references
            this.sampler = null;
            this.context = null;
            this.device = null;
            this.tileGrid = null;
            
            // Clear matrix references
            this.viewMatrix = null;
            this.projMatrix = null;
            
        } catch (e) {
            console.error('Error during WebGPU cleanup:', e);
        }
    }
}