using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using Engine.Core.Contracts;
using Engine.Core.DeveloperTools;

namespace Engine.Core.Diagnostics;

/// <summary>
/// Provides synthetic data, commands, and inspectable properties for developer tooling scenarios.
/// </summary>
public sealed class DiagnosticsModule : IModuleContract, IModuleDescriptorSource
{
    private readonly DeveloperProfileStore _profiles;
    private readonly ISystemClock _clock;
    private double _trend;
    private static readonly string[] DescriptorCapabilities = { "developer-tools", "telemetry" };
    private static readonly string[] DescriptorTelemetry = { "diagnostics.noise.sample" };

    public DiagnosticsModule(DeveloperProfileStore profiles, ISystemClock clock)
    {
        _profiles = profiles;
        _clock = clock;
    }

    public string Name => "Diagnostics";
    public string Version => "0.1.0";

    [ModuleInspectable("Noise amplitude", Description = "Scales generated telemetry samples (0.1 - 10)",
        Group = "Signals")]
    public double NoiseAmplitude { get; set; } = 1.25d;

    [ModuleInspectable("Sandbox bots", Description = "Synthetic workers emitting preview data", Group = "Agents")]
    public int SandboxBots { get; set; } = 3;

    [ModuleInspectable("Sandbox mode", Description = "Toggle sample data seeding", Group = "Agents")]
    public bool SandboxMode { get; set; } = true;

    public ValueTask InitializeAsync(ModuleContext context, CancellationToken cancellationToken = default)
    {
        if (SandboxMode)
        {
            SeedSampleProfiles();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<ModuleHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var details = new Dictionary<string, string>
        {
            ["noiseAmplitude"] = NoiseAmplitude.ToString("0.##", CultureInfo.InvariantCulture),
            ["sandboxBots"] = SandboxBots.ToString(CultureInfo.InvariantCulture)
        };
        return ValueTask.FromResult(ModuleHealth.Healthy(details));
    }

    [ModuleCommand("generate-noise", Description = "Emit pseudo-random telemetry samples", IsQuery = true)]
    public IReadOnlyList<double> GenerateNoise(int samples = 16)
    {
        var size = Math.Clamp(samples, 1, 128);
        var payload = new double[size];
        for (var i = 0; i < size; i++)
        {
            payload[i] = NextSample();
        }

        return payload;
    }

    [ModuleCommand("recent-noise", Description = "Snapshot of last 16 generated samples", IsQuery = true)]
    public IReadOnlyDictionary<string, double> GetRecentNoise()
    {
        var payload = new Dictionary<string, double>();
        for (var i = 0; i < 16; i++)
        {
            payload[$"t{i:00}"] = NextSample();
        }

        return payload;
    }

    [ModuleCommand("profile-snapshot", Description = "Dump developer profile metadata", IsQuery = true)]
    public IReadOnlyCollection<DeveloperProfile> SnapshotProfiles() => _profiles.List();

    [ModuleCommand("seed-profiles", Description = "Reset sample profiles with canned data")]
    public ValueTask SeedProfilesAsync()
    {
        _profiles.Clear();
        SeedSampleProfiles();
        return ValueTask.CompletedTask;
    }

    [ModuleCommand("clear-profiles", Description = "Remove all cached profiles")]
    public ValueTask ClearProfilesAsync()
    {
        _profiles.Clear();
        return ValueTask.CompletedTask;
    }

    public ModuleDescriptor Describe()
    {
        return new ModuleDescriptor(
            Name,
            Version,
            DescriptorCapabilities,
            Array.Empty<string>(),
            DescriptorTelemetry,
            "Synthetic diagnostics and tooling playground",
            new Dictionary<string, string>
            {
                ["owner"] = "tooling",
                ["tier"] = "utility"
            });
    }

    private double NextSample()
    {
        var jitter = (NextNormalized() * 2 - 1) * NoiseAmplitude;
        _trend = (_trend * 0.85d) + jitter + SandboxBots * 0.01d;
        return Math.Round(_trend, 3);
    }

    private static double NextNormalized()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        var value = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        return value / (double)ulong.MaxValue;
    }

    private void SeedSampleProfiles()
    {
        var now = _clock.UtcNow;
        _profiles.Upsert("sandbox-default", new Dictionary<string, string>
        {
            ["lastNoise"] = NextSample().ToString(CultureInfo.InvariantCulture),
            ["timestamp"] = now.ToString("O", CultureInfo.InvariantCulture)
        });

        _profiles.Upsert("gpu-playground", new Dictionary<string, string>
        {
            ["shaders"] = "18",
            ["enabled"] = "true"
        });

        _profiles.Upsert("multiplayer-lab", new Dictionary<string, string>
        {
            ["sessions"] = SandboxBots.ToString(CultureInfo.InvariantCulture),
            ["uptime"] = "PT4H30M"
        });
    }
}
