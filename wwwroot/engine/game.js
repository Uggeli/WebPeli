import { AssetManager } from './asset-manager.js';
import { WebGPURenderer } from './renderer/WebGPU-renderer.js';
import { WebGL2Renderer } from './renderer/WebGL2-renderer.js';
import { Canvas2DRenderer } from './renderer/Canvas2D-renderer.js';
import { ConnectionManager } from './connection-manager.js';
import { WEBGL2Renderer } from './WEBGL2Renderer.js';

export class Game {
    constructor() {
        this.isRunning = false;
        this.viewportSize = 64;
        this.canvas = document.getElementById('gameCanvas');
        this.canvas.tabIndex = 0;
        this.canvas.addEventListener('click', () => this.canvas.focus());
        this.renderer = this.selectRenderer();
        if (this.renderer === null) {
            console.error('No supported renderer found');
            return;
        }
        this.assetManager = new AssetManager(this.viewportSize);
        this.ConnectionManager = new ConnectionManager('ws://' + window.location.host + '/ws');
        this.bindConnectionManager();
        this.tileMaterialData = new Uint8Array(this.renderer.total_tiles);
        this.tileSurfaceData = new Uint8Array(this.renderer.total_tiles);
        this.tileProperties = new Uint8Array(this.renderer.total_tiles);

        this.camera = { x: 0, y: 0 };
        this.debugElement = document.getElementById('debugInfo');
        window.addEventListener('keydown', (e) => this.handleKeyPress(e));
    }

    async init() {
        await this.renderer.setup(this.assetManager.createTextureAtlas());
        // this.bindConnectionManager();
        await this.ConnectionManager.connect();
        this.ConnectionManager.requestViewport(0, 0, this.viewportSize, this.viewportSize);
        // this.start();
        this.initRendererControls();
    }

    bindConnectionManager() {
        this.ConnectionManager.on('viewport', (viewport) => {
            this.updateViewport(viewport);
        });
        this.ConnectionManager.on('tileData', (tileData) => {
            console.log('tileData', tileData);
            console.log(tileData.length);
            if (tileData.tileMaterial) {
                this.tileMaterialData = new Uint8Array(tileData.tileMaterial);
                this.renderer.updateGridData(this.tileMaterialData);
                console.log('tileData.materials', this.tileMaterialData);
            }
            if (tileData.surfaces) {
                this.tileSurfaceData = new Uint8Array(tileData.surfaces);
            }
            if (tileData.Properties) {
                this.tileProperties = new Uint8Array(tileData.Properties);
            }
        });
        this.ConnectionManager.on('entityData', (entityData) => {
            this.updateEntityData(entityData);
        });
    }

