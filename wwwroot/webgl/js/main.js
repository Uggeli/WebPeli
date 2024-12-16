export class Game {
    constructor() {
        this.assetManager = new AssetManager(32);
        this.canvas = document.getElementById('gameCanvas');
        this.renderer = new WebGL2Renderer(this.canvas, 32);
        this.renderer.setup(this.assetManager.createTextureAtlas());
    }

    start() {
        console.log('Game started');
        const tileData = this.createDummyTileData();
        const gameLoop = () => {
            this.renderer.updateGridData(tileData);
            this.renderer.handleResize();
            this.renderer.draw();
            requestAnimationFrame(gameLoop);
        };
        gameLoop();
    }

    createDummyTileData() {
        const tileData = new Uint8Array(this.renderer.total_tiles);
        for (let i = 0; i < tileData.length; i++) {
            tileData[i] = Math.floor(Math.random() * 12);
        }
        return tileData;
    }
}


class WebGL2Renderer {
    constructor(canvas, grid_size) {
        this.grid_size = grid_size
        this.total_tiles = grid_size * grid_size;
        this.canvas = canvas;
        this.gl = canvas.getContext('webgl2');
        if (!this.gl) {
            console.error('WebGL2 not supported');
            return;
        }
        this.gl.viewport(0, 0, this.gl.canvas.width, this.gl.canvas.height);
        this.gl.clearColor(0.0, 0.0, 0.0, 1.0);
        this.gl.clear(this.gl.COLOR_BUFFER_BIT);
    }

    draw() {
        this.gl.clear(this.gl.COLOR_BUFFER_BIT);
        this.gl.useProgram(this.program);
    
        // Bind quad vertices
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.quadBuffer);
        this.gl.enableVertexAttribArray(this.positionLocation);
        this.gl.vertexAttribPointer(this.positionLocation, 2, this.gl.FLOAT, false, 0, 0);
        this.gl.vertexAttribDivisor(this.positionLocation, 0);  // Not instanced
    
