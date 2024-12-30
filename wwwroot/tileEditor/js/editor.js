import { Sheet } from './sheet.js';

export class Editor {
    constructor(options = {}) {
        this.container = null;
        this.sheets = new Map();
        this.activeSheetId = null;
        this.tabContainer = null;
        this.workspaceContainer = null;
        this.debug = options.debug || false;
    }

    setup(containerId) {
        this.container = typeof containerId === 'string' 
            ? document.getElementById(containerId)
            : containerId;

        if (!this.container) {
            throw new Error('Container not found');
        }

        this._createLayout();
    }

    _createLayout() {
        // Main container setup
        this.container.classList.add('sprite-editor-container');

        // Create tab container
        this.tabContainer = document.createElement('div');
        this.tabContainer.classList.add('sprite-editor-tabs');
        this.container.appendChild(this.tabContainer);

        // Create workspace container
        this.workspaceContainer = document.createElement('div');
        this.workspaceContainer.classList.add('sprite-editor-workspace');
        this.container.appendChild(this.workspaceContainer);

        // Create main toolbar
        this._createMainToolbar();
    }

    _createMainToolbar() {
        const toolbar = document.createElement('div');
        toolbar.classList.add('sprite-editor-main-toolbar');

        const buttons = [
            { text: 'New Sheet', onClick: () => this._handleNewSheet() },
            { text: 'New Sheet from atlas', onClick: () => this._handleOpenSheet() }
        ];

        buttons.forEach(({ text, onClick }) => {
            const button = document.createElement('button');
            button.textContent = text;
            button.classList.add('sprite-editor-button');
            button.onclick = onClick;
            toolbar.appendChild(button);
        });

        this.container.insertBefore(toolbar, this.tabContainer);
    }

    _createNewSheetFromSelection = async (newSheetData) => {
        // width, height = tile size
        const { textureAtlas, width, height } = newSheetData;

        // to Image
        const img = new Image();
        await new Promise(resolve => {
            img.onload = resolve;
            img.src = textureAtlas.toDataURL();
        });

        // new sheet
        const sheetId = `sheet_${Date.now()}`;
        const sheet = new Sheet({
            debug: this.debug,
            createFromSelection: this._createNewSheetFromSelection,
            textureAtlas: img,
            width: textureAtlas.width,
            height: textureAtlas.height,
            gridSize: { width: width, height: height }
        });

        this.sheets.set(sheetId, sheet);
        this._addTab(sheetId, 'Selection Sheet');
        this.setActiveSheet(sheetId);
    }

    async _handleNewSheet() {
        const sheetId = `sheet_${Date.now()}`;
        const sheet = new Sheet({
            debug: this.debug,
            createFromSelection: this._createNewSheetFromSelection,
            width: 800,
            height: 600,
            gridSize: { width: 32, height: 32 }
        });

        this.sheets.set(sheetId, sheet);
        this._addTab(sheetId, 'New Sheet');
        this.setActiveSheet(sheetId);
    }

    async _handleOpenSheet() {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/*';

        input.onchange = async (e) => {
            const file = e.target.files[0];
            if (!file) return;

            const sheetId = `sheet_${Date.now()}`;
            const img = await this._loadImage(file);
            
            const sheet = new Sheet({
                debug: this.debug,
                createFromSelection: this._createNewSheetFromSelection,
                textureAtlas: img,
                width: img.width, 
                height: img.height, 
                gridSize: { width: 32, height: 32 }
            });

            this.sheets.set(sheetId, sheet);
            this._addTab(sheetId, file.name);
            this.setActiveSheet(sheetId);
        };

        input.click();
    }

    _addTab(sheetId, label) {
        const tab = document.createElement('div');
        tab.id = `tab_${sheetId}`;
        tab.classList.add('sprite-editor-tab');
        
        const tabLabel = document.createElement('span');
        tabLabel.addEventListener('dblclick', () => {
            const input = document.createElement('input');
            input.value = tabLabel.textContent;
            input.onblur = () => {
                tabLabel.textContent = input.value;
                input.remove();
            };
            tabLabel.textContent = '';
            tabLabel.appendChild(input);
            input.focus();

            input.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    tabLabel.textContent = input.value;
                    input.remove();
                }
            }
            );
        });


        tabLabel.textContent = label;
        tab.appendChild(tabLabel);

        const closeBtn = document.createElement('button');
        closeBtn.textContent = '×';
        closeBtn.classList.add('sprite-editor-tab-close');
        closeBtn.onclick = (e) => {
            e.stopPropagation();
            this._closeSheet(sheetId);
        };
        tab.appendChild(closeBtn);

        tab.onclick = () => this.setActiveSheet(sheetId);
        
        this.tabContainer.appendChild(tab);
    }

    _closeSheet(sheetId) {
        const sheet = this.sheets.get(sheetId);
        if (sheet) {
            sheet.destroy();
            this.sheets.delete(sheetId);
            
            // Remove tab
            const tab = document.getElementById(`tab_${sheetId}`);
            tab?.remove();
            
            // If this was the active sheet, activate another one
            if (this.activeSheetId === sheetId) {
                const remainingSheets = Array.from(this.sheets.keys());
                if (remainingSheets.length > 0) {
                    this.setActiveSheet(remainingSheets[0]);
                } else {
                    this.activeSheetId = null;
                }
            }
        }
    }

    setActiveSheet(sheetId) {
        // Hide current sheet if exists
        if (this.activeSheetId) {
            const currentSheet = this.sheets.get(this.activeSheetId);
            currentSheet?.hide();
            
            // Update tab state
            const currentTab = this.tabContainer.children[
                Array.from(this.sheets.keys()).indexOf(this.activeSheetId)
            ];
            currentTab?.classList.remove('active');
        }

        // Show new sheet
        const sheet = this.sheets.get(sheetId);
        if (sheet) {
            this.activeSheetId = sheetId;
            sheet.show(this.workspaceContainer);
            
            // Update tab state
            const newTab = this.tabContainer.children[
                Array.from(this.sheets.keys()).indexOf(sheetId)
            ];
            newTab?.classList.add('active');
        }
    }


    _showAreaDialog(sheet, area) {
        const dialog = document.createElement('div');
        dialog.classList.add('sprite-editor-dialog');

        const content = document.createElement('div');
        content.classList.add('sprite-editor-dialog-content');

        const nameInput = document.createElement('input');
        nameInput.type = 'text';
        nameInput.placeholder = 'Area Name';
        nameInput.value = area.name || '';

        const addButton = document.createElement('button');
        addButton.textContent = 'Add Area';
        addButton.onclick = () => {
            area.name = nameInput.value;
            sheet.addArea(area);
            dialog.remove();
        };

        const cancelButton = document.createElement('button');
        cancelButton.textContent = 'Cancel';
        cancelButton.onclick = () => dialog.remove();

        content.appendChild(nameInput);
        content.appendChild(addButton);
        content.appendChild(cancelButton);
        dialog.appendChild(content);

        this.container.appendChild(dialog);
    }

    async _loadImage(file) {
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
}

window.addEventListener('load', () => {
    const editor = new Editor();
    editor.setup('editorContainer');
});