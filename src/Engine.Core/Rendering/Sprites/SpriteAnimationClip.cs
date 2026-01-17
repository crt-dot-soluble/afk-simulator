using System;
using System.Collections.Immutable;

namespace Engine.Core.Rendering.Sprites;

public sealed class SpriteAnimationClip
{
    public SpriteAnimationClip(string name, IEnumerable<int> frames, TimeSpan frameDuration, bool loop = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameDuration, TimeSpan.Zero);

        Name = name;
        Frames = frames.ToImmutableArray();
        if (Frames.Count == 0)
        {
            throw new ArgumentException("Animation requires at least one frame.", nameof(frames));
        }

        FrameDuration = frameDuration;
        Loop = loop;
    }

    public string Name { get; }
    public IReadOnlyList<int> Frames { get; }
    public TimeSpan FrameDuration { get; }
    public bool Loop { get; }
}