        // Bind instance positions
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.instancePositionBuffer);
        this.gl.enableVertexAttribArray(this.instanceLocation);
        this.gl.vertexAttribPointer(this.instanceLocation, 2, this.gl.FLOAT, false, 0, 0);
        this.gl.vertexAttribDivisor(this.instanceLocation, 1);  // One per instance
    
        // Bind tile IDs
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.tiledataBuffer);
        this.gl.enableVertexAttribArray(this.tileIdLocation);
        this.gl.vertexAttribPointer(this.tileIdLocation, 1, this.gl.UNSIGNED_BYTE, false, 0, 0);
        this.gl.vertexAttribDivisor(this.tileIdLocation, 1);
    
        // Draw instances
        this.gl.drawArraysInstanced(this.gl.TRIANGLE_STRIP, 0, 4, this.total_tiles);
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
        }
    }

    dispose() {
        this.gl.deleteBuffer(this.instancePositionBuffer);
        this.gl.deleteBuffer(this.tiledataBuffer);
        this.gl.deleteTexture(this.atlasTexture);
        this.gl.deleteProgram(this.program);
    }

    setup(tileAtlasTexture) {
        console.log('Renderer setup');

        const quadVertices = new Float32Array([
            0.0, 0.0,  // Bottom left
            1.0, 0.0,  // Bottom right
            0.0, 1.0,  // Top left
            1.0, 1.0   // Top right
        ]);
    
        // Create vertex buffer
        this.quadBuffer = this.gl.createBuffer();
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.quadBuffer);
        this.gl.bufferData(this.gl.ARRAY_BUFFER, quadVertices, this.gl.STATIC_DRAW);

        this.tileGrid = new Uint8Array(this.total_tiles);
        const instancePositions = new Float32Array(this.total_tiles * 2);
        for (let y = 0; y < this.grid_size; y++) {
            for (let x = 0; x < this.grid_size; x++) {
                const index = (y * this.grid_size + x) * 2;
                instancePositions[index] = x;     // x position
                instancePositions[index + 1] = y; // y position
            }
        }

        this.instancePositionBuffer = this.gl.createBuffer();
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.instancePositionBuffer);
        this.gl.bufferData(this.gl.ARRAY_BUFFER, instancePositions, this.gl.STATIC_DRAW);

        this.tiledataBuffer = this.gl.createBuffer();
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.tiledataBuffer);
        this.gl.bufferData(this.gl.ARRAY_BUFFER, this.tileGrid, this.gl.DYNAMIC_DRAW);

        this.atlasTexture = this.gl.createTexture();
        this.gl.bindTexture(this.gl.TEXTURE_2D, this.atlasTexture);
        this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_MIN_FILTER, this.gl.NEAREST);
        this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_MAG_FILTER, this.gl.NEAREST);
        this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_WRAP_S, this.gl.CLAMP_TO_EDGE);
        this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_WRAP_T, this.gl.CLAMP_TO_EDGE);
        this.gl.texImage2D(this.gl.TEXTURE_2D, 0, this.gl.RGBA, this.gl.RGBA, this.gl.UNSIGNED_BYTE, tileAtlasTexture);

        this.createShaders();
        this.gl.useProgram(this.program);

        // Set the uniforms
        this.gl.uniform2f(this.resolutionLocation, this.canvas.width, this.canvas.height);
        this.gl.uniform1f(this.gridSizeLocation, this.grid_size);
        this.gl.uniform1f(this.atlasSizeLocation, 4); // 4x4 atlas

        // Set up texture
        this.gl.activeTexture(this.gl.TEXTURE0);
        this.gl.bindTexture(this.gl.TEXTURE_2D, this.atlasTexture);
        this.gl.uniform1i(this.atlasLocation, 0);
        this.instanceLocation = this.gl.getAttribLocation(this.program, 'a_instance');
    }


    createShaders() {
        // 1. First define shader source code as strings
        const vertexShaderSource = `
            attribute vec2 a_position;      // Quad vertex position
            attribute vec2 a_instance;      // Instance position
            attribute float a_tileId;
            uniform vec2 u_resolution;
            uniform float u_gridSize;
            uniform float u_atlasSize;
            varying vec2 v_texCoord;

            void main() {
                // Scale quad to tile size
                vec2 tileSize = u_resolution / u_gridSize;
                
                // Calculate instance position
                vec2 instancePixelPos = a_instance * u_resolution / u_gridSize;
                
                // Apply JRPG-style projection to the instance position
                float rpgX = instancePixelPos.x - instancePixelPos.y * 0.5;  // Reduced from 0.866 to 0.5 for less extreme angle
                float rpgY = -instancePixelPos.y * 0.25;  // Reduced vertical compression to match FF6 style
                
                // Transform quad vertices while maintaining orientation
                vec2 quadPos = vec2(
                    a_position.x - (a_position.y * 0.5),  // Reduced angle to match FF6
                    -a_position.y * 0.25                  // Less vertical compression
                ) * tileSize;
                
                // Combine instance position with transformed quad
                vec2 finalPosition = vec2(
                    rpgX + quadPos.x,
                    rpgY + quadPos.y
                );
                
                // Center in screen
                vec2 centered = finalPosition + u_resolution * 0.5;
                
                // Convert to clip space
                vec2 clipSpace = (centered / u_resolution) * 2.0 - 1.0;
                gl_Position = vec4(clipSpace, 0, 1);
                
                // Calculate texture coordinates (keep as is)
                float tileX = mod(a_tileId, u_atlasSize);
                float tileY = floor(a_tileId / u_atlasSize);
                v_texCoord = (vec2(tileX, tileY) + a_position) / u_atlasSize;
            }
        `;

        const fragmentShaderSource = `
            precision mediump float;
            uniform sampler2D u_atlas;
            varying vec2 v_texCoord;
    
            void main() {
                gl_FragColor = texture2D(u_atlas, v_texCoord);
            }
        `;

        // 2. Create the actual shader objects
        const vertexShader = this.gl.createShader(this.gl.VERTEX_SHADER);
        const fragmentShader = this.gl.createShader(this.gl.FRAGMENT_SHADER);

        // 3. Add the source code and compile
        this.gl.shaderSource(vertexShader, vertexShaderSource);
        this.gl.shaderSource(fragmentShader, fragmentShaderSource);
        this.gl.compileShader(vertexShader);
        this.gl.compileShader(fragmentShader);

        // 4. Create program and link shaders
        this.program = this.gl.createProgram();
        this.gl.attachShader(this.program, vertexShader);
        this.gl.attachShader(this.program, fragmentShader);
        this.gl.linkProgram(this.program);

        // 5. Check for compilation errors
        if (!this.gl.getProgramParameter(this.program, this.gl.LINK_STATUS)) {
            console.error('Shader program error:', this.gl.getProgramInfoLog(this.program));
            console.error('Vertex shader:', this.gl.getShaderInfoLog(vertexShader));
            console.error('Fragment shader:', this.gl.getShaderInfoLog(fragmentShader));
        }

        // 6. Get attribute and uniform locations
        this.positionLocation = this.gl.getAttribLocation(this.program, 'a_position');
        this.tileIdLocation = this.gl.getAttribLocation(this.program, 'a_tileId');

        // Get uniform locations
        this.resolutionLocation = this.gl.getUniformLocation(this.program, 'u_resolution');
        this.gridSizeLocation = this.gl.getUniformLocation(this.program, 'u_gridSize');
        this.atlasSizeLocation = this.gl.getUniformLocation(this.program, 'u_atlasSize');
        this.atlasLocation = this.gl.getUniformLocation(this.program, 'u_atlas');
    }
}

