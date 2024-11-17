class Camera {
    constructor() {
        // Camera position and orientation
        this.target = [32, 0, 32];      // Look-at point
        this.distance = 100;         // Distance from target
        this.rotation = 45;          // Degrees around Y axis
        this.elevation = 30;         // Degrees up from ground

        // Camera constraints
        this.minDistance = 10;
        this.maxDistance = 200;
        this.minElevation = 15;
        this.maxElevation = 85;

        // Movement settings
        this.moveSpeed = 2.0;      // Was 1.0
        this.rotateSpeed = 2.0;    // Was 1.0
        this.zoomSpeed = 2.0;      // Was 1.0

        // Smoothing for camera movement
        this.smoothing = 0.15;  // Lower = smoother
        this.targetRotation = this.rotation;
        this.targetElevation = this.elevation;
        this.targetDistance = this.distance;
        this.targetPosition = [...this.target];
    }

    // Move camera in the XZ plane, relative to current rotation
    move(dx, dz) {
        const rad = this.rotation * Math.PI / 180;
        const cos = Math.cos(rad);
        const sin = Math.sin(rad);

        // Transform movement direction based on camera rotation
        const worldDx = cos * dx - sin * dz;
        const worldDz = sin * dx + cos * dz;

        // Update target position with smoothing
        this.targetPosition[0] += worldDx * this.moveSpeed;
        this.targetPosition[2] += worldDz * this.moveSpeed;

        // Smooth movement
        this.target[0] += (this.targetPosition[0] - this.target[0]) * this.smoothing;
        this.target[2] += (this.targetPosition[2] - this.target[2]) * this.smoothing;
    }

    // Rotate camera around target point
    rotate(degrees) {
        this.targetRotation = (this.targetRotation + degrees * this.rotateSpeed) % 360;
        if (this.targetRotation < 0) this.targetRotation += 360;

        // Smooth rotation
        let deltaRotation = this.targetRotation - this.rotation;

        // Handle wraparound
        if (deltaRotation > 180) deltaRotation -= 360;
        if (deltaRotation < -180) deltaRotation += 360;

        this.rotation += deltaRotation * this.smoothing;
        if (this.rotation < 0) this.rotation += 360;
        if (this.rotation >= 360) this.rotation -= 360;
    }

    // Change camera elevation angle
    elevate(degrees) {
        this.targetElevation = Math.max(this.minElevation,
            Math.min(this.maxElevation,
                this.targetElevation + degrees));

        // Smooth elevation change
        this.elevation += (this.targetElevation - this.elevation) * this.smoothing;
    }

    // Zoom camera in/out
    zoom(delta) {
        this.targetDistance = Math.max(this.minDistance,
            Math.min(this.maxDistance,
                this.targetDistance + delta * this.zoomSpeed));

        // Smooth zooming
        this.distance += (this.targetDistance - this.distance) * this.smoothing;
    }

    // Get current camera position in world space
    getPosition() {
        const rad = this.rotation * Math.PI / 180;
        const elevRad = this.elevation * Math.PI / 180;

        const horizontalDistance = Math.cos(elevRad) * this.distance;

        return {
            x: this.target[0] + Math.cos(rad) * horizontalDistance,
            y: this.target[1] + Math.sin(elevRad) * this.distance,
            z: this.target[2] + Math.sin(rad) * horizontalDistance
        };
    }

    // Get the view matrix for the renderer
    getViewMatrix() {
        const pos = this.getPosition();
        const viewMatrix = mat4.create();

        mat4.lookAt(
            viewMatrix,
            [pos.x, pos.y, pos.z],    // Camera position
            this.target,              // Look at target
            [0, 1, 0]                 // Up vector
        );

        return viewMatrix;
    }

    // Shake camera (for effects)
    shake(intensity = 1, duration = 500) {
        if (this.shakeTimeout) {
            clearTimeout(this.shakeTimeout);
            this.shakeOffset = [0, 0, 0];
        }

        const startTime = performance.now();
        const animate = () => {
            const elapsed = performance.now() - startTime;
            if (elapsed < duration) {
                // Random offset that decreases with time
                const remaining = 1 - (elapsed / duration);
                this.shakeOffset = [
                    (Math.random() - 0.5) * intensity * remaining,
                    (Math.random() - 0.5) * intensity * remaining,
                    (Math.random() - 0.5) * intensity * remaining
                ];
                requestAnimationFrame(animate);
            } else {
                this.shakeOffset = [0, 0, 0];
            }
        };

        animate();
        this.shakeTimeout = setTimeout(() => {
            this.shakeOffset = [0, 0, 0];
            this.shakeTimeout = null;
        }, duration);
    }

    // Helper to check if camera is still moving
    isMoving() {
        const positionDelta = Math.abs(this.targetPosition[0] - this.target[0]) +
            Math.abs(this.targetPosition[2] - this.target[2]);
        const rotationDelta = Math.abs(this.targetRotation - this.rotation);
        const elevationDelta = Math.abs(this.targetElevation - this.elevation);
        const distanceDelta = Math.abs(this.targetDistance - this.distance);

        const THRESHOLD = 0.001;
        return positionDelta > THRESHOLD ||
            rotationDelta > THRESHOLD ||
            elevationDelta > THRESHOLD ||
            distanceDelta > THRESHOLD;
    }

    // Reset camera to default position
    reset() {
        this.target = [0, 0, 0];
        this.targetPosition = [0, 0, 0];
        this.distance = this.targetDistance = 100;
        this.rotation = this.targetRotation = 45;
        this.elevation = this.targetElevation = 30;
    }
}