using System;
using Engine.Core.Assets;

namespace Engine.Core.Rendering.Sprites;

public sealed class SpriteSheetLayout
{
    public SpriteSheetLayout(int frameWidth, int frameHeight, int columns, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameWidth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameHeight, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rows, 0);

        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        Columns = columns;
        Rows = rows;
        TotalFrames = columns * rows;
    }

    public int FrameWidth { get; }
    public int FrameHeight { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int TotalFrames { get; }

    public SpriteFrameRegion GetRegion(int frameIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, TotalFrames);

        var column = frameIndex % Columns;
        var row = frameIndex / Columns;
        var x = column * FrameWidth;
        var y = row * FrameHeight;
        return new SpriteFrameRegion(frameIndex, x, y, FrameWidth, FrameHeight);
    }

    public static SpriteSheetLayout FromAsset(AssetRecord asset, int frameWidth, int frameHeight)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameWidth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameHeight, 0);

        var columns = Math.Max(1, asset.Width / frameWidth);
        var rows = Math.Max(1, asset.Height / frameHeight);
        return new SpriteSheetLayout(frameWidth, frameHeight, columns, rows);
    }

    public static SpriteSheetLayout SingleFrame(int width, int height)
    {
        return new SpriteSheetLayout(width, height, 1, 1);
    }
}
