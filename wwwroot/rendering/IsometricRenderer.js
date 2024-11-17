// rendering/IsometricRenderer.js

class IsometricRenderer {
    constructor(canvas) {
        this.canvas = canvas;
        this.gl = canvas.getContext('webgl2');
        if (!this.gl) throw new Error('WebGL2 not supported');

        // Debug flag - we'll add debug rendering when true
        this.debug = true;
        this.currentViewMatrix = mat4.create(); // Store current view matrix
        this.setupGL();
    }

    updateViewMatrix(viewMatrix) {
        const gl = this.gl;

        // Store the new view matrix
        mat4.copy(this.currentViewMatrix, viewMatrix);

        // If we have a shader program active, update the uniform
        if (this.program) {
            gl.useProgram(this.program);
            gl.uniformMatrix4fv(this.uniforms.view, false, this.currentViewMatrix);
        }
    }

    handleResize() {
        const gl = this.gl;
        
        // Update the WebGL viewport to match canvas
        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);
        
        // Update projection matrix for new aspect ratio
        this.updateProjection();
        
        // Force a re-render if we have data
        if (this.currentViewportData) {
            this.render(this.currentViewportData);
        }
    }

    setupGL() {
        console.log("Setting up WebGL...");
        const gl = this.gl;

        // Basic WebGL setup
        gl.enable(gl.DEPTH_TEST);
        gl.enable(gl.CULL_FACE);
        gl.clearColor(0.1, 0.1, 0.1, 1.0);

        try {
            // Create and compile shaders
            const program = this.createShaderProgram(BASIC_VERTEX_SHADER, BASIC_FRAGMENT_SHADER);
            gl.useProgram(program);
            this.program = program;

            // Get all uniform locations
            this.uniforms = {
                projection: gl.getUniformLocation(program, 'uProjection'),
                view: gl.getUniformLocation(program, 'uView'),
                model: gl.getUniformLocation(program, 'uModel'),
                height: gl.getUniformLocation(program, 'uHeight'),
                baseColor: gl.getUniformLocation(program, 'uBaseColor'),
                ambient: gl.getUniformLocation(program, 'uAmbient')
            };

            console.log("Shader setup complete");

            // Create geometry
            this.createGeometry();

            // Initial matrix setup
            this.updateProjection();

            console.log("GL Setup complete");
        } catch (error) {
            console.error("Error during GL setup:", error);
            throw error;
        }
    }

    createShaderProgram(vertexSource, fragmentSource) {
        const gl = this.gl;

        // Create vertex shader
        const vertexShader = gl.createShader(gl.VERTEX_SHADER);
        gl.shaderSource(vertexShader, vertexSource);
        gl.compileShader(vertexShader);

        if (!gl.getShaderParameter(vertexShader, gl.COMPILE_STATUS)) {
            const error = gl.getShaderInfoLog(vertexShader);
            gl.deleteShader(vertexShader);
            throw new Error(`Vertex shader compilation failed: ${error}`);
        }

        // Create fragment shader
        const fragmentShader = gl.createShader(gl.FRAGMENT_SHADER);
        gl.shaderSource(fragmentShader, fragmentSource);
        gl.compileShader(fragmentShader);

        if (!gl.getShaderParameter(fragmentShader, gl.COMPILE_STATUS)) {
            const error = gl.getShaderInfoLog(fragmentShader);
            gl.deleteShader(fragmentShader);
            gl.deleteShader(vertexShader);
            throw new Error(`Fragment shader compilation failed: ${error}`);
        }

        // Create program and link shaders
        const program = gl.createProgram();
        gl.attachShader(program, vertexShader);
        gl.attachShader(program, fragmentShader);
        gl.linkProgram(program);

        if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
            const error = gl.getProgramInfoLog(program);
            gl.deleteProgram(program);
            gl.deleteShader(vertexShader);
            gl.deleteShader(fragmentShader);
            throw new Error(`Shader program link failed: ${error}`);
        }

        // Clean up individual shaders
        gl.deleteShader(vertexShader);
        gl.deleteShader(fragmentShader);

        return program;
    }

    // Will be used later for debug visualization
    setDebug(enabled) {
        this.debug = enabled;
        // Re-render if we have current data
        if (this.currentViewportData) {
            this.render(this.currentViewportData);
        }
    }

    createGeometry() {
        console.log("Creating geometry...");
        const gl = this.gl;

        // Create vertices with positions and normals
        const vertices = new Float32Array([
            // Front face         // Normal
            -0.5, -0.5, 0.5, 0.0, 0.0, 1.0,
            0.5, -0.5, 0.5, 0.0, 0.0, 1.0,
            0.5, 0.5, 0.5, 0.0, 0.0, 1.0,
            -0.5, 0.5, 0.5, 0.0, 0.0, 1.0,

            // Back face
            -0.5, -0.5, -0.5, 0.0, 0.0, -1.0,
            -0.5, 0.5, -0.5, 0.0, 0.0, -1.0,
            0.5, 0.5, -0.5, 0.0, 0.0, -1.0,
            0.5, -0.5, -0.5, 0.0, 0.0, -1.0,

            // Top face
            -0.5, 0.5, -0.5, 0.0, 1.0, 0.0,
            -0.5, 0.5, 0.5, 0.0, 1.0, 0.0,
            0.5, 0.5, 0.5, 0.0, 1.0, 0.0,
            0.5, 0.5, -0.5, 0.0, 1.0, 0.0,

            // Bottom face
            -0.5, -0.5, -0.5, 0.0, -1.0, 0.0,
            0.5, -0.5, -0.5, 0.0, -1.0, 0.0,
            0.5, -0.5, 0.5, 0.0, -1.0, 0.0,
            -0.5, -0.5, 0.5, 0.0, -1.0, 0.0,

            // Right face
            0.5, -0.5, -0.5, 1.0, 0.0, 0.0,
            0.5, 0.5, -0.5, 1.0, 0.0, 0.0,
            0.5, 0.5, 0.5, 1.0, 0.0, 0.0,
            0.5, -0.5, 0.5, 1.0, 0.0, 0.0,

            // Left face
            -0.5, -0.5, -0.5, -1.0, 0.0, 0.0,
            -0.5, -0.5, 0.5, -1.0, 0.0, 0.0,
            -0.5, 0.5, 0.5, -1.0, 0.0, 0.0,
            -0.5, 0.5, -0.5, -1.0, 0.0, 0.0,
        ]);

        // Create indices for drawing the cube
        const indices = new Uint16Array([
            0, 1, 2, 0, 2, 3,  // front
            4, 5, 6, 4, 6, 7,  // back
            8, 9, 10, 8, 10, 11, // top
            12, 13, 14, 12, 14, 15, // bottom
            16, 17, 18, 16, 18, 19, // right
            20, 21, 22, 20, 22, 23  // left
        ]);

        // Create and bind vertex array object
        const vao = gl.createVertexArray();
        gl.bindVertexArray(vao);

        // Create and bind vertex buffer
        const vertexBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, vertexBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

        // Position attribute
        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 24, 0);

        // Normal attribute
        gl.enableVertexAttribArray(1);
        gl.vertexAttribPointer(1, 3, gl.FLOAT, false, 24, 12);

        // Create and bind index buffer
        const indexBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBuffer);
        gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, indices, gl.STATIC_DRAW);

        // Store VAO and index count for rendering
        this.vao = vao;
        this.indexCount = indices.length;

        // Create debug grid geometry
        this.createDebugGrid();

        console.log("Geometry creation complete");
    }

    // Debug grid for visualization
    createDebugGrid() {
        const gl = this.gl;

        // Create a simple XZ grid
        const gridSize = 100;  // Adjustable grid size
        const gridVertices = [];
        const gridColors = [];

        // Create lines along X axis
        for (let x = -gridSize; x <= gridSize; x++) {
            gridVertices.push(x, 0, -gridSize, x, 0, gridSize);
            // Red for X=0 line, grey for others
            const color = x === 0 ? [1, 0, 0] : [0.3, 0.3, 0.3];
            gridColors.push(...color, ...color);
        }

        // Create lines along Z axis
        for (let z = -gridSize; z <= gridSize; z++) {
            gridVertices.push(-gridSize, 0, z, gridSize, 0, z);
            // Blue for Z=0 line, grey for others
            const color = z === 0 ? [0, 0, 1] : [0.3, 0.3, 0.3];
            gridColors.push(...color, ...color);
        }

        // Create VAO for debug grid
        const gridVao = gl.createVertexArray();
        gl.bindVertexArray(gridVao);

        // Vertex positions
        const gridVertexBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, gridVertexBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(gridVertices), gl.STATIC_DRAW);
        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 0, 0);

        // Vertex colors
        const gridColorBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, gridColorBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(gridColors), gl.STATIC_DRAW);
        gl.enableVertexAttribArray(2);  // Use location 2 for debug colors
        gl.vertexAttribPointer(2, 3, gl.FLOAT, false, 0, 0);

        this.debugGrid = {
            vao: gridVao,
            vertexCount: gridVertices.length / 3
        };

        // Reset VAO binding
        gl.bindVertexArray(null);
    }

    // Debug helpers
    drawDebugGrid(viewMatrix, projectionMatrix) {
        if (!this.debug) return;

        const gl = this.gl;

        // Use the same shader but ignore lighting
        gl.uniform1f(this.uniforms.ambient, 1.0);
        gl.uniformMatrix4fv(this.uniforms.view, false, viewMatrix);
        gl.uniformMatrix4fv(this.uniforms.projection, false, projectionMatrix);

        // Identity model matrix for grid
        const modelMatrix = mat4.create();
        gl.uniformMatrix4fv(this.uniforms.model, false, modelMatrix);

        // Draw grid
        gl.bindVertexArray(this.debugGrid.vao);
        gl.drawArrays(gl.LINES, 0, this.debugGrid.vertexCount);

        // Reset VAO binding
        gl.bindVertexArray(null);
    }

    // Debug info overlay
    drawDebugInfo(x, y, text, color = '#ffffff') {
        if (!this.debug) return;

        // Create debug overlay if it doesn't exist
        if (!this.debugOverlay) {
            this.debugOverlay = document.createElement('div');
            this.debugOverlay.style.position = 'fixed';
            this.debugOverlay.style.left = '10px';
            this.debugOverlay.style.top = '10px';
            this.debugOverlay.style.color = 'white';
            this.debugOverlay.style.fontFamily = 'monospace';
            this.debugOverlay.style.pointerEvents = 'none';
            document.body.appendChild(this.debugOverlay);
        }

        // Add debug text
        const line = document.createElement('div');
        line.style.color = color;
        line.textContent = text;
        this.debugOverlay.appendChild(line);
    }

    clearDebugInfo() {
        if (this.debugOverlay) {
            this.debugOverlay.innerHTML = '';
        }
    }

    // Material definitions
    getMaterialColor(material) {
        // Define base colors for each material type (from TileMaterial enum)
        switch (material) {
            case 1: return [0.5, 0.35, 0.2];  // Dirt
            case 2: return [0.6, 0.6, 0.6];   // Stone
            case 3: return [0.4, 0.3, 0.2];   // Wood
            case 4: return [0.7, 0.7, 0.8];   // Metal
            case 5: return [0.8, 0.9, 0.9];   // Ice
            case 6: return [0.9, 0.8, 0.6];   // Sand
            case 8: return [0.2, 0.4, 0.8];   // Water
            case 9: return [0.8, 0.2, 0.0];   // Lava
            case 10: return [0.9, 0.9, 0.9];  // Snow
            case 12: return [0.7, 0.0, 0.0];  // Blood (🤘)
            case 13: return [0.4, 0.3, 0.2];  // Mud
            default: return [0.5, 0.0, 0.5];  // Hot pink for unknown materials (easier to spot)
        }
    }

    getTileHeight(material, properties) {
        // Define height based on material and properties
        // Properties are bit flags, we need to check specific bits

        const SOLID = 0x10;        // 16 - Solid flag
        const BLOCKS_LIGHT = 0x02; // 2  - BlocksLight flag

        if (material === 8) { // Water
            return 0.3;  // Water is shallow
        }

        if (properties & SOLID) {
            if (properties & BLOCKS_LIGHT) {
                return 1.0;  // Full height for solid, light-blocking things (walls etc)
            }
            return 0.8;  // Slightly shorter for solid but transparent things
        }

        return 0.5;  // Default height for other stuff
    }

    decodeViewportData(data) {
        console.log("Decoding viewport data...");
        console.log("Received data size:", data.byteLength, "bytes");
        
        // Log raw data for debugging
        const debugView = new Uint8Array(data);
        console.log("First 20 bytes:", Array.from(debugView.slice(0, 20)));
        
        // Read the raw bytes for debugging
        console.log("Reading dimensions bytes:");
        console.log(`First two bytes (width): ${debugView[0]}, ${debugView[1]}`);
        console.log(`Next two bytes (height): ${debugView[2]}, ${debugView[3]}`);
        
        // The server writes 32 as width and height
        // In little endian, 32 = 0x20 = [32, 0] in byte pairs
        // So in our data we should see [32, 0] for both width and height
        const width = debugView[2];  // Should be 32
        const height = debugView[4]; // Should be 32
        
        console.log(`Viewport dimensions: ${width}x${height}`);
        let offset = 4;  // Skip the header
        
        // Validate expected data size
        const expectedSize = 4 + (width * height); // 4 bytes header + 1 byte per tile
        console.log(`Expected total size: ${expectedSize} bytes`);
        if (data.byteLength < expectedSize) {
            throw new Error(`Data too short: got ${data.byteLength}, expected ${expectedSize}`);
        }
        
        const tiles = [];
        console.log("Reading first few tiles:");
        
        // Each tile has 1 byte for material
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const idx = offset + (y * width + x);
                const material = debugView[idx];
                
                if (x < 5 && y < 5) {
                    console.log(`Tile(${x},${y}): material=${material}`);
                }
                
                tiles.push({ 
                    material,
                    surface: 0,  // Default surface for now
                    properties: 0 // Default properties for now
                });
            }
        }
        
        console.log(`Total tiles read: ${tiles.length}`);
        return { width, height, tiles };
    }

    updateProjection() {
        const gl = this.gl;
        
        // Calculate aspect ratio and update projection matrix
        const aspect = gl.canvas.clientWidth / gl.canvas.clientHeight;
        const projectionMatrix = mat4.create();
        
        // Increase the FOV and adjust near/far planes
        mat4.perspective(
            projectionMatrix,
            60 * Math.PI / 180,  // 60 degree FOV (was 45)
            aspect,
            0.1,                 // Near plane
            2000.0               // Far plane (was 1000)
        );
        
        gl.uniformMatrix4fv(this.uniforms.projection, false, projectionMatrix);
        this.currentProjection = projectionMatrix;
    }

    renderTile(x, z, tile, modelMatrix = mat4.create()) {
        const gl = this.gl;
        
        // Get material color and height
        const color = this.getMaterialColor(tile.material);
        const height = this.getTileHeight(tile.material, tile.properties);
        
        if (x < 5 && z < 5) {
            console.log(`Rendering tile(${x},${z}): material=${tile.material}, height=${height}, color=`, color);
        }
        
        // Set uniforms for this tile
        gl.uniform3fv(this.uniforms.baseColor, color);
        gl.uniform1f(this.uniforms.height, height);
        
        // Calculate and set model matrix
        mat4.translate(modelMatrix, modelMatrix, [x, 0, z]);
        gl.uniformMatrix4fv(this.uniforms.model, false, modelMatrix);
        
        // Draw the cube
        gl.bindVertexArray(this.vao);
        gl.drawElements(gl.TRIANGLES, this.indexCount, gl.UNSIGNED_SHORT, 0);
        
        // Reset transformations
        mat4.identity(modelMatrix);
    }

    render(viewportData) {
        this.clearDebugInfo();

        const gl = this.gl;
        gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

        // Make sure we're using our shader program
        gl.useProgram(this.program);

        // Update view matrix uniform (in case it changed)
        gl.uniformMatrix4fv(this.uniforms.view, false, this.currentViewMatrix);

        // Rest of the render method remains the same...
        const { width, height, tiles } = this.decodeViewportData(viewportData);
        this.currentViewportData = viewportData;

        // Set ambient light level
        gl.uniform1f(this.uniforms.ambient, 0.3);

        // Draw debug grid if enabled
        if (this.debug) {
            this.drawDebugGrid(this.currentViewMatrix, this.currentProjection);
        }

        // Create shared model matrix to avoid allocations
        const modelMatrix = mat4.create();

        // Render all tiles
        for (let x = 0; x < width; x++) {
            for (let z = 0; z < height; z++) {
                const tile = tiles[z * width + x];
                this.renderTile(x, z, tile, modelMatrix);
            }
        }

        if (this.debug) {
            this.drawDebugInfo(0, 0, `Rendered ${width * height} tiles`, '#00ff00');
        }
    }

    setupDebugControls() {
        window.addEventListener('keydown', (e) => {
            switch (e.key.toLowerCase()) {
                case 'g':
                    this.debugGrid = !this.debugGrid;
                    break;
                case 'i':
                    this.debugInfo = !this.debugInfo;
                    break;
                case 'w':
                    // Toggle wireframe mode
                    const gl = this.gl;
                    this.wireframe = !this.wireframe;
                    gl.polygonMode = this.wireframe ? gl.LINE : gl.FILL;
                    break;
                case 'd':
                    // Toggle all debug features
                    this.setDebug(!this.debug);
                    break;
            }
            // Re-render with new debug settings
            if (this.currentViewportData) {
                this.render(this.currentViewportData);
            }
        });
    }
}