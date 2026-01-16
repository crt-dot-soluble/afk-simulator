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

    public async Task InitializeAsync(ElementReference canvas, RenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/render-host.js")
            .ConfigureAwait(false);
        await _module.InvokeVoidAsync("renderHost.start", canvas, Serialize(settings)).ConfigureAwait(false);
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

    private static object Serialize(RenderSettings settings) => new
    {
        targetFps = settings.TargetFps,
        resolutionScale = settings.ResolutionScale,
        spriteSmoothing = settings.SpriteSmoothing,
        particleDensity = settings.ParticleDensity,
        gpuTier = settings.GpuTier
    };

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync().ConfigureAwait(false);
        }
    }
}
