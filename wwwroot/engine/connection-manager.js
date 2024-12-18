// Message types matching server
const MessageType = {
    // Client -> Server
    ViewportRequest: 0x01,
    CellInfo: 0x02,
    
    // Server -> Client  
    ViewportData: 0x81,
    TileData: 0x82,
    EntityData: 0x83,
    // CellData: 0x82,
    Error: 0xFF
};



export class ConnectionManager {
    constructor(url) {
        this.url = url;
        this.ws = null;
        this.connected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.viewportCallback = null;
        this.errorCallback = null;
        this.callbacks = {
            'viewport': [],
            'tileData': [],
            'entityData': [],
            'error': [],
        };
    }

    on(event, callback) {
        console.log('on', event, callback);
        if (this.callbacks[event]) {
            this.callbacks[event].push(callback);
        }
    }

    emit(event, data) {
        console.log('emit', event, data);
        if (this.callbacks[event]) {
            this.callbacks[event].forEach(callback => callback(data));
        }
    }


    // Connect to server
    async connect() {
        return new Promise((resolve, reject) => {
            try {
                this.ws = new WebSocket(this.url);
                
                this.ws.binaryType = 'arraybuffer';  // We want binary data as ArrayBuffer
                
                this.ws.onopen = () => {
                    console.log('Connected to game server');
                    this.connected = true;
                    this.reconnectAttempts = 0;
                    resolve();
                };
                
                this.ws.onclose = () => {
                    console.log('Disconnected from game server');
                    this.connected = false;
                    this.handleDisconnect();
                };
                
                this.ws.onerror = (error) => {
                    console.error('WebSocket error:', error);
                    reject(error);
                };
                
                this.ws.onmessage = (event) => {
                    this.handleMessage(event.data);
                };
                
            } catch (error) {
                reject(error);
            }
        });
    }
    
    // Handle disconnection and reconnection
    async handleDisconnect() {
        if (this.reconnectAttempts >= this.maxReconnectAttempts) {
            console.error('Max reconnection attempts reached');
            return;
        }
        
        this.reconnectAttempts++;
        console.log(`Attempting to reconnect (${this.reconnectAttempts}/${this.maxReconnectAttempts})...`);
        
        // Exponential backoff
        const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts - 1), 10000);
        await new Promise(resolve => setTimeout(resolve, delay));
        
        try {
            await this.connect();
        } catch (error) {
            console.error('Reconnection failed:', error);
        }
    }
    
    // Clean disconnect
    disconnect() {
        if (this.ws && this.connected) {
            this.ws.close();
        }
    }
    
    // Message handling
    handleMessage(data) {
        const view = new DataView(data);
        const type = view.getUint8(0);
        const length = view.getUint16(1, true); // true for little-endian
        const payload = new Uint8Array(data, 3, length);
        
        switch (type) {
            case MessageType.EntityData:
                this.handleEntityData(payload);
                break;

            case MessageType.TileData:
                this.handleTileData(payload);
                break;

            case MessageType.ViewportData:
                this.handleViewportData(payload);
                break;
                
            case MessageType.Error:
                this.handleError(payload);
                break;
                
            default:
                console.log('Received message:', type, payload);
                console.error('Unknown message type:', type);
        }
    }

    handleEntityData(payload) {
        const entities = [];
        let offset = 0;
        const view = new DataView(payload.buffer, payload.byteOffset);
        
        while(offset < payload.length) {
            entities.push({
                x: payload[offset++],
                y: payload[offset++],
                id: view.getInt32(offset, true),  // Need DataView for 32-bit int
                action: payload[offset + 4],
                type: payload[offset + 5], 
                direction: payload[offset + 6]
            });
            offset += 7;
        }
        this.emit('entityData', entities);
    }

    handleTileData(payload) {
        const width = payload[0];
        const height = payload[1];
        let offset = 2;
    
        // Parse tiles
        const tileMaterial = new Array(width * height);
        const tileSurface = new Array(width * height);
        for(let i = 0; i < width * height; i++) {
            tileMaterial[i] = payload[offset++];
            tileSurface[i] = payload[offset++];
        }
        this.emit('tileData', { tileMaterial, tileSurface });
    }

    // Handle viewport data from server
    handleViewportData(payload) {
        const view = new DataView(payload.buffer, payload.byteOffset, payload.length);
        const width = payload[0];
        const height = payload[1];
        let offset = 2;

        // Parse tiles
        const tileMaterial = new Array(width * height);
        const tileSurface = new Array(width * height);
        const tileProperties = new Array(width * height);
        for(let i = 0; i < width * height; i++) {
            tileMaterial[i] = payload[offset++];
            tileSurface[i] = payload[offset++];
            tileProperties[i] = payload[offset++];
        }

        this.emit('tileData', { tileMaterial, tileSurface, tileProperties });
        
        // Parse entities
        const entities = [];
        while(offset < payload.length) {
            entities.push({
                x: payload[offset++],
                y: payload[offset++],
                id: view.getInt32(offset, true),
                action: payload[offset + 4],
                type: payload[offset + 5],
                direction: payload[offset + 6]
            });
            offset += 7;
        }

        this.emit('entityData', entities);
    }
    
    // Handle error messages
    handleError(payload) {
        const decoder = new TextDecoder();
        const message = decoder.decode(payload);
        
        console.error('Server error:', message);
        if (this.errorCallback) {
            this.errorCallback(message);
        }
    }
    
    // Request viewport data
    requestViewport(cameraX, cameraY, width, height) {
        if (!this.connected) {
            throw new Error('Not connected to server');
        }
        
        // Create message buffer
        const buffer = new ArrayBuffer(19); // 3 header + 16 payload
        const view = new DataView(buffer);
        
        // Write header
        view.setUint8(0, MessageType.ViewportRequest);
        view.setUint16(1, 16, true); // payload length, little-endian
        
        // Write payload
        view.setInt32(3, cameraX, true);
        view.setInt32(7, cameraY, true);
        view.setUint8(11, width, true);
        view.setUint8(12, height, true);
        
        // Send to server
        this.ws.send(buffer);
    }
    
    // Register callbacks
    onViewportData(callback) {
        this.viewportCallback = callback;
    }
    
    onError(callback) {
        this.errorCallback = callback;
    }
}