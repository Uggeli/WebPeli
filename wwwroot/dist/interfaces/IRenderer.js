// Add status tracking
export var RendererStatus;
(function (RendererStatus) {
    RendererStatus[RendererStatus["Uninitialized"] = 0] = "Uninitialized";
    RendererStatus[RendererStatus["Initializing"] = 1] = "Initializing";
    RendererStatus[RendererStatus["Ready"] = 2] = "Ready";
    RendererStatus[RendererStatus["Failed"] = 3] = "Failed";
    RendererStatus[RendererStatus["Disposed"] = 4] = "Disposed";
})(RendererStatus || (RendererStatus = {}));
export var BlendMode;
(function (BlendMode) {
    BlendMode["Normal"] = "normal";
    BlendMode["Add"] = "add";
    BlendMode["Multiply"] = "multiply";
    BlendMode["Screen"] = "screen";
})(BlendMode || (BlendMode = {}));
export class RendererLayer {
    // protected tileData: Uint8Array;
    constructor(config) {
        this.atlasName = config.atlasName;
        this.visible = config.visible ?? true;
        this.opacity = config.opacity ?? 1.0;
        this.renderOrder = config.renderOrder;
    }
    setVisible(visible) {
        this.visible = visible;
    }
    setOpacity(opacity) {
        this.opacity = Math.max(0, Math.min(1, opacity));
    }
    setRenderOrder(order) {
        this.renderOrder = order;
    }
    isVisible() {
        return this.visible;
    }
    getRenderOrder() {
        return this.renderOrder;
    }
}