class AssetManager {
    constructor(tile_size) {
        this.tileSize = tile_size;
        this.assets = {};
        this.tileColors = {
            0: '#000000', // Empty
            1: '#8B4513', // Dirt
            2: '#808080', // Stone
            3: '#8B7355', // Wood
            4: '#C0C0C0', // Metal
            5: '#E0FFFF', // Ice
            6: '#F4A460', // Sand
            8: '#0000FF', // Water
            9: '#FF4500', // Lava
            10: '#FFFFFF', // Snow
            12: '#8B0000', // Blood
            13: '#654321', // Mud
        };
        this.createTileTextures();
        this.tileAtlasTexture = null;
        this.tileAtlasTexture = this.createTextureAtlas();
    }

    createTileTextures() {
        for (let i = 0; i < 16; i++) {
            const canvas = document.createElement('canvas');
            canvas.width = this.tileSize;
            canvas.height = this.tileSize;
            const ctx = canvas.getContext('2d');
            ctx.fillStyle = this.tileColors[i] || '#FF00FF'; // Use magenta for undefined colors
            ctx.fillRect(0, 0, this.tileSize, this.tileSize);

            // Debug: Display each tile
            const debugImg = document.createElement('img');
            debugImg.src = canvas.toDataURL();
            debugImg.style.border = '1px solid blue';
            debugImg.style.margin = '2px';
            document.body.appendChild(debugImg);

            this.loadAsset(`tile_${i}`, canvas.toDataURL());
        }
    }

    createTextureAtlas() {
        if (this.tileAtlasTexture !== null) {
            return this.tileAtlasTexture;
        }

        const atlasSize = 4; // 4x4 grid of tiles
        const atlasCanvas = document.createElement('canvas');
        atlasCanvas.width = this.tileSize * atlasSize;
        atlasCanvas.height = this.tileSize * atlasSize;
        const ctx = atlasCanvas.getContext('2d');

        // Draw each tile into its position in the atlas
        for (let i = 0; i < 16; i++) {
            const x = (i % atlasSize) * this.tileSize;
            const y = Math.floor(i / atlasSize) * this.tileSize;
            ctx.fillStyle = this.tileColors[i] || '#FF00FF'; // Use magenta for undefined colors
            ctx.fillRect(x, y, this.tileSize, this.tileSize);
        }

        // Debug: Display the atlas
        const debugImg = document.createElement('img');
        debugImg.src = atlasCanvas.toDataURL();
        debugImg.style.border = '1px solid red';
        document.body.appendChild(debugImg);

        return atlasCanvas;
    }

    loadAsset(name, path) {
        this.assets[name] = new Image();
        this.assets[name].src = path;
    }

    getAsset(name) {
        return this.assets[name];
    }
}

class ConnectionManager {

}