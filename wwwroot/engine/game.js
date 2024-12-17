import { AssetManager } from './asset-manager.js';
import { WebGPURenderer } from './renderer/webgl2-renderer.js';


export class Game {
    constructor() {
        this.tileSize = 32;
        this.assetManager = new AssetManager(this.tileSize);
        this.canvas = document.getElementById('gameCanvas');
        this.renderer = this.selectRenderer();
        if (this.renderer === null) {
            console.error('No supported renderer found');
            return;
        }
        this.renderer.setup(this.assetManager.createTextureAtlas());
    }
    selectRenderer() {
        if (WebGPURenderer.isSupported()) {
            return new WebGPURenderer(this.canvas);
        }
        return null;

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