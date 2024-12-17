import { AssetManager } from './asset-manager.js';
import { WebGPURenderer } from './renderer/WebGPU-renderer.js';
import { WebGL2Renderer } from './renderer/WebGL2-renderer.js';
import { Canvas2DRenderer } from './renderer/Canvas2D-renderer.js';
import { ConnectionManager } from './connection-manager.js';

export class Game {
    constructor() {
        this.tileSize = 32;
        this.canvas = document.getElementById('gameCanvas');
        this.renderer = this.selectRenderer();
        if (this.renderer === null) {
            console.error('No supported renderer found');
            return;
        }
        this.assetManager = new AssetManager(this.tileSize);
        this.ConnectionManager = new ConnectionManager('ws://' + window.location.host + '/ws');
        this.renderer.setup(this.assetManager.createTextureAtlas());
    }

    bindConnectionManager() {
        this.ConnectionManager.on('viewport', (viewport) => {
            this.updateViewport(viewport);
        });
        this.ConnectionManager.on('tileData', (tileData) => {
            this.updateTileData(tileData);
        });
        this.ConnectionManager.on('entityData', (entityData) => {
            this.updateEntityData(entityData);
        });
    }

    updateTileData(tileData) {
        this.renderer.updateGridData(tileData);
    }

    updateEntityData(entityData) {

    }

    selectRenderer() {
        if (WebGPURenderer.isSupported()) {
            return new WebGPURenderer(this.canvas, this.tileSize);
        }
        if (WebGL2Renderer.isSupported()) {
            return new WebGL2Renderer(this.canvas, this.tileSize);
        }
        if (Canvas2DRenderer.isSupported()) {
            return new Canvas2DRenderer(this.canvas, this.tileSize);
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