export interface IRenderer {
    // Core setup and lifecycle
    init(): Promise<void>;
    dispose(): void;
    setup(config: RendererConfig): Promise<void>;
    
    // Resource management
    loadAtlas(name: string, image: ImageBitmap, metadata: AtlasMetadata): Promise<void>;
    unloadAtlas(name: string): void;
    
    // Layer management
    addLayer(layerId: string, config: LayerConfig): void;
    removeLayer(layerId: string): void;
    setLayerOrder(layerId: string, order: number): void;

    // Data management
    updateTileMaterialData(data: Uint8Array): void;
    updateTileSurfaceData(data: Uint8Array): void;
    updateEntityData(data: Uint8Array): void;

    // Drawing and rendering
    draw(): void;
    handleResize(): void;
    readonly status: RendererStatus;
    readonly lastError: RendererError | null;
    
    // Add event handling
    on(event: 'error' | 'ready' | 'disposed', callback: (data?: any) => void): void;
    off(event: string, callback: (data?: any) => void): void;

    // WebGL2-specific methods and properties
    updateGridData(data: Uint8Array): void;
    handleResize(): void;
}

export type RendererError = {
    code: number;
    message: string;
    details?: any;
}

// Add status tracking
export enum RendererStatus {
    Uninitialized,
    Initializing,
    Ready,
    Failed,
    Disposed
}

export interface RendererConfig {
    gridSize: number;
    tileSize: number;
    defaultAtlas?: string;
    viewportWidth: number;
    viewportHeight: number;
}

export interface LayerConfig {
    atlasName: string;
    renderOrder: number;
    opacity?: number;
    blendMode?: BlendMode;
    visible?: boolean;
    shaderConfig?: {
        vertexShader?: string;
        fragmentShader?: string;
        uniforms?: Record<string, any>;
    };
    renderTarget?: 'default' | 'offscreen';
}

export interface AtlasMetadata {
    base: {
        tileWidth: number;
        tileHeight: number;
        textureWidth: number;
        textureHeight: number;
    };
    tiles: [
        {
            x: number;
            y: number;
            bitmask: number;
        }
    ];
    lookup: {
        [key: number]: {
            x: number;
            y: number;
            bitmask: number;
        }
    };
}

export interface RenderingContext {
    renderer: IRenderer;
    viewportWidth: number;
    viewportHeight: number;
    timestamp: number;
    deltaTime: number;
}

export enum BlendMode {
    Normal = 'normal',
    Add = 'add',
    Multiply = 'multiply',
    Screen = 'screen'
}

export abstract class RendererLayer {
    protected atlasName: string;
    protected visible: boolean;
    protected opacity: number;
    protected renderOrder: number;
    // protected tileData: Uint8Array;

    constructor(config: LayerConfig) {
        this.atlasName = config.atlasName;
        this.visible = config.visible ?? true;
        this.opacity = config.opacity ?? 1.0;
        this.renderOrder = config.renderOrder;
    }

    abstract render(context: RenderingContext): void;
    abstract updateData(data: Uint8Array): void;

    setVisible(visible: boolean): void {
        this.visible = visible;
    }

    setOpacity(opacity: number): void {
        this.opacity = Math.max(0, Math.min(1, opacity));
    }

    setRenderOrder(order: number): void {
        this.renderOrder = order;
    }

    isVisible(): boolean {
        return this.visible;
    }

    getRenderOrder(): number {
        return this.renderOrder;
    }

    protected abstract validate(data: Uint8Array): boolean;
}
