// Message types matching server
const MessageType = {
    // Client -> Server
    ViewportRequest: 0x01,
    CellInfo: 0x02,
    
    // Server -> Client  
    ViewportData: 0x81,
    CellData: 0x82,
    Error: 0xFF
};





class GameClient {
    constructor(url) {
        this.url = url;
        this.ws = null;
        this.connected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.viewportCallback = null;
        this.errorCallback = null;
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
            case MessageType.ViewportData:
                this.handleViewportData(payload);
                break;
                
            case MessageType.Error:
                this.handleError(payload);
                break;
                
            default:
                console.error('Unknown message type:', type);
        }
    }
    
    // Handle viewport data from server
    handleViewportData(payload) {
        const view = new DataView(payload.buffer, payload.byteOffset, payload.length);
        const width = view.getUint16(0, true);
        const height = view.getUint16(2, true);
        
        // Create grid array
        const grid = new Uint8Array(width * height);
        for (let i = 0; i < width * height; i++) {
            grid[i] = payload[4 + i];
        }
        
        // If we have a callback registered, send the data
        if (this.viewportCallback) {
            this.viewportCallback({
                width,
                height,
                grid
            });
        }
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
        view.setFloat32(3, cameraX, true);
        view.setFloat32(7, cameraY, true);
        view.setFloat32(11, width, true);
        view.setFloat32(15, height, true);
        
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

// Example usage:
/*
const client = new GameClient('ws://localhost:5000/ws');

// Register callbacks
client.onViewportData(({width, height, grid}) => {
    console.log(`Received viewport data: ${width}x${height}`);
    // Handle the grid data...
});

client.onError((message) => {
    console.error('Game error:', message);
});

// Connect and start using
await client.connect();

// Request viewport data
client.requestViewport(0, 0, 800, 600);
*/