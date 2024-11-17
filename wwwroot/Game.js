class Game {
    constructor(canvasId) {
        this.canvas = document.getElementById(canvasId);
        if (!this.canvas) throw new Error('Canvas not found');

        // Initialize subsystems
        this.setupCanvas();
        this.camera = new Camera();
        this.camera.distance = 150;     // Default starting distance
        this.camera.minDistance = 50;   // Minimum zoom out
        this.camera.maxDistance = 400;  // Maximum zoom out
        this.camera.elevation = 45;     // More top-down view
        this.camera.minElevation = 20;
        this.camera.maxElevation = 80;
        this.renderer = new IsometricRenderer(this.canvas);
        this.network = new NetworkManager(`ws://${document.location.host}/ws`);

        // Game state
        this.viewportWidth = 64;  // Was 32
        this.viewportHeight = 64; // Was 32
        this.isRunning = false;
        this.lastFrameTime = 0;
        this.frameCount = 0;
        this.fpsUpdateInterval = 1000; // Update FPS display every second
        this.lastFpsUpdate = 0;
        this.currentFps = 0;

        // Bind methods
        this.gameLoop = this.gameLoop.bind(this);

        // Setup everything
        this.setupInput();
        this.setupNetwork();
        this.setupDebugInfo();

        // Start the game
        console.log('Game initialized, starting game loop...');
        this.isRunning = true;
        requestAnimationFrame(this.gameLoop);
    }

    setupCanvas() {
        const resizeCanvas = () => {
            // Get the CSS size of the canvas
            const displayWidth = this.canvas.clientWidth;
            const displayHeight = this.canvas.clientHeight;
    
            // Set the canvas buffer size to match the display size
            if (this.canvas.width !== displayWidth || this.canvas.height !== displayHeight) {
                this.canvas.width = displayWidth;
                this.canvas.height = displayHeight;
                
                console.log(`Canvas resized to ${displayWidth}x${displayHeight}`);
                
                // Update WebGL viewport and projection if renderer exists
                if (this.renderer) {
                    this.renderer.handleResize();
                }
            }
        };
        
        // Call once immediately
        resizeCanvas();
        
        // Handle window resizes
        window.addEventListener('resize', resizeCanvas);
    }
    

    setupInput() {
        // Movement keys state
        this.keys = {
            forward: false,  // W
            back: false,    // S
            left: false,    // A
            right: false,   // D
            rotateLeft: false,  // Q
            rotateRight: false, // E
            zoomIn: false,     // R
            zoomOut: false     // F
        };

        // Keyboard controls
        window.addEventListener('keydown', (e) => {
            switch (e.key.toLowerCase()) {
                case 'w': this.keys.forward = true; break;
                case 's': this.keys.back = true; break;
                case 'a': this.keys.left = true; break;
                case 'd': this.keys.right = true; break;
                case 'q': this.keys.rotateLeft = true; break;
                case 'e': this.keys.rotateRight = true; break;
                case 'r': this.keys.zoomIn = true; break;
                case 'f': this.keys.zoomOut = true; break;
            }
        });

        window.addEventListener('keyup', (e) => {
            switch (e.key.toLowerCase()) {
                case 'w': this.keys.forward = false; break;
                case 's': this.keys.back = false; break;
                case 'a': this.keys.left = false; break;
                case 'd': this.keys.right = false; break;
                case 'q': this.keys.rotateLeft = false; break;
                case 'e': this.keys.rotateRight = false; break;
                case 'r': this.keys.zoomIn = false; break;
                case 'f': this.keys.zoomOut = false; break;
            }
        });

        // Mouse controls
        let isDragging = false;
        let lastX = 0, lastY = 0;

        this.canvas.addEventListener('mousedown', (e) => {
            isDragging = true;
            lastX = e.clientX;
            lastY = e.clientY;
        });

        window.addEventListener('mousemove', (e) => {
            if (!isDragging) return;

            const dx = (e.clientX - lastX) * 0.1;
            const dy = (e.clientY - lastY) * 0.1;

            if (e.buttons & 1) { // Left button - pan
                this.camera.move(dx, dy);
            } else if (e.buttons & 2) { // Right button - rotate
                this.camera.rotate(dx);
            }

            lastX = e.clientX;
            lastY = e.clientY;
        });

        window.addEventListener('mouseup', () => {
            isDragging = false;
        });

        // Mouse wheel zoom
        this.canvas.addEventListener('wheel', (e) => {
            e.preventDefault();
            this.camera.zoom(Math.sign(e.deltaY) * -1);
        });

        // Prevent context menu on right click
        this.canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    }

    setupNetwork() {
        // Handle viewport data from server
        this.network.onViewportData((data) => {
            this.renderer.render(data);
        });

        // Handle connection state changes
        this.network.onConnectionChange((connected) => {
            console.log(`WebSocket ${connected ? 'connected' : 'disconnected'}`);
            if (connected) {
                this.requestViewportUpdate();
            }
        });
    }

    setupDebugInfo() {
        this.debugOverlay = document.createElement('div');
        this.debugOverlay.style.position = 'fixed';
        this.debugOverlay.style.top = '10px';
        this.debugOverlay.style.right = '10px';
        this.debugOverlay.style.color = '#00ff00';
        this.debugOverlay.style.fontFamily = 'monospace';
        this.debugOverlay.style.backgroundColor = 'rgba(0,0,0,0.5)';
        this.debugOverlay.style.padding = '10px';
        document.body.appendChild(this.debugOverlay);
    }

    updateDebugInfo() {
        if (!this.debugOverlay) return;

        const pos = this.camera.getPosition();
        this.debugOverlay.textContent =
            `FPS: ${this.currentFps}\n` +
            `Camera Pos: (${pos.x.toFixed(1)}, ${pos.y.toFixed(1)}, ${pos.z.toFixed(1)})\n` +
            `Rotation: ${this.camera.rotation.toFixed(1)}°\n` +
            `Distance: ${this.camera.distance.toFixed(1)}\n` +
            `Connection: ${this.network.isConnected() ? 'Connected' : 'Disconnected'}`;
    }

    requestViewportUpdate() {
        if (!this.network.isConnected()) return;

        const pos = this.camera.getPosition();
        console.log('Camera position:', pos);
        console.log('Camera rotation:', this.camera.rotation);
        console.log('Camera elevation:', this.camera.elevation);
        console.log('Camera distance:', this.camera.distance);

        this.network.sendViewportRequest(
            pos.x, pos.z,  // Use x,z for ground plane coordinates
            this.viewportWidth,
            this.viewportHeight
        );
    }

    update(deltaTime) {
        // Handle keyboard input
        const moveSpeed = 5 * deltaTime;
        const rotateSpeed = 90 * deltaTime;  // degrees per second
        const zoomSpeed = 10 * deltaTime;

        if (this.keys.forward) this.camera.move(0, -moveSpeed);
        if (this.keys.back) this.camera.move(0, moveSpeed);
        if (this.keys.left) this.camera.move(-moveSpeed, 0);
        if (this.keys.right) this.camera.move(moveSpeed, 0);
        if (this.keys.rotateLeft) this.camera.rotate(-rotateSpeed);
        if (this.keys.rotateRight) this.camera.rotate(rotateSpeed);
        if (this.keys.zoomIn) this.camera.zoom(-zoomSpeed);
        if (this.keys.zoomOut) this.camera.zoom(zoomSpeed);

        // Update renderer with new camera matrix
        this.renderer.updateViewMatrix(this.camera.getViewMatrix());

        // Request new viewport data if camera moved
        this.requestViewportUpdate();
    }

    updateFps(timestamp) {
        this.frameCount++;

        if (timestamp - this.lastFpsUpdate >= this.fpsUpdateInterval) {
            this.currentFps = Math.round(
                (this.frameCount * 1000) / (timestamp - this.lastFpsUpdate)
            );
            this.frameCount = 0;
            this.lastFpsUpdate = timestamp;
        }
    }

    gameLoop(timestamp) {
        if (!this.isRunning) return;

        // Calculate delta time
        const deltaTime = (timestamp - this.lastFrameTime) / 1000;
        this.lastFrameTime = timestamp;

        // Update game state
        this.update(deltaTime);

        // Update FPS counter
        this.updateFps(timestamp);

        // Update debug overlay
        this.updateDebugInfo();

        // Request next frame
        requestAnimationFrame(this.gameLoop);
    }

    shutdown() {
        this.isRunning = false;
        this.network.disconnect();
        // Clean up any other resources...
    }
}