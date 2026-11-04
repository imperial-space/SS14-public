using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>projectile_dampener: поле, гасящее чужие снаряды вокруг носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialModDampenerComponent : Component
{
    [DataField]
    public float Radius = 2f;

    [DataField]
    public float DamageMultiplier = 0.75f;

    [DataField]
    public float SpeedMultiplier = 0.4f;
}
