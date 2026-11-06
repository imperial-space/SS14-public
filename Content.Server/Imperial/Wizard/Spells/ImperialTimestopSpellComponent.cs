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

/// <summary>timestop: поле остановки времени (радиус 2, 10 секунд).</summary>
[RegisterComponent]
public sealed partial class ImperialTimestopSpellComponent : Component
{
    [DataField]
    public EntProtoId Field = "ImperialTimestopField";

    [DataField]
    public float Range = 2f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);
}
