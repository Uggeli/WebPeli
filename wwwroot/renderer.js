class BiMap {
    constructor(obj) {
        this.nameToValue = Object.freeze({...obj});
        this.valueToName = Object.freeze(
            Object.entries(obj).reduce((acc, [key, val]) => ({...acc, [val]: key}), {})
        );
    }
    
    fromValue(value) {
        return this.valueToName[value];
    }

    fromName(name) {
        return this.nameToValue[name];
    }
}

class TileMaterial {
    static #mapping = new BiMap({
        None: 0,
        Dirt: 1,
        Stone: 2,
        Wood: 3,
        Metal: 4,
        Ice: 5,
        Sand: 6,
        Water: 8,
        Lava: 9,
        Snow: 10,
        Blood: 12,
        Mud: 13,
    });

    static fromValue(value) { return this.#mapping.fromValue(value); }
    static fromName(name) { return this.#mapping.fromName(name); }
}

class Surface {
    static #mapping = new BiMap({
        None: 0,
        ShortGrass: 1 << 0,
        TallGrass: 1 << 6,
        Snow: 1 << 1,
        Moss: 1 << 2,
        Water: 1 << 3,
        Blood: 1 << 4,
        Mud: 1 << 5,
        Reserved2: 1 << 7
    });

    static fromValue(value) { return this.#mapping.fromValue(value); }
    static fromName(name) { return this.#mapping.fromName(name); }
    
    static fromBitField(bits) {
        return Object.entries(this.#mapping.nameToValue)
            .filter(([_, value]) => bits & value)
            .map(([name]) => name);
    }
}

class TileProperties {
    static #mapping = new BiMap({
        None: 0,
        Walkable: 1 << 0,
        BlocksLight: 1 << 1,
        Transparent: 1 << 2,
        BlocksProjectiles: 1 << 3,
        Solid: 1 << 4,
        Interactive: 1 << 5,
        Breakable: 1 << 6,
        Reserved: 1 << 7
    });

    static fromValue(value) { return this.#mapping.fromValue(value); }
    static fromName(name) { return this.#mapping.fromName(name); }
    
    static fromBitField(bits) {
        return Object.entries(this.#mapping.nameToValue)
            .filter(([_, value]) => bits & value)
            .map(([name]) => name);
    }
}




class GameRenderer {
    constructor(canvas) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.tileSize = 32;
        this.camera = { x: 0, y: 0 };
        this.debugElement = document.getElementById('debugInfo');

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

        // Surface rendering patterns
        this.surfacePatterns = {
            ShortGrass: (x, y) => {
                const grassColor = '#90EE90';
                const grassSize = this.tileSize / 8;
                this.ctx.fillStyle = grassColor;
                // top left of the tile
                const grassX = x * this.tileSize;
                const grassY = y * this.tileSize;
                this.ctx.fillRect(grassX, grassY, grassSize, grassSize);

            },
            
            TallGrass: (x, y) => {
                const grassColor = '#228B22';
                this.ctx.fillStyle = grassColor;
                // Draw taller grass blades
                for (let i = 0; i < 6; i++) {
                    const offsetX = Math.random() * (this.tileSize - 8) + 4;
                    const offsetY = Math.random() * (this.tileSize - 8) + 4;
                    this.ctx.beginPath();
                    this.ctx.moveTo(x * this.tileSize + offsetX, y * this.tileSize + offsetY);
                    this.ctx.lineTo(x * this.tileSize + offsetX - 3, y * this.tileSize + offsetY - 8);
                    this.ctx.lineTo(x * this.tileSize + offsetX + 3, y * this.tileSize + offsetY - 8);
                    this.ctx.closePath();
                    this.ctx.fill();
                }
            },
            
            Snow: (x, y) => {
                // Add snow overlay with some variation
                this.ctx.fillStyle = 'rgba(255, 255, 255, 0.7)';
                this.ctx.fillRect(
                    x * this.tileSize, 
                    y * this.tileSize, 
                    this.tileSize, 
                    this.tileSize
                );
                
                // Add snow sparkles
                this.ctx.fillStyle = '#FFFFFF';
                for (let i = 0; i < 5; i++) {
                    const offsetX = Math.random() * this.tileSize;
                    const offsetY = Math.random() * this.tileSize;
                    this.ctx.fillRect(
                        x * this.tileSize + offsetX,
                        y * this.tileSize + offsetY,
                        2,
                        2
                    );
                }
            },
            
            Moss: (x, y) => {
                // Draw moss patches
                this.ctx.fillStyle = '#3A5F0B';
                for (let i = 0; i < 6; i++) {
                    const offsetX = Math.random() * (this.tileSize - 6) + 3;
                    const offsetY = Math.random() * (this.tileSize - 6) + 3;
                    const size = Math.random() * 6 + 4;
                    this.ctx.beginPath();
                    this.ctx.arc(
                        x * this.tileSize + offsetX,
                        y * this.tileSize + offsetY,
                        size,
                        0,
                        Math.PI * 2
                    );
                    this.ctx.fill();
                }
            },
            
            Water: (x, y) => {
                // Semi-transparent water overlay
                this.ctx.fillStyle = 'rgba(0, 105, 148, 0.4)';
                this.ctx.fillRect(
                    x * this.tileSize,
                    y * this.tileSize,
                    this.tileSize,
                    this.tileSize
                );
                
                // Add ripple effect
                this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
                this.ctx.beginPath();
                this.ctx.arc(
                    x * this.tileSize + this.tileSize/2,
                    y * this.tileSize + this.tileSize/2,
                    this.tileSize/3,
                    0,
                    Math.PI * 2
                );
                this.ctx.stroke();
            },
            
            Blood: (x, y) => {
                // Draw blood splatters
                this.ctx.fillStyle = '#8B0000';
                for (let i = 0; i < 4; i++) {
                    const offsetX = Math.random() * (this.tileSize - 8) + 4;
                    const offsetY = Math.random() * (this.tileSize - 8) + 4;
                    const size = Math.random() * 5 + 3;
                    this.ctx.beginPath();
                    this.ctx.arc(
                        x * this.tileSize + offsetX,
                        y * this.tileSize + offsetY,
                        size,
                        0,
                        Math.PI * 2
                    );
                    this.ctx.fill();
                }
            },
            
            Mud: (x, y) => {
                // Draw mud texture
                this.ctx.fillStyle = '#483C32';
                const opacity = 0.6;
                this.ctx.fillStyle = `rgba(72, 60, 50, ${opacity})`;
                this.ctx.fillRect(
                    x * this.tileSize,
                    y * this.tileSize,
                    this.tileSize,
                    this.tileSize
                );
                
                // Add mud cracks
                this.ctx.strokeStyle = '#362D23';
                for (let i = 0; i < 3; i++) {
                    const startX = x * this.tileSize + Math.random() * this.tileSize;
                    const startY = y * this.tileSize + Math.random() * this.tileSize;
                    this.ctx.beginPath();
                    this.ctx.moveTo(startX, startY);
                    this.ctx.lineTo(
                        startX + (Math.random() * 10 - 5),
                        startY + (Math.random() * 10 - 5)
                    );
                    this.ctx.stroke();
                }
            }
        };

        // Initialize size
        this.resize();
        window.addEventListener('resize', () => this.resize());
        window.addEventListener('keydown', (e) => this.handleKeyPress(e));
    }

    render(viewportData) {
        const { width, height, tiles, entities } = viewportData;

        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

        // Render ground tiles
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const tile = tiles[y * width + x];
                this.renderTile(x, y, tile.material);
                this.renderTileSurface(x, y, tile.surface);
            }
        }

        // Render entities
        entities.forEach(entity => {
            this.renderEntity(entity.x, entity.y, entity.direction);
        });
    }

    renderTile(x, y, material) {
        const color = this.tileColors[material] || '#FF00FF'; // Magenta for unknown material
        this.ctx.fillStyle = color;
        this.ctx.fillRect(
            x * this.tileSize,
            y * this.tileSize,
            this.tileSize,
            this.tileSize
        );
        
        // Keep the grid lines
        this.ctx.strokeStyle = '#333333'; // Dark gray
        this.ctx.strokeRect(
            x * this.tileSize,
            y * this.tileSize,
            this.tileSize,
            this.tileSize
        );
    }

    renderTileSurface(x, y, surface) {
        if (surface === 0) return;
        
        // Get array of surface names from bitfield
        const surfaces = Surface.fromBitField(surface);
        
        // Render each surface type
        surfaces.forEach(surfaceName => {
            if (this.surfacePatterns[surfaceName]) {
                this.surfacePatterns[surfaceName](x, y);
            }
        });
    }

    renderDebugInfo() {
    }


    renderEntity(x, y, direction) {
        // Draw triangle pointing in direction
        const centerX = (x + 0.5) * this.tileSize;
        const centerY = (y + 0.5) * this.tileSize;
        const size = this.tileSize * 0.8; // Slightly smaller than tile

        this.ctx.save();
        this.ctx.translate(centerX, centerY);
        this.ctx.rotate(direction * Math.PI / 2); // Convert direction to radians

        this.ctx.beginPath();
        this.ctx.moveTo(0, -size / 2);  // Top
        this.ctx.lineTo(-size / 2, size / 2);  // Bottom left
        this.ctx.lineTo(size / 2, size / 2);   // Bottom right
        this.ctx.closePath();

        this.ctx.fillStyle = 'red';  // Could vary by entity type
        this.ctx.fill();

        this.ctx.restore();
    }

    resize() {
        this.canvas.width = window.innerWidth;
        this.canvas.height = window.innerHeight;

        // Request new viewport data after resize
        this.requestViewport();
    }

    handleKeyPress(e) {
        const moveSpeed = 5; // Pixels per keypress

        switch (e.key) {
            case 'ArrowUp':
                this.camera.y -= moveSpeed;
                break;
            case 'ArrowDown':
                this.camera.y += moveSpeed;
                break;
            case 'ArrowLeft':
                this.camera.x -= moveSpeed;
                break;
            case 'ArrowRight':
                this.camera.x += moveSpeed;
                break;
        }

        this.requestViewport();
    }

    requestViewport() {
        if (this.client) {
            const viewportWidth = Math.ceil(this.canvas.width / this.tileSize);
            const viewportHeight = Math.ceil(this.canvas.height / this.tileSize);

            this.client.requestViewport(
                this.camera.x,
                this.camera.y,
                viewportWidth,
                viewportHeight
            );

            // Update debug info
            this.debugElement.textContent =
                `Camera: (${this.camera.x}, ${this.camera.y})\n` +
                `Viewport: ${viewportWidth}x${viewportHeight}`;
        }
    }

    setClient(client) {
        this.client = client;

        client.onViewportData(data => {
            this.render(data);
        });
    }
}