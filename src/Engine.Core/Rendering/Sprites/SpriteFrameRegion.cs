using System;

namespace Engine.Core.Rendering.Sprites;

public readonly struct SpriteFrameRegion : IEquatable<SpriteFrameRegion>
{
    public SpriteFrameRegion(int index, int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0);

        Index = index;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int Index { get; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public bool Equals(SpriteFrameRegion other)
    {
        return Index == other.Index && X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    }

    public override bool Equals(object? obj) => obj is SpriteFrameRegion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Index, X, Y, Width, Height);

    public static bool operator ==(SpriteFrameRegion left, SpriteFrameRegion right) => left.Equals(right);

    public static bool operator !=(SpriteFrameRegion left, SpriteFrameRegion right) => !left.Equals(right);
}
