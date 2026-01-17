using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Engine.Core.Contracts;

/// <summary>
/// Allows engine modules to describe self-contained dashboard views that can be rendered by generic shells.
/// </summary>
public interface IModuleViewProvider
{
    IReadOnlyCollection<ModuleViewDocument> DescribeModuleViews(ModuleViewContext context);
}

/// <summary>
/// Provides request-scoped details so modules can personalize their dashboard documents.
/// </summary>
/// <param name="UserId">Optional authenticated user identifier.</param>
/// <param name="Parameters">Arbitrary key/value hints supplied by the client.</param>
public sealed record ModuleViewContext(
    string? UserId,
    IReadOnlyDictionary<string, string>? Parameters = null)
{
    public static ModuleViewContext Empty { get; } = new(null, null);
}

/// <summary>
/// Represents a full dashboard panel along with its renderable component tree.
/// </summary>
/// <param name="Descriptor">Static layout metadata shared with Mission Control.</param>
/// <param name="Blocks">Renderable component tree.</param>
/// <param name="DataSource">Optional refresh hints for the client.</param>
public sealed record ModuleViewDocument(
    DashboardViewDescriptor Descriptor,
    IReadOnlyList<ModuleViewBlock> Blocks,
    ModuleViewDataSource? DataSource = null);

/// <summary>
/// Advises the client how frequently a module view should be refreshed.
/// </summary>
/// <param name="RefreshInterval">ISO-8601 duration (e.g., PT1S) describing the desired polling cadence.</param>
/// <param name="Parameters">Optional key/value pairs the client should send back during refresh.</param>
public sealed record ModuleViewDataSource(
    string RefreshInterval,
    IReadOnlyDictionary<string, string>? Parameters = null);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ModuleViewStackBlock), typeDiscriminator: ModuleViewBlockKinds.Stack)]
[JsonDerivedType(typeof(ModuleViewSectionBlock), typeDiscriminator: ModuleViewBlockKinds.Section)]
[JsonDerivedType(typeof(ModuleViewGridBlock), typeDiscriminator: ModuleViewBlockKinds.Grid)]
[JsonDerivedType(typeof(ModuleViewMetricBlock), typeDiscriminator: ModuleViewBlockKinds.Metric)]
[JsonDerivedType(typeof(ModuleViewListBlock), typeDiscriminator: ModuleViewBlockKinds.List)]
[JsonDerivedType(typeof(ModuleViewActionBarBlock), typeDiscriminator: ModuleViewBlockKinds.ActionBar)]
[JsonDerivedType(typeof(ModuleViewFormBlock), typeDiscriminator: ModuleViewBlockKinds.Form)]
[JsonDerivedType(typeof(ModuleViewSpriteBlock), typeDiscriminator: ModuleViewBlockKinds.Sprite)]
[JsonDerivedType(typeof(ModuleViewEquipmentBlock), typeDiscriminator: ModuleViewBlockKinds.Equipment)]
[JsonDerivedType(typeof(ModuleViewTimelineBlock), typeDiscriminator: ModuleViewBlockKinds.Timeline)]
public abstract record ModuleViewBlock(string BlockKind);

public static class ModuleViewBlockKinds
{
    public const string Stack = "stack";
    public const string Section = "section";
    public const string Grid = "grid";
    public const string Metric = "metric";
    public const string List = "list";
    public const string ActionBar = "action-bar";
    public const string Form = "form";
    public const string Sprite = "sprite";
    public const string Equipment = "equipment";
    public const string Timeline = "timeline";
}

public enum ModuleViewOrientation
{
    Vertical,
    Horizontal
}

public sealed record ModuleViewStackBlock(
    string Id,
    ModuleViewOrientation Orientation,
    IReadOnlyList<ModuleViewBlock> Children,
    ModuleViewStyle? Style = null,
    string? Title = null,
    string? Description = null) : ModuleViewBlock(ModuleViewBlockKinds.Stack);

