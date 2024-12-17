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