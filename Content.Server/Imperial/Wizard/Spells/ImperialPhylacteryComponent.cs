using System.Linq;
using System.Numerics;
using Content.Server.Emp;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Throwing;
using Content.Shared.Temperature.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/datum/component/phylactery: пока предмет цел, лич возрождается.</summary>
[RegisterComponent]
public sealed partial class ImperialPhylacteryComponent : Component
{
    [ViewVariables]
    public EntityUid Mind;

    /// <summary>base_respawn_time.</summary>
    [DataField]
    public TimeSpan RespawnTime = TimeSpan.FromMinutes(3);

    /// <summary>stun_per_resurrection.</summary>
    [DataField]
    public TimeSpan StunPerResurrection = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public int Resurrections;

    [ViewVariables]
    public TimeSpan? ReviveAt;

    [ViewVariables]
    public EntityUid? Corpse;
}
