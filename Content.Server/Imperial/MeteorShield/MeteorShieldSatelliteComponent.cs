using System.Linq;
using System.Numerics;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Mining;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.MeteorShield;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.MeteorShield;

/// <summary>Спутник метеоритного щита (obj/machinery/satellite/meteor_shield).</summary>
[RegisterComponent]
public sealed partial class MeteorShieldSatelliteComponent : Component
{
    [DataField]
    public string Mode = "M-SHIELD";

    /// <summary>kill_range: радиус, в котором спутник сбивает метеоры.</summary>
    [DataField]
    public int KillRange = 14;

    [DataField]
    public EntProtoId Beam = "ImperialMeteorShieldBeam";

    [DataField]
    public EntProtoId Signal = "ImperialMeteorShieldSignal";

    /// <summary>Звук луча. В SS13 луч щита беззвучен — добавлен лазерный выстрел.</summary>
    [DataField]
    public SoundSpecifier BeamSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/laser.ogg");

    [ViewVariables] public int Id;
    [ViewVariables] public bool Active;
    [ViewVariables] public TimeSpan NextCheck;
    [ViewVariables] public EntityUid? SignalEntity;

    [DataField]
    public TimeSpan CheckDelay = TimeSpan.FromSeconds(0.2);

    [DataField]
    public TimeSpan SatelliteActivateDelay = TimeSpan.FromSeconds(2);
}
