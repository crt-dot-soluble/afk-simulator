const defaultState = {
    targetFps: 60,
    resolutionScale: 1,
    spriteSmoothing: true,
    particleDensity: 60,
    gpuTier: "tier2"
};

const renderHostInternal = {
    gl: null,
    canvas: null,
    state: { ...defaultState },
    raf: 0,
    hue: 0,
    lastFrame: 0
};

function configureCanvas(canvas, state) {
    renderHostInternal.canvas = canvas;
    renderHostInternal.state = { ...renderHostInternal.state, ...state };
    const context =
        canvas.getContext("webgl2", { antialias: state.spriteSmoothing }) ??
        canvas.getContext("webgl", { antialias: state.spriteSmoothing });
    renderHostInternal.gl = context;
    if (!context) {
        console.warn("WebGL unavailable; falling back to 2d context");
        renderHostInternal.gl = null;
    }
}

function frame(timestamp) {
    const { gl, canvas, state } = renderHostInternal;
    if (!canvas) {
        return;
    }

    const frameMs = 1000 / state.targetFps;
    if (!renderHostInternal.lastFrame) {
        renderHostInternal.lastFrame = timestamp;
    }

    const delta = timestamp - renderHostInternal.lastFrame;
    if (delta < frameMs) {
        renderHostInternal.raf = requestAnimationFrame(frame);
        return;
    }

    renderHostInternal.lastFrame = timestamp;
    renderHostInternal.hue = (renderHostInternal.hue + 0.1 * state.resolutionScale) % 360;

    if (gl) {
        const scale = state.resolutionScale;
        const width = Math.floor(canvas.width * scale);
        const height = Math.floor(canvas.height * scale);
        gl.viewport(0, 0, width, height);
        const shade = Math.abs(Math.sin(timestamp / 800)) * 0.6 + 0.2;
        gl.clearColor(shade, 0.08 * state.particleDensity / 100, 0.3, 1);
        gl.clear(gl.COLOR_BUFFER_BIT);
    } else {
        const ctx2d = canvas.getContext("2d");
        if (ctx2d) {
            ctx2d.clearRect(0, 0, canvas.width, canvas.height);
            const gradient = ctx2d.createLinearGradient(0, 0, canvas.width, canvas.height);
            gradient.addColorStop(0, `hsl(${renderHostInternal.hue}, 80%, 40%)`);
            gradient.addColorStop(1, `hsl(${(renderHostInternal.hue + 60) % 360}, 80%, 20%)`);
            ctx2d.fillStyle = gradient;
            ctx2d.fillRect(0, 0, canvas.width, canvas.height);
        }
    }

    renderHostInternal.raf = requestAnimationFrame(frame);
}

export const renderHost = {
    start(canvas, settings) {
        configureCanvas(canvas, settings ?? defaultState);
        renderHostInternal.lastFrame = 0;
        cancelAnimationFrame(renderHostInternal.raf);
        renderHostInternal.raf = requestAnimationFrame(frame);
    },
    update(settings) {
        renderHostInternal.state = { ...renderHostInternal.state, ...settings };
    }
};
