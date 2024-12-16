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

class ConnectionManager {
    constructor(url) {
        this.url = url;
        this.ws = null;
        this.connected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.messageHandlers = new Map();
        this.connectionStateHandlers = new Set();
    }

    async connect() {
        return new Promise((resolve, reject) => {
            try {
                this.ws = new WebSocket(this.url);
                this.ws.binaryType = 'arraybuffer';

                this.ws.onopen = () => {
                    this.connected = true;
                    this.reconnectAttempts = 0;
                    this._notifyConnectionState(true);
                    resolve();
                };

                this.ws.onclose = () => {
                    this.connected = false;
                    this._notifyConnectionState(false);
                    this._handleDisconnect();
                };

                this.ws.onerror = (error) => {
                    reject(error);
                };

                this.ws.onmessage = (event) => {
                    this._handleMessage(event.data);
                };

            } catch (error) {
                reject(error);
            }
        });
    }

    disconnect() {
        if (this.ws && this.connected) {
            this.ws.close();
        }
    }

    sendMessage(type, payload) {
        if (!this.connected) {
            throw new Error('Not connected to server');
        }

        const messageSize = payload ? payload.byteLength : 0;
        const buffer = new ArrayBuffer(3 + messageSize);
        const view = new DataView(buffer);

        view.setUint8(0, type);
        view.setUint16(1, messageSize, true);

        if (payload) {
            new Uint8Array(buffer).set(new Uint8Array(payload), 3);
        }

        this.ws.send(buffer);
    }

    onMessage(type, handler) {
        if (!this.messageHandlers.has(type)) {
            this.messageHandlers.set(type, new Set());
        }
        this.messageHandlers.get(type).add(handler);
    }

    onConnectionStateChange(handler) {
        this.connectionStateHandlers.add(handler);
    }

    _handleMessage(data) {
        const view = new DataView(data);
        const type = view.getUint8(0);
        const length = view.getUint16(1, true);
        const payload = new Uint8Array(data, 3, length);

        const handlers = this.messageHandlers.get(type);
        if (handlers) {
            handlers.forEach(handler => handler(payload));
        }
    }

    async _handleDisconnect() {
        if (this.reconnectAttempts >= this.maxReconnectAttempts) {
            return;
        }

        this.reconnectAttempts++;
        const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts - 1), 10000);
        await new Promise(resolve => setTimeout(resolve, delay));

        try {
            await this.connect();
        } catch (error) {
            console.error('Reconnection failed:', error);
        }
    }

    _notifyConnectionState(connected) {
        this.connectionStateHandlers.forEach(handler => handler(connected));
    }
}