using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

[Serializable, NetSerializable]
public sealed class XenoRadialOption(string id, string name, SpriteSpecifier icon)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly SpriteSpecifier Icon = icon;
}
