import { mat4 } from 'gl-matrix';

export class WebGL2Renderer {
    constructor(canvas, grid_size) {
        this.grid_size = grid_size;
        this.total_tiles = grid_size * grid_size;
        this.canvas = canvas;
        this.gl = canvas.getContext('webgl2');
        
        if (!this.gl) {
            console.error('WebGL2 not supported');
            return;
        }

        // Initialize matrices once
        this.projMatrix = mat4.create();
        this.viewMatrix = mat4.create();
        
        mat4.perspective(this.projMatrix, 60 * Math.PI / 180, 
                        this.canvas.width / this.canvas.height, 0.1, 100.0);
        
        mat4.lookAt(this.viewMatrix,
            [this.grid_size / 2, -this.grid_size, this.grid_size / 2], // camera position
            [this.grid_size / 2, this.grid_size / 2, 0], // look at point
            [0, 0, 1] // up vector
        );

        this.gl.enable(this.gl.DEPTH_TEST);
        this.gl.enable(this.gl.BLEND);
        this.gl.blendFunc(this.gl.SRC_ALPHA, this.gl.ONE_MINUS_SRC_ALPHA);
        this.handleResize();
    }

    isSupported() {
        const dummyCanvas = document.createElement('canvas');
        if (!dummyCanvas.getContext('webgl2')) {
            return false;
        }
        document.removeChild(dummyCanvas);
    }

    setup(tileAtlasTexture) {
        this.createBuffers();
        this.setupTexture(tileAtlasTexture);
        this.createShaders();
        
        // Set static uniforms after shader creation
        this.gl.useProgram(this.program);
        this.gl.uniformMatrix4fv(this.locations.projectionMatrix, false, this.projMatrix);
        this.gl.uniformMatrix4fv(this.locations.viewMatrix, false, this.viewMatrix);
        this.gl.uniform2f(this.locations.gridSize, this.grid_size, this.grid_size);
        
        // Set up texture
        this.gl.activeTexture(this.gl.TEXTURE0);
        this.gl.bindTexture(this.gl.TEXTURE_2D, this.atlasTexture);
        this.gl.uniform1i(this.locations.atlas, 0);
    }

    createBuffers() {
        // Create vertex buffer
        const vertices = new Float32Array([
            0.0, 0.0, 0.0,  // Bottom left
            1.0, 0.0, 0.0,  // Bottom right
            1.0, 1.0, 0.0,  // Top right
            0.0, 0.0, 0.0,  // Bottom left
            1.0, 1.0, 0.0,  // Top right
            0.0, 1.0, 0.0   // Top left
        ]);

        this.vertexBuffer = this.gl.createBuffer();
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.vertexBuffer);
        this.gl.bufferData(this.gl.ARRAY_BUFFER, vertices, this.gl.STATIC_DRAW);

