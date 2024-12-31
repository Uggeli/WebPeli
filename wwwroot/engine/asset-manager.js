export class AssetManager {
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


class TileAtlas {
    constructor(atlasTexture, metadata) {
        this.atlasTexture = atlasTexture;
        this.metadata = metadata;
        this.bitMasks = {
            'top_left': 1 << 0,
            'top_center': 1 << 1,
            'top_right': 1 << 2,
            'left_center': 1 << 3,
            'left_bottom': 1 << 4,
            'bottom_center': 1 << 5,
            'bottom_right': 1 << 6,
            'right_center': 1 << 7,
        };
        this.tiles = this._processTiles();
    }

    _processTiles() {
        // Create a canvas large enough for the entire atlas
        const atlasCache = document.createElement('canvas');
        atlasCache.width = this.metadata.textureWidth;
        atlasCache.height = this.metadata.textureHeight;
        const atlasCacheCtx = atlasCache.getContext('2d', { willReadFrequently: true });
        
        // Draw the full atlas
        atlasCacheCtx.drawImage(this.atlasTexture, 0, 0);
        
        // Get the full atlas image data
        const fullImageData = atlasCacheCtx.getImageData(
            0, 0, 
            this.metadata.textureWidth, 
            this.metadata.textureHeight
        );

        const tiles = [];
        const tilesX = this.metadata.textureWidth / this.metadata.tileWidth;
        const tilesY = this.metadata.textureHeight / this.metadata.tileHeight;

        // Process each tile
        for (let y = 0; y < tilesY; y++) {
            for (let x = 0; x < tilesX; x++) {
                const bitmask = this._createBitMasks(fullImageData, x, y);
                tiles.push({
                    x: x,
                    y: y,
                    bitmask: bitmask,
                });
            }
        }
        return tiles;
    }

    _createBitMasks(imageData, tileX, tileY) {
        const startX = tileX * this.metadata.tileWidth;
        const startY = tileY * this.metadata.tileHeight;
        
        const samplePoints = {
            'top_left': { x: startX, y: startY },
            'top_center': { x: startX + Math.floor(this.metadata.tileWidth / 2), y: startY },
            'top_right': { x: startX + this.metadata.tileWidth - 1, y: startY },
            'left_center': { x: startX, y: startY + Math.floor(this.metadata.tileHeight / 2) },
            'left_bottom': { x: startX, y: startY + this.metadata.tileHeight - 1 },
            'bottom_center': { x: startX + Math.floor(this.metadata.tileWidth / 2), y: startY + this.metadata.tileHeight - 1 },
            'bottom_right': { x: startX + this.metadata.tileWidth - 1, y: startY + this.metadata.tileHeight - 1 },
            'right_center': { x: startX + this.metadata.tileWidth - 1, y: startY + Math.floor(this.metadata.tileHeight / 2) },
        };

        let flags = 0;
        
        for (const direction in samplePoints) {
            const point = samplePoints[direction];
            const index = (point.y * imageData.width + point.x) * 4;
            
            // Check alpha channel
            if (imageData.data[index + 3] > 0) {
                flags |= this.bitMasks[direction];
            }
        }

        return flags;
    }

    // Helper method to get a tile by bitmask
    getTileByBitMask(bitmask) {
        return this.tiles.find(tile => tile.bitmask === bitmask);
    }
}


