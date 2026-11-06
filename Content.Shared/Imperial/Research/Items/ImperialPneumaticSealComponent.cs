using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Пневматический затвор шлюза (door_seal): дверь нельзя открыть, пока он стоит.</summary>
[RegisterComponent]
public sealed partial class ImperialPneumaticSealComponent : Component
{
    [DataField]
    public TimeSpan SealTime = TimeSpan.FromSeconds(2f);

    [DataField]
    public TimeSpan UnsealTime = TimeSpan.FromSeconds(4f);
}
