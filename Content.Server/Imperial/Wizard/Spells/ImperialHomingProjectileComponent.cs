using System.Numerics;
using Content.Server.Imperial.Antimagic;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>Самонаводящийся снаряд (homing у карт заклинаний и волшебной ракеты).</summary>
[RegisterComponent]
public sealed partial class ImperialHomingProjectileComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    /// <summary>Градусов в секунду.</summary>
    [DataField]
    public float TurnRate = 200f;
}
