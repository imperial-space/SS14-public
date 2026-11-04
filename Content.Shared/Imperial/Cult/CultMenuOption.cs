using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public sealed class CultMenuOption(string id, string label, SpriteSpecifier? icon = null, string? tooltip = null, Color? color = null)
{
    public readonly string Id = id;
    public readonly string Label = label;
    public readonly SpriteSpecifier? Icon = icon;
    public readonly string? Tooltip = tooltip;
    public readonly Color? Color = color;
}
