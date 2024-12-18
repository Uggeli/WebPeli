async function initGame() {
    const renderer = new GameRenderer(document.getElementById('gameCanvas'));
    
    const client = new GameClient('ws://' + window.location.host + '/ws');
    await client.connect();
    
    renderer.setClient(client);
    renderer.requestViewport();
}

window.addEventListener('load', initGame);