public sealed record ModuleViewSectionBlock(
    string Id,
    string Title,
    string? Description,
    IReadOnlyList<ModuleViewBlock> Children,
    ModuleViewStyle? Style = null) : ModuleViewBlock(ModuleViewBlockKinds.Section);

public sealed record ModuleViewGridBlock(
    string Id,
    int Columns,
    IReadOnlyList<ModuleViewGridCell> Cells,
    ModuleViewStyle? Style = null) : ModuleViewBlock(ModuleViewBlockKinds.Grid);

public sealed record ModuleViewGridCell(
    int ColumnSpan,
    IReadOnlyList<ModuleViewBlock> Children);

public sealed record ModuleViewMetricBlock(
    string Id,
    string Label,
    string Value,
    string? Secondary = null,
    string? Accent = null,
    string? Icon = null,
    string? Trend = null,
    string? TrendLabel = null,
    IReadOnlyDictionary<string, string>? Tags = null) : ModuleViewBlock(ModuleViewBlockKinds.Metric);

public sealed record ModuleViewListBlock(
    string Id,
    string Title,
    IReadOnlyList<ModuleViewListItem> Items,
    bool ShowOrder = false,
    bool AllowSelection = false) : ModuleViewBlock(ModuleViewBlockKinds.List);

public sealed record ModuleViewListItem(
    string Id,
    string Label,
    string? Description = null,
    string? Value = null,
    string? Accent = null,
    string? Icon = null,
    bool IsActive = false,
    IReadOnlyDictionary<string, string>? Badges = null);

public sealed record ModuleViewActionBarBlock(
    string Id,
    IReadOnlyList<ModuleViewActionDescriptor> Actions) : ModuleViewBlock(ModuleViewBlockKinds.ActionBar);

public sealed record ModuleViewActionDescriptor(
    string Id,
    string Label,
    string Command,
    string? Icon = null,
    string? Variant = null,
    bool IsPrimary = false);

public sealed record ModuleViewFormBlock(
    string Id,
    string Title,
    IReadOnlyList<ModuleViewFormField> Fields,
    IReadOnlyList<ModuleViewActionDescriptor> Actions,
    string? Description = null,
    ModuleViewStyle? Style = null) : ModuleViewBlock(ModuleViewBlockKinds.Form);

public sealed record ModuleViewFormField(
    string Id,
    string Label,
    string Type,
    string? Placeholder = null,
    string? Value = null,
    bool Required = false,
    string? Description = null,
    int? MaxLength = null,
    double? Min = null,
    double? Max = null,
    IReadOnlyList<ModuleViewFormOption>? Options = null);

public sealed record ModuleViewFormOption(
    string Value,
    string Label,
    string? Icon = null);

public sealed record ModuleViewSpriteBlock(
    string Id,
    string SpriteId,
    string Animation,
    string? Accent = null,
    int Width = 256,
    int Height = 256) : ModuleViewBlock(ModuleViewBlockKinds.Sprite);

public sealed record ModuleViewEquipmentBlock(
    string Id,
    IReadOnlyList<ModuleViewEquipmentSlot> Slots,
    string? Title = null) : ModuleViewBlock(ModuleViewBlockKinds.Equipment);

public sealed record ModuleViewEquipmentSlot(
    string Slot,
    string? ItemName,
    string? Icon = null,
    string? Rarity = null,
    string? Description = null);

public sealed record ModuleViewTimelineBlock(
    string Id,
    IReadOnlyList<ModuleViewTimelineEvent> Events,
    string? Title = null) : ModuleViewBlock(ModuleViewBlockKinds.Timeline);

public sealed record ModuleViewTimelineEvent(
    string Label,
    string Value,
    DateTimeOffset Timestamp,
    string? Accent = null,
    string? Icon = null);

public sealed record ModuleViewStyle(
    string? Accent = null,
    string? Background = null,
    string? Icon = null,
    string? Layout = null,
    IReadOnlyDictionary<string, string>? Tokens = null);
