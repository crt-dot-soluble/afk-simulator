using System.Linq;
using Engine.Client.Rendering;
using Engine.Core.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Engine.Client.Services;

internal sealed class RenderLoopService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public RenderLoopService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private ElementReference? _canvas;

    public async Task InitializeAsync(ElementReference canvas, RenderSettings settings,
        SpriteAnimationDescriptor? animation = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/render-host.js")
            .ConfigureAwait(false);
        _canvas = canvas;
        await _module.InvokeVoidAsync("renderHost.start", canvas, new
        {
            settings = Serialize(settings),
            animation = SerializeAnimation(animation)
        }).ConfigureAwait(false);
    }

    public async Task UpdateSettingsAsync(RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (_module is null)
        {
            return;
        }

        await _module.InvokeVoidAsync("renderHost.update", Serialize(settings)).ConfigureAwait(false);
    }

    public async Task PlayAnimationAsync(SpriteAnimationDescriptor animation)
    {
        if (_module is null || _canvas is null)
        {
            return;
        }

        await _module.InvokeVoidAsync("renderHost.play",
            SerializeAnimation(animation)).ConfigureAwait(false);
    }

    private static object Serialize(RenderSettings settings) => new
    {
        targetFps = settings.TargetFps,
        resolutionScale = settings.ResolutionScale,
        spriteSmoothing = settings.SpriteSmoothing,
        particleDensity = settings.ParticleDensity,
        gpuTier = settings.GpuTier
    };

    private static object? SerializeAnimation(SpriteAnimationDescriptor? animation)
    {
        if (animation is null)
        {
            return null;
        }

        return new
        {
            spriteId = animation.SpriteId,
            animation = animation.Animation,
            imageUrl = animation.ImageUrl,
            frameDurationMs = animation.FrameDurationMs,
            loop = animation.Loop,
            accentColor = animation.AccentColor,
            frames = animation.Frames.Select(frame => new
            {
                frame.Index,
                frame.X,
                frame.Y,
                frame.Width,
                frame.Height
            }).ToArray()
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync().ConfigureAwait(false);
        }
    }
}