    handleKeyPress(e) {
        if (!['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key)) {
            return;
        }
        const moveSpeed = 5; // Pixels per keypress
        e.preventDefault();
        switch (e.key) {
            case 'ArrowUp':
                this.camera.y += moveSpeed;
                break;
            case 'ArrowDown':
                this.camera.y -= moveSpeed;
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
        if (!this.ConnectionManager) return;

        this.ConnectionManager.requestViewport(
            this.camera.x,
            this.camera.y,
            this.viewportSize,
            this.viewportSize
        );

        // Update debug info
        this.debugElement.textContent =
            `Camera: (${this.camera.x}, ${this.camera.y})\n` +
            `Viewport: ${this.viewportSize}x${this.viewportSize}`;
        
    }

    updateEntityData(entityData) {

    }

    initRendererControls() {
        // Get all renderer buttons
        const buttons = {
            'webgpu-btn': 'WebGPU',
            'webgl2-btn': 'WebGL2',
            'canvas2d-btn': 'Canvas2D'
        };
    
        // Set initial active state
        const currentRenderer = this.renderer.constructor.name.replace('Renderer', '');
        document.getElementById(`${currentRenderer.toLowerCase()}-btn`)?.classList.add('active');
    
        // Add click handlers for each button
        Object.entries(buttons).forEach(([btnId, rendererName]) => {
            const btn = document.getElementById(btnId);
            if (btn) {
                btn.addEventListener('click', () => {
                    // Remove active class from all buttons
                    document.querySelectorAll('.renderer-btn').forEach(b => 
                        b.classList.remove('active'));
                    
                    // Add active class to clicked button
                    btn.classList.add('active');
    
                    // Change renderer
                    this.changeRenderer(rendererName);
                });
            }
        });
    }

    selectRenderer() {
        if (WebGPURenderer.isSupported()) {
            console.log('WebGPU is supported');
            return new WebGPURenderer(this.canvas, this.viewportSize);
        }
        if (WebGL2Renderer.isSupported()) {
            console.log('WebGL2 is supported');
            return new WebGL2Renderer(this.canvas, this.viewportSize);
        }
        if (WEBGL2Renderer.isSupported()) {
            console.log('WEBGL2 is supported');
            return new WEBGL2Renderer(this.canvas, this.viewportSize);
        }
        if (Canvas2DRenderer.isSupported()) {
            console.log('Canvas2D is supported');
            return new Canvas2DRenderer(this.canvas, this.viewportSize);
        }
        return null;
    }

    async changeRenderer(renderer) {
        // Store current renderer type for fallback logic
        const previousRenderer = this.renderer?.constructor.name.replace('Renderer', '');
        
        // Stop the game loop first
        this.stop();
    
        // Dispose of current renderer if it exists
        if (this.renderer) {
            try {
                this.renderer.dispose();
            } catch (e) {
                console.error('Error disposing old renderer:', e);
            }
            this.renderer = null;
    
            // Small delay to ensure context is properly released
            await new Promise(resolve => setTimeout(resolve, 50));
        }
    
        // Try to create new renderer
        try {
            // Reset canvas by cloning it
            const newCanvas = this.canvas.cloneNode(false);
            this.canvas.parentNode.replaceChild(newCanvas, this.canvas);
            this.canvas = newCanvas;
            
            // Initialize proper renderer
            switch (renderer) {
                case 'WebGPU':
                    if (!await WebGPURenderer.isSupported()) {
                        throw new Error('WebGPU not supported');
                    }
                    this.renderer = new WebGPURenderer(this.canvas, this.viewportSize);
                    break;
    
                case 'WebGL2':
                    if (!WebGL2Renderer.isSupported()) {
                        throw new Error('WebGL2 not supported');
                    }
                    this.renderer = new WebGL2Renderer(this.canvas, this.viewportSize);
                    break;

                case 'WEBGL2':
                    if (!WEBGL2Renderer.isSupported()) {
                        throw new Error('WEBGL2 not supported');
                    }
                    this.renderer = new WEBGL2Renderer(this.canvas, this.viewportSize);
                    break;
    
                case 'Canvas2D':
                    if (!Canvas2DRenderer.isSupported()) {
                        throw new Error('Canvas2D not supported');
                    }
                    this.renderer = new Canvas2DRenderer(this.canvas, this.viewportSize);
                    break;
    
                default:
                    throw new Error(`Invalid renderer type: ${renderer}`);
            }
    
            // Initialize new renderer
            await this.renderer.setup(this.assetManager.createTextureAtlas());
            
            // Update the buttons to show current renderer
            this.updateRendererButtons(renderer);
            
            // Start the game loop with new renderer
            this.start();
            
        } catch (e) {
            console.error(`Error initializing ${renderer} renderer:`, e);
            
            // Don't try to fall back to the renderer that just failed
            // or if we're already trying Canvas2D
            if (renderer === 'Canvas2D' || previousRenderer === 'Canvas2D') {
                console.error('Failed to initialize any renderer');
                return; // Just return instead of throwing
            }
            
            // Attempt to fall back to Canvas2D
            console.log('Falling back to Canvas2D renderer');
            await this.changeRenderer('Canvas2D');
        }
    }

    updateRendererButtons(activeRenderer) {
        // Remove active class from all buttons
        document.querySelectorAll('.renderer-btn').forEach(btn => {
            btn.classList.remove('active');
        });
        
        // Add active class to current renderer button
        const activeBtn = document.getElementById(`${activeRenderer.toLowerCase()}-btn`);
        if (activeBtn) {
            activeBtn.classList.add('active');
        }
    }

    stop() {
        console.log('Game stopped');
        this.isRunning = false;
    }

    start() {
        console.log('Game started');
        this.isRunning = true;
        // const tileData = this.createDummyTileData();
        const gameLoop = () => {
            if (!this.isRunning) {
                return;
            }
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
