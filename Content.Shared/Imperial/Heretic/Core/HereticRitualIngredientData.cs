using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

[Serializable, NetSerializable]
public sealed class HereticRitualIngredientData
{
    public string EntityId = string.Empty;
    public string? Tag;
    public bool HasMobState;
    public bool IsBurning;
    public bool IsDeadOrCritical;
    public bool RequireHumanoid;
    public int Amount = 1;
}
