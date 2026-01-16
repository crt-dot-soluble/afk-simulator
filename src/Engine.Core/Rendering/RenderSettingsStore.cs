namespace Engine.Core.Rendering;

public sealed class RenderSettingsStore
{
    private RenderSettings _current = RenderSettings.Balanced;
    private readonly object _gate = new();

    public RenderSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public RenderSettings Update(RenderSettings settings)
    {
        RenderSettingsValidator.Validate(settings);
        lock (_gate)
        {
            _current = settings;
            return _current;
        }
    }
}
