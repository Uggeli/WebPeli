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

        // Render chunk borders
        let chunkSize = 8;
        let chunkWidth = chunkSize * this.tileSize;
        let chunkHeight = chunkSize * this.tileSize;
        this.ctx.strokeStyle = '#FF0000';  // Red
        this.ctx.lineWidth = 2;

        if (x % chunkSize === 0) {
            this.ctx.beginPath();
            this.ctx.moveTo(x * this.tileSize, y * this.tileSize);
            this.ctx.lineTo(x * this.tileSize, y * this.tileSize + chunkHeight);
            this.ctx.stroke();
        }

        if (y % chunkSize === 0) {
            this.ctx.beginPath();
            this.ctx.moveTo(x * this.tileSize, y * this.tileSize);
            this.ctx.lineTo(x * this.tileSize + chunkWidth, y * this.tileSize);
            this.ctx.stroke();
        }

        // Render tile coordinates
        this.ctx.fillStyle = '#FFFFFF';
        this.ctx.font = '12px Arial';
        this.ctx.fillText(
            `${x},${y}`,
            x * this.tileSize + 2,
            y * this.tileSize + 12
        );
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