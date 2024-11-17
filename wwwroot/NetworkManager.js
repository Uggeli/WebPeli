class NetworkManager {
    constructor(url) {
        this.url = url;
        this.ws = null;
        this.connected = false;
        this.reconnectTimeout = null;
        this.messageQueue = [];
        this.handlers = new Map();
        this.connectionChangeCallbacks = new Set();
        
        // Message types (matching server enum)
        this.MESSAGE_TYPES = {
            VIEWPORT_REQUEST: 0x01,
            VIEWPORT_DATA: 0x02,
            ERROR: 0x03
        };
        
        // Connection settings
        this.reconnectDelay = 5000;  // 5 seconds between reconnect attempts
        this.maxReconnectAttempts = 5;
        this.reconnectAttempts = 0;
        
        // Start connection
        this.connect();
    }

    connect() {
        console.log('Attempting WebSocket connection...');
        
        try {
            this.ws = new WebSocket(this.url);
            this.ws.binaryType = 'arraybuffer';  // We're using binary messages
            
            this.ws.onopen = () => {
                console.log('WebSocket connected');
                this.connected = true;
                this.reconnectAttempts = 0;
                
                // Process any queued messages
                while (this.messageQueue.length > 0) {
                    const msg = this.messageQueue.shift();
                    this.send(msg);
                }
                
                // Notify connection state change
                this.connectionChangeCallbacks.forEach(cb => cb(true));
            };
            
            this.ws.onclose = (event) => {
                console.log('WebSocket closed:', event.code, event.reason);
                this.connected = false;
                this.connectionChangeCallbacks.forEach(cb => cb(false));
                
                // Try to reconnect if we haven't exceeded max attempts
                if (this.reconnectAttempts < this.maxReconnectAttempts) {
                    this.scheduleReconnect();
                } else {
                    console.error('Max reconnection attempts reached');
                }
            };
            
            this.ws.onerror = (error) => {
                console.error('WebSocket error:', error);
            };
            
            this.ws.onmessage = (event) => {
                try {
                    const data = event.data;
                    if (!(data instanceof ArrayBuffer)) {
                        console.error('Received non-binary message');
                        return;
                    }
                    
                    console.log('Received message size:', data.byteLength, 'bytes');
                    // Log first few bytes to see what we're getting
                    const debug = new Uint8Array(data);
                    console.log('First 10 bytes:', Array.from(debug.slice(0, 10)));
                    
                    const view = new DataView(data);
                    const type = view.getUint8(0);  // First byte is message type
                    console.log('Message type:', type);
                    
                    const handler = this.handlers.get(type);
                    if (handler) {
                        // For viewport data, let's strip the message type byte before passing to renderer
                        if (type === this.MESSAGE_TYPES.VIEWPORT_DATA) {
                            const viewportData = data.slice(1);  // Skip the message type byte
                            handler(viewportData);
                        } else {
                            handler(data);
                        }
                    } else {
                        console.warn('No handler for message type:', type);
                    }
                } catch (error) {
                    console.error('Error processing message:', error);
                }
            };
            
        } catch (error) {
            console.error('WebSocket connection error:', error);
            this.scheduleReconnect();
        }
    }

    scheduleReconnect() {
        if (this.reconnectTimeout) {
            clearTimeout(this.reconnectTimeout);
        }
        
        this.reconnectTimeout = setTimeout(() => {
            this.reconnectAttempts++;
            console.log(`Reconnect attempt ${this.reconnectAttempts}...`);
            this.connect();
        }, this.reconnectDelay);
    }

    disconnect() {
        if (this.ws) {
            this.ws.close();
        }
        if (this.reconnectTimeout) {
            clearTimeout(this.reconnectTimeout);
        }
    }

    isConnected() {
        return this.connected;
    }

    send(data) {
        if (!this.connected) {
            console.log('Not connected, queueing message');
            this.messageQueue.push(data);
            return;
        }
        
        try {
            this.ws.send(data);
        } catch (error) {
            console.error('Error sending message:', error);
            // Queue message for retry
            this.messageQueue.push(data);
        }
    }

    // Send viewport request to server
    sendViewportRequest(x, y, width, height) {
        const buffer = new ArrayBuffer(19);  // 1 type + 2 length + 4 floats (4 bytes each)
        const view = new DataView(buffer);
        let offset = 0;
        
        // Message type
        view.setUint8(offset, this.MESSAGE_TYPES.VIEWPORT_REQUEST);
        offset += 1;
        
        // Payload length (16 bytes - 4 floats)
        view.setUint16(offset, 16, true);  // Little endian
        offset += 2;
        
        // Viewport data as floats
        view.setFloat32(offset, x, true);
        offset += 4;
        view.setFloat32(offset, y, true);
        offset += 4;
        view.setFloat32(offset, width, true);
        offset += 4;
        view.setFloat32(offset, height, true);
        
        this.send(buffer);
    }

    // Register message handlers
    onViewportData(handler) {
        this.handlers.set(this.MESSAGE_TYPES.VIEWPORT_DATA, handler);
    }

    onError(handler) {
        this.handlers.set(this.MESSAGE_TYPES.ERROR, handler);
    }

    // Register connection state change callback
    onConnectionChange(callback) {
        this.connectionChangeCallbacks.add(callback);
        // Immediately call with current state
        callback(this.connected);
    }

    // Debug helper to simulate connection issues
    simulateDisconnect() {
        if (this.ws) {
            this.ws.close();
        }
    }

    // Debug helper to check message queue
    getQueueLength() {
        return this.messageQueue.length;
    }
}