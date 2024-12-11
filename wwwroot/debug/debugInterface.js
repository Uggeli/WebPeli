class DebugInterface {
    constructor() {
        this.cleanup();  // Clean up any existing instance
        
        // Store bound methods
        this.handleMessage = this.handleMessage.bind(this);
        this.connect = this.connect.bind(this);
        this.reconnect = this.reconnect.bind(this);
        // this.executeCommand = this.executeCommand.bind(this);
        this.ws = null;
        this.performanceChart = null;
        this.debugData = {};
        this.logBuffer = [];
        this.MAX_LOG_BUFFER = 1000;

        this.MessageType = {
            DebugRequest: 0x40,
            DebugResponse: 0x41,
            DebugData: 0x42,
            Error: 0xFF,
            LogMessages: 0x43
        };

        this.DebugRequestType = {
            ToggleDebugMode: 0,
            TogglePathfinding: 1,
            RequestFullState: 2,
            RequestFullLog: 3
        };

        this.initEventListeners();
        this.connect();
    }

    initEventListeners() {
        // Button event listeners
        const buttons = {
            'debugModeBtn': () => this.toggleDebugMode(),
            'pathfindingBtn': () => this.togglePathfinding(),
            'reconnectBtn': () => this.reconnect(),
            'exportBtn': () => this.exportData(),
            'clearConsoleBtn': () => this.clearConsole(),
            'runCommandBtn': () => this.executeCommand(),
            'clearLogsBtn': () => this.clearLogs()
        };

        Object.entries(buttons).forEach(([id, handler]) => {
            document.getElementById(id)?.addEventListener('click', handler.bind(this));
        });

        // Command input event listener
        document.getElementById('commandInput')?.addEventListener('keydown', (event) => {
            if (event.key === 'Enter') {
                this.executeCommand();
            }
        });

        // Filter event listeners
        ['logCategory', 'logLevel'].forEach(id => {
            document.getElementById(id)?.addEventListener('change', () => this.updateLogFilter());
        });
    }

    connect() {
        this.updateConnectionStatus('connecting');
        this.ws = new WebSocket(`ws://${window.location.host}/debugws`);
        this.ws.binaryType = 'arraybuffer';

        this.ws.addEventListener('open', () => {
            this.updateConnectionStatus('connected');
            this.initPerformanceChart();
        });

        this.ws.addEventListener('close', () => {
            this.updateConnectionStatus('disconnected');
            setTimeout(() => this.connect(), 5000);
        });

        this.ws.addEventListener('error', (error) => {
            this.showError('WebSocket error occurred');
            console.error('WebSocket error:', error);
        });

        this.ws.addEventListener('message', (event) => this.handleMessage(event));
    }

    handleMessage(event) {
        try {
            const buffer = event.data;
            const messageType = new Uint8Array(buffer, 0, 1)[0];
            const decoder = new TextDecoder();
            const messageData = decoder.decode(buffer.slice(3));

            switch (messageType) {
                case this.MessageType.Error:
                    this.showError(messageData);
                    break;
                case this.MessageType.DebugResponse:
                    this.logToConsole('success', messageData);
                    break;
                case this.MessageType.DebugData:
                    this.debugData = JSON.parse(messageData);
                    this.updateDebugInfo(this.debugData);
                    break;
                default:
                    this.logToConsole('info', `Unknown message type: ${messageType}`);
            }
        } catch (error) {
            this.showError('Error processing message');
            console.error(error);
        }
    }

    showError(message) {
        const banner = document.getElementById('errorBanner');
        if (banner) {
            banner.textContent = message;
            banner.style.display = 'block';
            setTimeout(() => { banner.style.display = 'none'; }, 5000);
        }
        this.logToConsole('error', message);
    }

    updateConnectionStatus(status) {
        const indicator = document.getElementById('statusIndicator');
        const statusText = document.getElementById('statusText');
        const reconnectBtn = document.getElementById('reconnectBtn');

        if (indicator && statusText && reconnectBtn) {
            indicator.className = `status-indicator status-${status}`;
            statusText.textContent = status.charAt(0).toUpperCase() + status.slice(1);
            reconnectBtn.disabled = status === 'connected';
        }

        this.logToConsole('info', `WebSocket ${status}`);
    }

    handleLogMessages(messages) {
        console.log('Received log messages:', messages);
        const logConsole = document.getElementById('logConsole');
        if (!logConsole) return;

        const shouldScroll = logConsole.scrollTop + logConsole.clientHeight === logConsole.scrollHeight;

        messages.forEach(msg => {
            // Add to buffer
            this.logBuffer.push(msg);
            if (this.logBuffer.length > this.MAX_LOG_BUFFER) {
                this.logBuffer.shift();
            }

            // Create log entry
            const entry = document.createElement('div');
            entry.className = 'log-entry';

            const timestamp = new Date(msg.timestamp).toLocaleTimeString();
            const level = msg.level.toLowerCase();

            entry.innerHTML = `
                <span class="timestamp">${timestamp}</span>
                <span class="category">[${this.escapeHtml(msg.category)}]</span>
                <span class="level level-${level}">${msg.level}</span>
                <span class="message">${this.escapeHtml(msg.message)}</span>
                ${msg.exception ? `<div class="exception">${this.escapeHtml(msg.exception)}</div>` : ''}
            `;

            logConsole.appendChild(entry);
        });

        if (shouldScroll) {
            logConsole.scrollTop = logConsole.scrollHeight;
        }

        this.updateLogFilters();
    }

    escapeHtml(unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    updateDebugInfo(data) {
        // Update time system info
        ['season', 'timeOfDay', 'day', 'year'].forEach(id => {
            const element = document.getElementById(id);
            if (element) element.textContent = data[id] || '-';
        });

        // Update entity stats
        ['totalEntities', 'activeEntities', 'movingEntities'].forEach(id => {
            const element = document.getElementById(id);
            if (element) element.textContent = data[id] || '-';
        });

        // Update system status
        ['debugMode', 'pathfindingDebug', 'activeViewports'].forEach(id => {
            const element = document.getElementById(id);
            if (element) element.textContent = data[id] || '-';
        });

        // Update performance chart
        if (data.performanceData) {
            this.updatePerformanceChart(data.performanceData);
            this.updatePerformanceFilter(data.performanceData);
        }
        // console.log('Data:', data);
        // Update log messages
        if (data.newLogMessages) {
            this.handleLogMessages(data.newLogMessages);
        }
    }

    initPerformanceChart() {
        const ctx = document.getElementById('performance-chart')?.getContext('2d');
        if (!ctx) return;

        this.performanceChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: []
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                scales: {
                    y: {
                        beginAtZero: true,
                        title: {
                            display: true,
                            text: 'Time (ms)'
                        },
                        grid: {
                            color: 'rgba(255, 255, 255, 0.1)'
                        }
                    },
                    x: {
                        grid: {
                            color: 'rgba(255, 255, 255, 0.1)'
                        }
                    }
                },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            boxWidth: 12,
                            usePointStyle: true,
                            color: '#fff'
                        }
                    }
                }
            }
        });
    }

    updatePerformanceChart(performanceData) {
        if (!this.performanceChart || !performanceData) return;

        const maxDataPoints = 25;
        const selectedSystem = document.getElementById('performanceFilter')?.value || 'all';

        this.performanceChart.data.labels.push(new Date().toLocaleTimeString());
        if (this.performanceChart.data.labels.length > maxDataPoints) {
            this.performanceChart.data.labels.shift();
        }

        Object.entries(performanceData).forEach(([system, time]) => {
            if (selectedSystem !== 'all' && system !== selectedSystem) return;

            let dataset = this.performanceChart.data.datasets.find(ds => ds.label === system);
            if (!dataset) {
                const hue = Math.random() * 360;
                dataset = {
                    label: system,
                    data: [],
                    borderColor: `hsl(${hue}, 70%, 50%)`,
                    tension: 0.4,
                    fill: false
                };
                this.performanceChart.data.datasets.push(dataset);
            }

            dataset.data.push(time);
            if (dataset.data.length > maxDataPoints) {
                dataset.data.shift();
            }
        });

        this.performanceChart.update();
    }

    updatePerformanceFilter(performanceData) {
        const filter = document.getElementById('performanceFilter');
        if (!filter || !performanceData) return;

        Object.keys(performanceData).forEach(system => {
            if (!filter.querySelector(`option[value="${system}"]`)) {
                const option = document.createElement('option');
                option.value = system;
                option.textContent = system;
                filter.appendChild(option);
            }
        });
    }

    logToConsole(type, message) {
        const console = document.getElementById('console');
        if (!console) return;

        const timestamp = new Date().toLocaleTimeString();
        const entry = document.createElement('div');
        entry.innerHTML = `<span class="timestamp">${timestamp}</span><span class="${type}">${message}</span>`;
        console.appendChild(entry);
        console.scrollTop = console.scrollHeight;
    }

    clearConsole() {
        const console = document.getElementById('console');
        if (console) {
            console.innerHTML = '';
            this.logToConsole('info', 'Console cleared');
        }
    }

    clearLogs() {
        const logConsole = document.getElementById('logConsole');
        if (logConsole) {
            logConsole.innerHTML = '';
            this.logBuffer = [];
        }
    }

    updateLogFilter() {
        const category = document.getElementById('logCategory')?.value;
        const level = document.getElementById('logLevel')?.value;

        const request = {
            type: 'RequestFullLog',
            category: category || null,
            minLevel: level || null,
            limit: 100
        };

        this.sendDebugRequest(this.DebugRequestType.RequestFullLog);
    }

    updateLogFilters() {
        const categorySelect = document.getElementById('logCategory');
        if (!categorySelect) return;

        const categories = new Set(this.logBuffer.map(msg => msg.category));
        const currentValue = categorySelect.value;

        categorySelect.innerHTML = '<option value="">All Categories</option>';
        [...categories].sort().forEach(category => {
            const option = document.createElement('option');
            option.value = category;
            option.textContent = category;
            categorySelect.appendChild(option);
        });

        categorySelect.value = currentValue;
    }

    sendDebugRequest(requestType) {
        if (this.ws?.readyState === WebSocket.OPEN) {
            const buffer = new ArrayBuffer(4);
            const view = new DataView(buffer);
            view.setUint8(0, this.MessageType.DebugRequest);
            view.setUint16(1, 1, true);
            view.setUint8(3, requestType);
            this.ws.send(buffer);
        } else {
            this.showError('WebSocket not connected');
        }
    }

    toggleDebugMode() {
        this.sendDebugRequest(this.DebugRequestType.ToggleDebugMode);
    }

    togglePathfinding() {
        this.sendDebugRequest(this.DebugRequestType.TogglePathfinding);
    }

    cleanup() {
        // Clean up WebSocket
        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }

        // Clean up Chart.js instance
        if (this.performanceChart) {
            this.performanceChart.destroy();
            this.performanceChart = null;
        }

        // Clean up any existing event listeners
        this.removeEventListeners();
    }

    removeEventListeners() {
        const elements = {
            'debugModeBtn': this.toggleDebugMode,
            'pathfindingBtn': this.togglePathfinding,
            'reconnectBtn': this.reconnect,
            'exportBtn': this.exportData,
            'clearConsoleBtn': this.clearConsole,
            'runCommandBtn': this.executeCommand,
            'clearLogsBtn': this.clearLogs,
            'commandInput': (e) => e.key === 'Enter' && this.executeCommand()
        };

        Object.entries(elements).forEach(([id, handler]) => {
            const element = document.getElementById(id);
            if (element) {
                if (id === 'commandInput') {
                    element.removeEventListener('keydown', handler);
                } else {
                    element.removeEventListener('click', handler);
                }
            }
        });
    }

    reconnect() {
        this.cleanup();
        this.connect();
    }

    async connect() {
        let retryCount = 0;
        const maxRetries = 5;
        const retryDelay = 5000;

        const tryConnect = async () => {
            try {
                this.updateConnectionStatus('connecting');
                this.ws = new WebSocket(`ws://${window.location.host}/debugws`);
                this.ws.binaryType = 'arraybuffer';

                // Set up WebSocket event listeners
                this.ws.addEventListener('open', () => {
                    this.updateConnectionStatus('connected');
                    this.initPerformanceChart();
                    retryCount = 0; // Reset retry count on successful connection
                });

                this.ws.addEventListener('close', async () => {
                    this.updateConnectionStatus('disconnected');
                    if (retryCount < maxRetries) {
                        retryCount++;
                        await new Promise(resolve => setTimeout(resolve, retryDelay));
                        tryConnect();
                    } else {
                        this.showError('Maximum reconnection attempts reached. Please try manually reconnecting.');
                    }
                });

                this.ws.addEventListener('error', (error) => {
                    this.showError('WebSocket error occurred');
                    console.error('WebSocket error:', error);
                });

                this.ws.addEventListener('message', this.handleMessage);
            } catch (error) {
                console.error('Connection error:', error);
                this.showError(`Failed to connect: ${error.message}`);
            }
        };

        await tryConnect();
    }

    exportData() {
        const exportData = {
            timestamp: new Date().toISOString(),
            debugState: this.debugData,
            performanceHistory: this.performanceChart?.data || null,
            logs: this.logBuffer
        };

        const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);

        const a = document.createElement('a');
        a.href = url;
        a.download = `webpeli-debug-${new Date().toISOString()}.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);

        this.logToConsole('success', 'Debug data exported');
    }
}

// Initialize the debug interface when the page loads
document.addEventListener('DOMContentLoaded', () => {
    window.debugInterface = new DebugInterface();
});