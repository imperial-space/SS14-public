using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>Плата дисковода: всасывание и автоотправку можно выключить (attack_self / alt-click).</summary>
[RegisterComponent]
public sealed partial class ImperialDishDriveBoardComponent : Component
{
    [DataField]
    public bool Suction = true;

    [DataField]
    public bool Transmit = true;
}
