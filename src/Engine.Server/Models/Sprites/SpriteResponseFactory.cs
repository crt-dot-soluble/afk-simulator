using System.Linq;
using Engine.Core.Rendering.Sprites;

namespace Engine.Server.Models.Sprites;

internal static class SpriteResponseFactory
{
    public static SpriteDefinitionResponse Create(SpriteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new SpriteDefinitionResponse
        {
            SpriteId = definition.SpriteId,
            AssetPath = definition.AssetPath,
            FrameWidth = definition.Layout.FrameWidth,
            FrameHeight = definition.Layout.FrameHeight,
            DefaultAnimation = definition.DefaultAnimation,
            Animations = definition.Animations.Values
                .Select(animation => new SpriteAnimationResponse
                {
                    Name = animation.Name,
                    FrameDurationMs = animation.FrameDuration.TotalMilliseconds,
                    Loop = animation.Loop,
                    Frames = animation.Frames
                        .Select(index => definition.Layout.GetRegion(index))
                        .Select(region => new SpriteAnimationFrameResponse
                        {
                            Index = region.Index,
                            X = region.X,
                            Y = region.Y,
                            Width = region.Width,
                            Height = region.Height
                        })
                        .ToArray()
                })
                .ToArray()
        };
    }
}
