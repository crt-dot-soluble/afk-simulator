namespace Engine.Core.Resources;

public sealed record ResourceNodeDefinition(
    string Id,
    double Capacity,
    double GenerationPerSecond = 0d,
    double Minimum = 0d)
{
    public double Clamp(double value) => Math.Clamp(value, Minimum, Capacity);
}

public sealed record ResourceEdgeDefinition(
    string Id,
    string SourceId,
    string TargetId,
    double RatePerSecond,
    double Efficiency = 1d);

public sealed record ResourceSnapshot(string Id, double Value);
