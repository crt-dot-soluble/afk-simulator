using System;
using System.Linq;
using Engine.Client.Services;

namespace Engine.Client.Rendering;

internal static class SpriteAnimationMapper
{
    public static SpriteAnimationDescriptor? CreateDescriptor(SpriteDefinitionDto? definition, string animationName,
        string accentColor)
    {
        if (definition is null)
        {
            return null;
        }

        var animation = definition.Animations.FirstOrDefault(a =>
                            string.Equals(a.Name, animationName, StringComparison.OrdinalIgnoreCase))
                        ?? definition.Animations.FirstOrDefault(a =>
                            string.Equals(a.Name, definition.DefaultAnimation, StringComparison.OrdinalIgnoreCase));
        if (animation is null)
        {
            return null;
        }

        return new SpriteAnimationDescriptor
        {
            SpriteId = definition.SpriteId,
            Animation = animation.Name,
            ImageUrl = NormalizeAssetPath(definition.AssetPath),
            FrameDurationMs = animation.FrameDurationMs,
            Loop = animation.Loop,
            AccentColor = accentColor,
            Frames = animation.Frames
                .Select(frame => new SpriteAnimationFrameDescriptor
                {
                    Index = frame.Index,
                    X = frame.X,
                    Y = frame.Y,
                    Width = frame.Width,
                    Height = frame.Height
                })
                .ToArray()
        };
    }

    private static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path
            : path.StartsWith('/')
                ? path
                : $"/{path}";
    }
}
