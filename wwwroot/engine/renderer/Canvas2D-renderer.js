export class Canvas2DRenderer {
    constructor(canvas, grid_size) {
        this.canvas = canvas;
        this.grid_size = grid_size;
        this.total_tiles = grid_size * grid_size;
        this.ctx = canvas.getContext('2d');
        this.tileSize = 32; // Default tile size in pixels
        this.panOffset = { x: 0, y: 0 }; // For potential pan/scroll
        this.scale = 1.0; // For potential zoom
        
        // Initialize the tile data array
        this.tileData = new Uint8Array(this.total_tiles);
        
        // Set up initial canvas state
        this.handleResize();
        
        // Set up canvas rendering options
        this.ctx.imageSmoothingEnabled = false; // Keep pixel art crisp
        
        // Bind event handlers for pan/zoom if needed
        this.setupInteraction();
    }

    static isSupported() {
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        const supported = ctx !== null;
        return supported;
    }

    async setup(tileAtlasTexture) {
        // Store the atlas texture for rendering
        this.atlasTexture = tileAtlasTexture;
        
        // Calculate the size of a single tile in the atlas
        this.atlasTileSize = tileAtlasTexture.width / 4; // Assuming 4x4 atlas
        
        // Initial draw
        this.draw();
    }

    setupInteraction() {
        let isDragging = false;
        let lastX = 0;
        let lastY = 0;
    
        // Create bound handlers and store them for cleanup
        this.boundHandlers = {
            handleMouseDown: (e) => {
                isDragging = true;
                lastX = e.clientX;
                lastY = e.clientY;
            },
            
            handleMouseMove: (e) => {
                if (!isDragging) return;
                
                const deltaX = e.clientX - lastX;
                const deltaY = e.clientY - lastY;
                
                this.panOffset.x += deltaX;
                this.panOffset.y += deltaY;
                
                lastX = e.clientX;
                lastY = e.clientY;
                
                this.draw();
            },
            
            handleMouseUp: () => {
                isDragging = false;
            },
            
            handleMouseLeave: () => {
                isDragging = false;
            },
            
            handleWheel: (e) => {
                e.preventDefault();
                
                // Calculate zoom center
                const rect = this.canvas.getBoundingClientRect();
                const x = e.clientX - rect.left;
                const y = e.clientY - rect.top;
                
                // Adjust scale
                const delta = e.deltaY > 0 ? 0.9 : 1.1;
                const newScale = Math.max(0.1, Math.min(5.0, this.scale * delta));
                
                // Adjust pan offset to zoom toward mouse position
                if (this.scale !== newScale) {
                    const scaleRatio = newScale / this.scale;
                    this.panOffset.x = x - (x - this.panOffset.x) * scaleRatio;
                    this.panOffset.y = y - (y - this.panOffset.y) * scaleRatio;
                    this.scale = newScale;
                    this.draw();
                }
            }
        };
    
        // Add event listeners using the bound handlers
        this.canvas.addEventListener('mousedown', this.boundHandlers.handleMouseDown);
        this.canvas.addEventListener('mousemove', this.boundHandlers.handleMouseMove);
        this.canvas.addEventListener('mouseup', this.boundHandlers.handleMouseUp);
        this.canvas.addEventListener('mouseleave', this.boundHandlers.handleMouseLeave);
        this.canvas.addEventListener('wheel', this.boundHandlers.handleWheel);
    }

    updateGridData(data) {
        // Update the tile data array
        this.tileData = data;
        this.draw();
    }

    handleResize() {
        const displayWidth = this.canvas.clientWidth;
        const displayHeight = this.canvas.clientHeight;
        
        if (this.canvas.width !== displayWidth || this.canvas.height !== displayHeight) {
            this.canvas.width = displayWidth;
            this.canvas.height = displayHeight;
            this.draw();
        }
    }

    getTileSourceRect(tileId) {
        // Calculate the position of the tile in the atlas
        const atlasSize = 4; // 4x4 atlas
        const tileX = (tileId % atlasSize) * this.atlasTileSize;
        const tileY = Math.floor(tileId / atlasSize) * this.atlasTileSize;
        
        return {
            x: tileX,
            y: tileY,
            width: this.atlasTileSize,
            height: this.atlasTileSize
        };
    }

    draw() {
        // Clear the canvas
        this.ctx.fillStyle = '#000000';
        this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
        
        // Save the current transform state
        this.ctx.save();
        
        // Apply pan and zoom transformations
        this.ctx.translate(this.panOffset.x, this.panOffset.y);
        this.ctx.scale(this.scale, this.scale);
        
        // Calculate visible range of tiles
        const effectiveTileSize = this.tileSize;
        const startX = Math.max(0, Math.floor(-this.panOffset.x / (effectiveTileSize * this.scale)));
        const startY = Math.max(0, Math.floor(-this.panOffset.y / (effectiveTileSize * this.scale)));
        const endX = Math.min(this.grid_size, Math.ceil((this.canvas.width - this.panOffset.x) / (effectiveTileSize * this.scale)));
        const endY = Math.min(this.grid_size, Math.ceil((this.canvas.height - this.panOffset.y) / (effectiveTileSize * this.scale)));
        
        // Draw visible tiles
        for (let y = startY; y < endY; y++) {
            for (let x = startX; x < endX; x++) {
                const index = y * this.grid_size + x;
                const tileId = this.tileData[index];
                
                if (tileId !== undefined) {
                    const sourceRect = this.getTileSourceRect(tileId);
                    const destX = x * effectiveTileSize;
                    const destY = y * effectiveTileSize;
                    
                    // Draw the tile from the atlas
                    this.ctx.drawImage(
                        this.atlasTexture,
                        sourceRect.x,
                        sourceRect.y,
                        sourceRect.width,
                        sourceRect.height,
                        destX,
                        destY,
                        effectiveTileSize,
                        effectiveTileSize
                    );
                }
            }
        }
        
        // Optional: Draw grid lines
        if (this.scale > 0.5) {  // Only draw grid when zoomed in enough
            this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
            this.ctx.lineWidth = 1 / this.scale;  // Keep line width consistent across zoom levels
            
            for (let x = 0; x <= this.grid_size; x++) {
                this.ctx.beginPath();
                this.ctx.moveTo(x * effectiveTileSize, 0);
                this.ctx.lineTo(x * effectiveTileSize, this.grid_size * effectiveTileSize);
                this.ctx.stroke();
            }
            
            for (let y = 0; y <= this.grid_size; y++) {
                this.ctx.beginPath();
                this.ctx.moveTo(0, y * effectiveTileSize);
                this.ctx.lineTo(this.grid_size * effectiveTileSize, y * effectiveTileSize);
                this.ctx.stroke();
            }
        }
        
        // Restore the transform state
        this.ctx.restore();
    }

    dispose() {
        // Store bound event handlers during setupInteraction
        if (this.boundHandlers) {
            const { handleMouseDown, handleMouseMove, handleMouseUp, 
                    handleMouseLeave, handleWheel } = this.boundHandlers;
                    
            this.canvas.removeEventListener('mousedown', handleMouseDown);
            this.canvas.removeEventListener('mousemove', handleMouseMove);
            this.canvas.removeEventListener('mouseup', handleMouseUp);
            this.canvas.removeEventListener('mouseleave', handleMouseLeave);
            this.canvas.removeEventListener('wheel', handleWheel);
        }
        
        // Clear the canvas
        if (this.ctx && this.canvas) {
            // Clear with black background
            this.ctx.fillStyle = '#000000';
            this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
            this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        }
        
        // Remove references
        this.boundHandlers = null;
        this.canvas = null;
        this.ctx = null;
        this.atlasTexture = null;
        this.tileData = null;
        this.panOffset = null;
    }
}