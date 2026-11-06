using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Сторона войны за Кадию.</summary>
[Serializable, NetSerializable]
public enum CadiaFaction : byte
{
    Imperium,
    Chaos,
}