        // Create tile data buffer
        this.tileGrid = new Uint8Array(this.total_tiles);
        this.tiledataBuffer = this.gl.createBuffer();
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.tiledataBuffer);
        this.gl.bufferData(this.gl.ARRAY_BUFFER, this.tileGrid, this.gl.DYNAMIC_DRAW);
    }

    setupTexture(atlas) {
        this.atlasTexture = this.gl.createTexture();
        this.gl.bindTexture(this.gl.TEXTURE_2D, this.atlasTexture);
        
        this.gl.texImage2D(this.gl.TEXTURE_2D, 0, this.gl.RGBA, this.gl.RGBA, 
                          this.gl.UNSIGNED_BYTE, atlas);
        
        this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_MIN_FILTER, this.gl.NEAREST);
        this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_MAG_FILTER, this.gl.NEAREST);
        this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_WRAP_S, this.gl.CLAMP_TO_EDGE);
        this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_WRAP_T, this.gl.CLAMP_TO_EDGE);
    }

    createShaders() {
        const vsSource = `#version 300 es
            in vec3 aPosition;
            in float aTileId;
            
            uniform mat4 uProjectionMatrix;
            uniform mat4 uViewMatrix;
            uniform vec2 uGridSize;
            
            out vec2 vTexCoord;
            
            void main() {
                float instance = float(gl_InstanceID);
                float x = mod(instance, uGridSize.x);
                float y = floor(instance / uGridSize.x);
                
                vec3 worldPos = vec3(
                    aPosition.x + x,
                    aPosition.y + y,
                    0.0
                );
                
                gl_Position = uProjectionMatrix * uViewMatrix * vec4(worldPos, 1.0);
                
                // Calculate texture coordinates
                float atlasSize = 4.0;  // 4x4 texture atlas
                float tileX = mod(aTileId, atlasSize);
                float tileY = floor(aTileId / atlasSize);
                
                vec2 tileCoord = vec2(
                    (tileX + mod(aPosition.x, 1.0)) / atlasSize,
                    (tileY + mod(aPosition.y, 1.0)) / atlasSize
                );
                
                vTexCoord = tileCoord;
            }
        `;

        const fsSource = `#version 300 es
            precision mediump float;
            
            in vec2 vTexCoord;
            uniform sampler2D uAtlas;
            
            out vec4 fragColor;
            
            void main() {
                fragColor = texture(uAtlas, vTexCoord);
            }
        `;

        const vertexShader = this.createShader(this.gl.VERTEX_SHADER, vsSource);
        const fragmentShader = this.createShader(this.gl.FRAGMENT_SHADER, fsSource);

        this.program = this.gl.createProgram();
        this.gl.attachShader(this.program, vertexShader);
        this.gl.attachShader(this.program, fragmentShader);
        this.gl.linkProgram(this.program);

        if (!this.gl.getProgramParameter(this.program, this.gl.LINK_STATUS)) {
            console.error('Program link error:', this.gl.getProgramInfoLog(this.program));
            return;
        }

        this.locations = {
            position: this.gl.getAttribLocation(this.program, 'aPosition'),
            tileId: this.gl.getAttribLocation(this.program, 'aTileId'),
            projectionMatrix: this.gl.getUniformLocation(this.program, 'uProjectionMatrix'),
            viewMatrix: this.gl.getUniformLocation(this.program, 'uViewMatrix'),
            gridSize: this.gl.getUniformLocation(this.program, 'uGridSize'),
            atlas: this.gl.getUniformLocation(this.program, 'uAtlas')
        };
    }

    createShader(type, source) {
        const shader = this.gl.createShader(type);
        this.gl.shaderSource(shader, source);
        this.gl.compileShader(shader);

        if (!this.gl.getShaderParameter(shader, this.gl.COMPILE_STATUS)) {
            console.error('Shader compile error:', this.gl.getShaderInfoLog(shader));
            this.gl.deleteShader(shader);
            return null;
        }
        return shader;
    }

    updateGridData(data) {
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.tiledataBuffer);
        this.gl.bufferSubData(this.gl.ARRAY_BUFFER, 0, data);
    }

    handleResize() {
        const displayWidth = this.canvas.clientWidth;
        const displayHeight = this.canvas.clientHeight;
        if (this.canvas.width !== displayWidth || this.canvas.height !== displayHeight) {
            this.canvas.width = displayWidth;
            this.canvas.height = displayHeight;
            this.gl.viewport(0, 0, this.gl.canvas.width, this.gl.canvas.height);
            
            // Update projection matrix on resize
            mat4.perspective(this.projMatrix, 60 * Math.PI / 180, 
                           this.canvas.width / this.canvas.height, 0.1, 100.0);
            
            // Update projection matrix uniform if shader program exists
            if (this.program) {
                this.gl.useProgram(this.program);
                this.gl.uniformMatrix4fv(this.locations.projectionMatrix, false, this.projMatrix);
            }
        }
    }

    draw() {
        this.gl.clear(this.gl.COLOR_BUFFER_BIT | this.gl.DEPTH_BUFFER_BIT);
        this.gl.useProgram(this.program);

        // Set up vertex attributes
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.vertexBuffer);
        this.gl.enableVertexAttribArray(this.locations.position);
        this.gl.vertexAttribPointer(this.locations.position, 3, this.gl.FLOAT, false, 0, 0);
        this.gl.vertexAttribDivisor(this.locations.position, 0);

        // Set up tile ID attributes
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.tiledataBuffer);
        this.gl.enableVertexAttribArray(this.locations.tileId);
        this.gl.vertexAttribPointer(this.locations.tileId, 1, this.gl.UNSIGNED_BYTE, false, 0, 0);
        this.gl.vertexAttribDivisor(this.locations.tileId, 1);

        // Draw
        this.gl.drawArraysInstanced(this.gl.TRIANGLES, 0, 6, this.total_tiles);
    }

    dispose() {
        this.gl.deleteBuffer(this.vertexBuffer);
        this.gl.deleteBuffer(this.tiledataBuffer);
        this.gl.deleteTexture(this.atlasTexture);
        this.gl.deleteProgram(this.program);
    }
}