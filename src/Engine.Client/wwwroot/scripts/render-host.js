const defaultState = {
    targetFps: 60,
    resolutionScale: 1,
    spriteSmoothing: true,
    particleDensity: 60,
    gpuTier: "tier2"
};

const renderHostInternal = {
    canvas: null,
    state: { ...defaultState },
    raf: 0,
    hue: 0,
    lastFrame: 0,
    animation: null
};

function normalizePayload(payload) {
    if (!payload) {
        return { settings: defaultState, animation: null };
    }

    if (payload.settings || payload.animation) {
        return {
            settings: { ...defaultState, ...(payload.settings ?? {}) },
            animation: payload.animation ?? null
        };
    }

    return { settings: { ...defaultState, ...payload }, animation: null };
}

function configureCanvas(canvas, state) {
    renderHostInternal.canvas = canvas;
    renderHostInternal.state = { ...renderHostInternal.state, ...state };
}

function configureAnimation(animation) {
    if (!animation) {
        renderHostInternal.animation = null;
        return;
    }

    const sprite = new Image();
    sprite.src = animation.imageUrl ?? "";
    renderHostInternal.animation = {
        ...animation,
        sprite,
        frameIndex: 0,
        frameElapsed: 0
    };
}

function frame(timestamp) {
    const { canvas, state } = renderHostInternal;
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

    const ctx2d = canvas.getContext("2d");
    if (ctx2d) {
        ctx2d.clearRect(0, 0, canvas.width, canvas.height);
        const gradient = ctx2d.createLinearGradient(0, 0, canvas.width, canvas.height);
        gradient.addColorStop(0, `hsl(${renderHostInternal.hue}, 80%, 40%)`);
        gradient.addColorStop(1, `hsl(${(renderHostInternal.hue + 60) % 360}, 80%, 20%)`);
        ctx2d.fillStyle = gradient;
        ctx2d.fillRect(0, 0, canvas.width, canvas.height);
        renderAnimation2d(ctx2d, delta);
    }

    renderHostInternal.raf = requestAnimationFrame(frame);
}

function renderAnimation2d(ctx, delta) {
    const animation = renderHostInternal.animation;
    if (!animation || !animation.frames || animation.frames.length === 0) {
        return;
    }

    const sprite = animation.sprite;
    if (!sprite.complete) {
        return;
    }

    const frameDuration = animation.frameDurationMs ?? 120;
    animation.frameElapsed += delta;
    if (animation.frameElapsed >= frameDuration) {
        const steps = Math.floor(animation.frameElapsed / frameDuration);
        animation.frameElapsed -= steps * frameDuration;
        animation.frameIndex = (animation.frameIndex + steps) % animation.frames.length;
    }

    const frame = animation.frames[animation.frameIndex];
    if (!frame) {
        return;
    }

    const renderSize = Math.min(ctx.canvas.width, ctx.canvas.height) * 0.7;
    const offsetX = (ctx.canvas.width - renderSize) / 2;
    const offsetY = ctx.canvas.height - renderSize * 0.9;

    ctx.save();
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(
        sprite,
        frame.x,
        frame.y,
        frame.width,
        frame.height,
        offsetX,
        offsetY,
        renderSize,
        renderSize
    );

    if (animation.accentColor) {
        ctx.strokeStyle = animation.accentColor;
        ctx.lineWidth = 6;
        ctx.strokeRect(offsetX, offsetY, renderSize, renderSize);
    }

    ctx.restore();
}

export const renderHost = {
    start(canvas, payload) {
        const normalized = normalizePayload(payload);
        configureCanvas(canvas, normalized.settings);
        configureAnimation(normalized.animation);
        renderHostInternal.lastFrame = 0;
        cancelAnimationFrame(renderHostInternal.raf);
        renderHostInternal.raf = requestAnimationFrame(frame);
    },
    update(settings) {
        renderHostInternal.state = { ...renderHostInternal.state, ...settings };
    },
    play(animation) {
        configureAnimation(animation);
    }
};
