using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Капсула в пути: кого везёт, подбита ли и когда садится, взлетает и исчезает (только сервер).</summary>
[RegisterComponent]
public sealed partial class CadiaDropPodFlightComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Passengers = new();

    [ViewVariables]
    public TimeSpan Impact;

    [ViewVariables]
    public TimeSpan LiftOff;

    [ViewVariables]
    public TimeSpan Gone;

    [ViewVariables]
    public bool Landed;

    /// <summary>ПВО попало: посадка жёсткая и мимо зоны, отделение ранено.</summary>
    [ViewVariables]
    public bool Hit;

    /// <summary>Разрывы зениток вокруг капсулы на спуске.</summary>
    [ViewVariables]
    public List<TimeSpan> Flak = new();

    [DataField]
    public TimeSpan LandDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan LandDuration2 = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan LandDuration3 = TimeSpan.FromSeconds(2.5);
}
