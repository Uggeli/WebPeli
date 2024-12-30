export class Sheet {
    constructor(options = {}) {
        this._initProperties(options);
        this.createFromSelection = options.createFromSelection;
        this.setup(options);
    }

    _initProperties(options = {}) {
        // Core properties
        this.tiles = [];
        this.selectedTiles = new Set();
        this.areas = [];
        this.isDragging = false;
        this.dragStart = null;
        this.dragEnd = null;

        // Configuration
        this.debug = options.debug || false;
        this.width = options.gridSize?.width || 32;
        this.height = options.gridSize?.height || 32;

        // DOM elements
        this.rootDiv = document.createElement('div');

        // Misc
        this.AreaColors = [
            'rgba(255, 0, 0, 0.3)',
            'rgba(0, 255, 0, 0.3)',
            'rgba(0, 0, 255, 0.3)',
            'rgba(255, 255, 0, 0.3)',
            'rgba(255, 0, 255, 0.3)',
            'rgba(0, 255, 255, 0.3)',
            'rgba(255, 255, 255, 0.3)',
        ];
        this.AreaColorIndex = 0;
    }

    setup(options = {}) {
        // Handle texture atlas
        if (options.textureAtlas) {
            this.setTextureAtlas(options.textureAtlas);
        } else {
            this._createEmptyAtlas(options.width || 800, options.height || 800);
        }

        // Initialize metadata
        this._initMetadata();


        // Create UI components
        this._createToolbar();
        this._createSheetCanvas();
        this._createMetadataArea();
        this._setupClickHandling();

        // Debug setup
        if (this.debug) {
            this._createDebugArea();
        }

        // Process initial tiles
        this.processTiles();
    }

    _createEmptyAtlas(width, height) {
        const emptyAtlas = document.createElement('canvas');
        emptyAtlas.width = width;
        emptyAtlas.height = height;
        const ctx = emptyAtlas.getContext('2d');
        ctx.fillStyle = 'rgba(0, 0, 0, 0)';
        ctx.fillRect(0, 0, width, height);
        this.setTextureAtlas(emptyAtlas);
    }

    reset() {
        // Cleanup
        this._teardownClickHandling();
        this._removeSheetCanvas();
        this.rootDiv.innerHTML = '';
        this.selectedTiles.clear();
        this.areas = [];
    }

    show(container) {
        container.appendChild(this.rootDiv);
    }

    hide() {
        this.rootDiv.remove();
    }

    _createSheetCanvas() {
        this.sheetCanvasDiv = document.createElement('div');
        this.sheetCanvasDiv.classList.add('sheet-canvas-container');
        
        this.canvas = document.createElement('canvas');
        this.canvas.classList.add('sheet-canvas');
        this.canvas.width = this.width * this.textures_x;
        this.canvas.height = this.height * this.textures_y;

        this.sheetCtx = this.canvas.getContext('2d');
        this._updateSheetCanvas();

        this.sheetCanvasDiv.appendChild(this.canvas);
        this.rootDiv.appendChild(this.sheetCanvasDiv);
    }

    _removeSheetCanvas() {
        this.sheetCanvasDiv.remove();
    }

    setTileSize(width, height) {
        this.width = width;
        this.height = height;
        this._initMetadata();
    }

    _loadImage(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = (e) => {
                const img = new Image();
                img.onload = () => resolve(img);
                img.onerror = reject;
                img.src = e.target.result;
            };
            reader.onerror = reject;
            reader.readAsDataURL(file);
        });
    }

    setTextureAtlas(textureAtlas) {
        this.textureAtlas = textureAtlas;
        this.textures_x = textureAtlas.width / this.width;
        this.textures_y = textureAtlas.height / this.height;
    }

    _handleAddNewAtlas() {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/*';

        input.onchange = async (e) => {
            const file = e.target.files[0];
            if (!file) return;

            const img = await this._loadImage(file);
            this.reset();
            this.setup({ textureAtlas: img });
        };

        input.click();
    }

    _createToolbar() {
        const toolbar = document.createElement('div');
        toolbar.classList.add('sheet-toolbar');
        
        const buttons = [
            { text: 'Update Atlas', onClick: () => {
                this._handleAddNewAtlas();
            }},
            { text: 'Toggle Grid', onClick: () => {
                this._updateSheetCanvas();
            }},
            { text: 'Add Area', onClick: () => {
                this._addArea();
            }},
            { text: 'Create From Selection', onClick: async () => {
                if (!this.createFromSelection) {
                    console.error('No createFromSelection callback provided');
                    return;
                }
                const newSheet = await this._createNewSheetFromSelection();
                if (!newSheet) {
                    console.error('Failed to create new sheet');
                    return;
                }
                this.createFromSelection(newSheet);
            }}
        ];

        const tileSizeControls = document.createElement('div');
        tileSizeControls.classList.add('sheet-tile-controls');
        
        const updateTileSizeX = document.createElement('input');
        const updateTileSizeY = document.createElement('input');
        updateTileSizeX.type = 'number';
        updateTileSizeY.type = 'number';
        updateTileSizeX.value = this.width;
        updateTileSizeY.value = this.height;

        const updateTilesBtn = document.createElement('button');
        updateTilesBtn.textContent = 'Update Tile Size';
        updateTilesBtn.onclick = () => {
            this.setTileSize(updateTileSizeX.value, updateTileSizeY.value);
            this.selectedTiles.clear();
            this.processTiles();
            this._updateSheetCanvas();
        };

        tileSizeControls.appendChild(updateTileSizeX);
        tileSizeControls.appendChild(updateTileSizeY);
        tileSizeControls.appendChild(updateTilesBtn);

        buttons.forEach(({ text, onClick }) => {
            const button = document.createElement('button');
            button.textContent = text;
            button.classList.add('sheet-button');
            button.onclick = onClick;
            toolbar.appendChild(button);
        });

        toolbar.appendChild(tileSizeControls);
        this.rootDiv.appendChild(toolbar);
    }

    _createMetadataArea() {
        this.metadataArea = document.createElement('div');
        this.rootDiv.appendChild(this.metadataArea);
    }

    _createDebugArea() {
        this.debugArea = document.createElement('div');
        const debugCanvas = document.createElement('canvas');
        debugCanvas.width = this.textureAtlas.width;
        debugCanvas.height = this.textureAtlas.height;
        this.debugCtx = debugCanvas.getContext('2d');
        this.debugArea.appendChild(debugCanvas);

        this.rootDiv.appendChild(this.debugArea);
    }

    _updateDebugArea() {
        if (!this.debugArea) {
            return;
        }
        this.debugArea.innerHTML = '';
        this.debugArea.innerHTML += `Selected Tiles: ${Array.from(this.selectedTiles).join(', ')}`;
        this.debugArea.innerHTML += '<br>';
        this.debugArea.innerHTML += `Dragging: ${this.isDragging}`;
        this.debugArea.innerHTML += '<br>';
        this.debugArea.innerHTML += `Drag Start: ${this.dragStart}`;
        this.debugArea.innerHTML += '<br>';
        this.debugArea.innerHTML += `Drag End: ${this.dragEnd}`;

        for (const tileIndex of this.selectedTiles) {
            const [x, y] = this._1DTo2D(tileIndex);
            const tile = this.tiles[tileIndex];
            const img = new Image();
            img.src = tile.canvas;
            this.debugArea.appendChild(img);
        }
    }

    _setupClickHandling() {
        this.canvas.addEventListener('mousedown', (e) => {
            this.isDragging = true;
            const rect = this.canvas.getBoundingClientRect();
            const x = e.clientX - rect.left + Number.EPSILON;
            const y = e.clientY - rect.top + Number.EPSILON;

            // Convert to grid coordinates
            this.dragStart = [
                Math.floor(x / this.width),
                Math.floor(y / this.height)
            ];
        });

        this.canvas.addEventListener('mouseup', (e) => {
            this.isDragging = false;
            const rect = this.canvas.getBoundingClientRect();
            const x = e.clientX - rect.left + Number.EPSILON;
            const y = e.clientY - rect.top + Number.EPSILON;

            // Convert to grid coordinates
            this.dragEnd = [Math.floor(x / this.width), Math.floor(y / this.height)];

            // Select all tiles in the drag area
            for (let y = Math.min(this.dragStart[1], this.dragEnd[1]); y <= Math.max(this.dragStart[1], this.dragEnd[1]); y++) {
                for (let x = Math.min(this.dragStart[0], this.dragEnd[0]); x <= Math.max(this.dragStart[0], this.dragEnd[0]); x++) {
                    this.selectedTiles.add(this._2DTo1D(x, y));
                }
            }

            // Redraw with highlights
            this._updateSheetCanvas();
        });

        this.canvas.addEventListener('mousemove', (e) => {
            if (!this.isDragging) {
                return;
            }

            const rect = this.canvas.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;

            // Convert to grid coordinates
            this.dragEnd = [Math.floor(x / this.width), Math.floor(y / this.height)];

            // Redraw with highlights
            this._updateSheetCanvas();
        });
    }

    _teardownClickHandling() {
        this.canvas.removeEventListener('mousedown', this._handleMouseDown);
        this.canvas.removeEventListener('mousemove', this._handleMouseMove);
        window.removeEventListener('mouseup', this._handleMouseUp);
    }

    _addArea() {
        const tiles = new Set(this.selectedTiles);
        if (tiles.size === 0) {
            return;
        }
        const areaId = `area_${Date.now()}`;
        const newArea ={
            id: areaId,
            tiles: tiles,
            color: this.AreaColors[this.AreaColorIndex++ % this.AreaColors.length]  // Cycle through colors
        };

        this.areas.push(newArea);
        console.log('Added area:', newArea);
        this._addMetaDataArea(newArea);
        this.selectedTiles.clear();
        this._updateSheetCanvas();
        return areaId;
    }

    _initMetadata() {
        this.metadata = {
            base: {
                tileWidth: this.width,
                tileHeight: this.height,
                textureHeight: this.textureAtlas.height,
                textureWidth: this.textureAtlas.width,
            }
        };
    }

    _addMetaDataArea(newArea, name='new Area') {
        const newMetadata = {
            id: newArea.id,
            name: name,
            tiles: [],
        };
        for (const tile of newArea.tiles) {
            const [x, y] = this._1DTo2D(tile);
            newMetadata.tiles.push({x, y});
        }
        if (!this.metadata.areas) {
            this.metadata.areas = [];
        }
        this.metadata.areas.push(newMetadata);
    }

    _updateMetadataArea() {

    }

    _removeMetaDataArea(areaId) {
        const index = this.metadata.areas.findIndex(area => area.id === areaId);
        if (index !== -1) {
            this.metadata.areas.splice(index, 1);
            return true;
        }
        return false;
    }

    _updateMetaDataContainer() {
        if (!this.metadataArea) {
            return;
        }
        
        this.metadataArea.innerHTML = '';
        
        // Base metadata section
        const baseSection = document.createElement('div');
        baseSection.className = 'metadata-base';
        baseSection.innerHTML = `
            <h3>Base Information</h3>
            <table>
                <tr><td>Atlas Width:</td><td>${this.metadata.base.textureWidth}</td></tr>
                <tr><td>Atlas Height:</td><td>${this.metadata.base.textureHeight}</td></tr>
                <tr><td>Tile Width:</td><td>${this.metadata.base.tileWidth}</td></tr>
                <tr><td>Tile Height:</td><td>${this.metadata.base.tileHeight}</td></tr>
            </table>
        `;
        this.metadataArea.appendChild(baseSection);
    
        // Areas section
        const areasSection = document.createElement('div');
        areasSection.className = 'metadata-areas';
        areasSection.innerHTML = '<h3>Areas</h3>';
    
        if (this.metadata.areas && this.metadata.areas.length > 0) {
            const areasList = document.createElement('div');
            areasList.className = 'areas-list';
    
            this.metadata.areas.forEach(area => {
                const areaElement = document.createElement('div');
                areaElement.className = 'area-item';
                
                // Area name (double-clickable)
                const nameSpan = document.createElement('span');
                nameSpan.className = 'area-name';
                nameSpan.textContent = area.name;
                nameSpan.addEventListener('dblclick', (e) => {
                    const input = document.createElement('input');
                    input.value = area.name;
                    input.className = 'area-name-input';
                    
                    input.onblur = () => {
                        area.name = input.value;
                        nameSpan.textContent = input.value;
                        input.replaceWith(nameSpan);
                    };
                    
                    input.onkeydown = (e) => {
                        if (e.key === 'Enter') {
                            input.blur();
                        }
                    };
    
                    nameSpan.replaceWith(input);
                    input.focus();
                });
    
                // Area details
                const details = document.createElement('div');
                details.className = 'area-details';
                details.innerHTML = `
                    <div>ID: ${area.id}</div>
                    <div>Tiles: ${area.tiles.length}</div>
                    <div class="tile-coords">
                        ${area.tiles.map(t => `(${t.x},${t.y})`).join(', ')}
                    </div>
                `;
    
                areaElement.appendChild(nameSpan);
                areaElement.appendChild(details);
                areasList.appendChild(areaElement);
            });
    
            areasSection.appendChild(areasList);
        }
        const rawDiv = document.createElement('div');
        const rawJson = document.createElement('pre');
        rawJson.textContent = JSON.stringify(this.metadata, null, 2);
        rawDiv.innerHTML = '<h3>Raw JSON</h3>';
        rawDiv.appendChild(rawJson);
        this.metadataArea.appendChild(areasSection);
        this.metadataArea.appendChild(rawDiv);
    }

    _removeArea(areaId) {
        const index = this.areas.findIndex(area => area.id === areaId);
        if (index !== -1) {
            this.areas.splice(index, 1);
            this._updateSheetCanvas();
            return true;
        }
        return false;
    }

    _drawSheetCanvas() {
        this.sheetCtx.clearRect(0, 0, this.width * this.textures_x, this.height * this.textures_y);
        this.sheetCtx.drawImage(this.textureAtlas, 0, 0);
    }

    _drawSheetGrid() {
        this.sheetCtx.strokeStyle = 'rgba(0, 0, 0, 0.5)';
        this.sheetCtx.lineWidth = 1;

        for (let x = 0; x < this.textures_x; x++) {
            this.sheetCtx.beginPath();
            this.sheetCtx.moveTo(x * this.width, 0);
            this.sheetCtx.lineTo(x * this.width, this.height * this.textures_y);
            this.sheetCtx.stroke();
        }

        for (let y = 0; y < this.textures_y; y++) {
            this.sheetCtx.beginPath();
            this.sheetCtx.moveTo(0, y * this.height);
            this.sheetCtx.lineTo(this.width * this.textures_x, y * this.height);
            this.sheetCtx.stroke();
        }
    }

    _drawSelectedTiles() {
        this.sheetCtx.fillStyle = 'rgba(255, 255, 0, 0.3)'; // Yellow highlight with alpha

        for (const tileIndex of this.selectedTiles) {
            const [x, y] = this._1DTo2D(tileIndex);
            this.sheetCtx.fillRect(
                x * this.width,
                y * this.height,
                this.width,
                this.height
            );
        }

        if (this.isDragging) {
            this.sheetCtx.fillStyle = 'rgba(0, 255, 0, 0.3)'; // Green highlight with alpha
            const x = Math.min(this.dragStart[0], this.dragEnd[0]);
            const y = Math.min(this.dragStart[1], this.dragEnd[1]);
            const width = Math.abs(this.dragStart[0] - this.dragEnd[0]) + 1;
            const height = Math.abs(this.dragStart[1] - this.dragEnd[1]) + 1;
            this.sheetCtx.fillRect(
                x * this.width,
                y * this.height,
                width * this.width,
                height * this.height
            );
        }
    }

    _drawAreas() {
        this.sheetCtx.lineWidth = 2;

        this.areas.forEach(area => {
            this.sheetCtx.strokeStyle = area.color;
            this.sheetCtx.fillStyle = area.color;
            for (const tileIndex of area.tiles) {
                const [x, y] = this._1DTo2D(tileIndex);

                this.sheetCtx.strokeRect(
                    x * this.width,
                    y * this.height,
                    this.width,
                    this.height
                );

                this.sheetCtx.fillRect(
                    x * this.width,
                    y * this.height,
                    this.width,
                    this.height
                );
            }
        });
    }

    _updateSheetCanvas() {
        this._updateMetaDataContainer();
        this._drawSheetCanvas();
        this._drawSheetGrid();
        this._drawSelectedTiles();
        this._drawAreas();
    }

    async _createNewSheetFromSelection() {
        // Find all connected regions with their bboxes
        const regions = this._findConnectedRegions();

        // Calculate total size needed
        let totalHeight = 0;
        let maxWidth = 0;
        for (const region of regions) {
            totalHeight += region.bbox.height;
            maxWidth = Math.max(maxWidth, region.bbox.width);
        }

        // Create destination canvas
        const newCanvas = document.createElement('canvas');
        newCanvas.width = maxWidth * this.width;
        newCanvas.height = totalHeight * this.height;
        const ctx = newCanvas.getContext('2d');

        // Draw each region
        let currentY = 0;
        for (const region of regions) {
            for (const tileIndex of region.tiles) {
                const tile = this.tiles[tileIndex];
                const [origX, origY] = [tile.x, tile.y];

                // Calculate new position relative to region's bbox
                const newX = (origX - region.bbox.x);
                const newY = currentY + (origY - region.bbox.y);

                // Create and draw image from dataURL
                ctx.drawImage(
                    this.textureAtlas,
                    origX * this.width,
                    origY * this.height,
                    this.width,
                    this.height,
                    newX * this.width,
                    newY * this.height,
                    this.width,
                    this.height
                );
            }
            currentY += region.bbox.height;
        }

        return {
            textureAtlas: newCanvas,
            width: this.width,
            height: this.height
        }
    }

    destroy() {
        // Remove event listeners
        this._teardownClickHandling();
        
        // Clean up DOM elements
        this.rootDiv.remove();
        
        // Clear references
        this.tiles = [];
        this.selectedTiles.clear();
        this.areas = [];
    }

    _findConnectedRegions() {
        let remainingTiles = new Set(this.selectedTiles);
        const regions = [];

        while (remainingTiles.size > 0) {
            const iterator = remainingTiles.values();
            const firstResult = iterator.next();
            const tileIndex = firstResult.done ? null : firstResult.value;

            const [x, y] = this._1DTo2D(tileIndex);

            const { connectedTiles, remainingTiles: newRemaining } = this._checkNeighbours(x, y, remainingTiles);
            remainingTiles = newRemaining;

            let minX = Infinity;
            let minY = Infinity;
            let maxX = -Infinity;
            let maxY = -Infinity;

            for (const tileIndex of connectedTiles) {
                const [x, y] = this._1DTo2D(tileIndex);
                minX = Math.min(minX, x);
                minY = Math.min(minY, y);
                maxX = Math.max(maxX, x);
                maxY = Math.max(maxY, y);
            }

            regions.push({
                tiles: connectedTiles,
                bbox: {
                    x: minX,
                    y: minY,
                    width: maxX - minX + 1,
                    height: maxY - minY + 1
                }
            });
        }

        return regions;
    }

    _checkNeighbours(x, y, remainingTiles) {
        const connectedTiles = new Set();
        const toCheck = [[x, y]];
        const neighbors = [
            [-1, -1], [0, -1], [1, -1],  // top row
            [-1, 0], [1, 0],    // middle row (excluding center)
            [-1, 1], [0, 1], [1, 1]    // bottom row
        ];

        while (toCheck.length > 0) {
            const [currentX, currentY] = toCheck.pop();
            const tileIndex = this._2DTo1D(currentX, currentY);

            if (!remainingTiles.has(tileIndex)) {
                continue;
            }

            connectedTiles.add(tileIndex);
            remainingTiles.delete(tileIndex);

            for (const [dx, dy] of neighbors) {
                const newX = currentX + dx;
                const newY = currentY + dy;
                const neighborIndex = this._2DTo1D(newX, newY);

                if (!remainingTiles.has(neighborIndex)) {
                    continue;
                }

                if (newX < 0 || newX >= this.textures_x || newY < 0 || newY >= this.textures_y) {
                    continue;
                }

                toCheck.push([newX, newY]);
            }
        }

        return { connectedTiles, remainingTiles };
    }

    processTiles() {
        // Create single canvas for processing
        const tileCache = document.createElement('canvas');
        tileCache.width = this.textureAtlas.width;
        tileCache.height = this.textureAtlas.height;
        const ctx = tileCache.getContext('2d', { willReadFrequently: true });
        
        // Draw atlas once
        ctx.drawImage(this.textureAtlas, 0, 0);
        
        // Get all pixel data at once
        const fullImageData = ctx.getImageData(0, 0, tileCache.width, tileCache.height);
        const data = fullImageData.data;
    
        // Process each tile
        for (let y = 0; y < this.textures_y; y++) {
            for (let x = 0; x < this.textures_x; x++) {
                // Sample 5 points for isEmpty check
                const isEmpty = this._isTileEmpty(data, x, y, tileCache.width);
                
                this.tiles[this._2DTo1D(x, y)] = {
                    x: x,
                    y: y,
                    isEmpty: isEmpty,
                    bounds: {
                        x: x * this.width,
                        y: y * this.height,
                        width: this.width,
                        height: this.height
                    }
                };
            }
        }
    }
    
    _isTileEmpty(imageData, tileX, tileY, fullWidth) {
        // Get tile bounds
        const startX = tileX * this.width;
        const startY = tileY * this.height;
        
        // Sample points: center and 4 corners
        const points = [
            [startX + this.width/2, startY + this.height/2],  // Center
            [startX, startY],                                 // Top-left
            [startX + this.width - 1, startY],               // Top-right
            [startX, startY + this.height - 1],              // Bottom-left
            [startX + this.width - 1, startY + this.height - 1] // Bottom-right
        ];
    
        // Check alpha value at each point
        for (const [x, y] of points) {
            const index = (y * fullWidth + x) * 4;
            if (imageData[index + 3] > 0) {
                return false;
            }
        }
    }

    _1DTo2D(index) {
        return [index % this.textures_x, Math.floor(index / this.textures_x)];
    }

    _2DTo1D(x, y) {
        return y * this.textures_x + x;
    }
}