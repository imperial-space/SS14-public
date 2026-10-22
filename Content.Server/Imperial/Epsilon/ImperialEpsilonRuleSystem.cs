using System.Numerics;
using Content.Server.AlertLevel;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Uplink.AssaultPod;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Epsilon;
using Content.Shared.Light.Components;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Content.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Epsilon;

/// <summary>
/// Протокол «Эпсилон»: под эскадрона смерти ждёт у ЦК, экипаж пода выбирает зону высадки,
/// а сценарий сам запускает оповещение, гасит станцию и сажает под ровно на 1:28 трека.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialEpsilonRuleComponent : Component
{
    /// <summary>Через сколько после появления пода оповещение начнётся само (команда «epsilon start» — сразу).</summary>
    [DataField]
    public TimeSpan PrepTime = TimeSpan.FromMinutes(5);

    [DataField]
    public int Operatives = 8;

    [DataField]
    public EntProtoId Operative = "ImperialEpsilonOperative";

    /// <summary>Во сколько раз тусклее светят лампы после смены кода.</summary>
    [DataField]
    public float LightDim = 0.35f;

    [DataField]
    public string AlertLevel = "epsilon";

    [ViewVariables]
    public TimeSpan AutoStartAt;

    [ViewVariables]
    public TimeSpan? SequenceStart;

    [ViewVariables]
    public EntityUid? Pod;

    [ViewVariables]
    public EntityUid? Console;

    [ViewVariables]
    public EpsilonStage Stage = EpsilonStage.Waiting;

    [ViewVariables]
    public List<EntityUid> Lights = new();

    [ViewVariables]
    public int LightsOff;

    [ViewVariables]
    public TimeSpan NextEmergencyRefresh;
}

public enum EpsilonStage : byte
{
    Waiting,
    Announcing,
    LightsOut,
    Emergency,
    Launched,
    Done,
}

public sealed class ImperialEpsilonRuleSystem : GameRuleSystem<ImperialEpsilonRuleComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertLevelSystem _alert = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AssaultPodShipSystem _pods = default!;
    [Dependency] private readonly AmbientSoundSystem _ambient = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly PoweredLightSystem _lights = default!;
    [Dependency] private readonly LightBulbSystem _bulbs = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly TimeSpan EmergencyRefresh = TimeSpan.FromSeconds(2);

    protected override void Started(EntityUid uid, ImperialEpsilonRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.AutoStartAt = _timing.CurTime + component.PrepTime;

        var pod = _pods.SpawnShip(AssaultPodShipStyle.Epsilon, GetPodSpot());
        component.Pod = pod;

        var seats = new List<EntityCoordinates>();
        var children = Transform(pod).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<AssaultPodConsoleComponent>(child))
                component.Console = child;
            else if (HasComp<StrapComponent>(child))
                seats.Add(Transform(child).Coordinates);
        }

        RobustRandom.Shuffle(seats);
        var fallback = component.Console is { } console ? Transform(console).Coordinates : new EntityCoordinates(pod, 3.5f, 3.5f);
        for (var i = 0; i < component.Operatives; i++)
        {
            Spawn(component.Operative, i < seats.Count ? seats[i] : fallback);
        }

        Log.Info($"Протокол «Эпсилон»: под {ToPrettyString(pod)} готов, оповещение через {component.PrepTime}.");
    }

    /// <summary>Под ждёт рядом с ЦК; если ЦК нет — на собственной карте.</summary>
    private MapCoordinates GetPodSpot()
    {
        var query = EntityQueryEnumerator<StationCentcommComponent>();
        while (query.MoveNext(out var centcomm))
        {
            if (centcomm.Entity is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid))
                continue;

            var mapPos = _transform.GetMapCoordinates(grid);
            var aabb = mapGrid.LocalAABB;
            return new MapCoordinates(mapPos.Position + new Vector2(aabb.Right + 20f, aabb.Center.Y), mapPos.MapId);
        }

        _map.CreateMap(out var mapId);
        return new MapCoordinates(Vector2.Zero, mapId);
    }

    /// <summary>«epsilon start»: начать оповещение немедленно.</summary>
    public int StartNow()
    {
        var started = 0;
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var rule, out _))
        {
            if (rule.SequenceStart != null)
                continue;

            StartSequence(rule);
            started++;
        }

        return started;
    }

    /// <summary>«epsilon stop»: оборвать трек и надписи у клиентов (свет и код не трогаем).</summary>
    public void StopMusic()
    {
        RaiseNetworkEvent(new EpsilonSequenceStopEvent(), Filter.Broadcast());
    }

    private void StartSequence(ImperialEpsilonRuleComponent rule)
    {
        var now = _timing.CurTime;
        rule.SequenceStart = now;
        rule.Stage = EpsilonStage.Announcing;
        RaiseNetworkEvent(new EpsilonSequenceStartEvent(now), Filter.Broadcast());

        // Свет гаснет вразнобой по всей станции, будто ЦК рубит рубильники отсек за отсеком.
        rule.Lights.Clear();
        rule.LightsOff = 0;
        var lights = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lights.MoveNext(out var uid, out _, out var xform))
        {
            if (IsOnStation(xform))
                rule.Lights.Add(uid);
        }

        RobustRandom.Shuffle(rule.Lights);
        Log.Info($"Протокол «Эпсилон»: оповещение началось, ламп на станции: {rule.Lights.Count}.");
    }

    protected override void ActiveTick(EntityUid uid, ImperialEpsilonRuleComponent rule, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, rule, gameRule, frameTime);

        var now = _timing.CurTime;
        if (rule.SequenceStart is not { } start)
        {
            if (now >= rule.AutoStartAt)
                StartSequence(rule);

            return;
        }

        var elapsed = (float) (now - start).TotalSeconds;
        switch (rule.Stage)
        {
            case EpsilonStage.Announcing when elapsed >= EpsilonTimeline.LightsOutStart:
                rule.Stage = EpsilonStage.LightsOut;
                break;

            case EpsilonStage.LightsOut:
            {
                var fraction = Math.Clamp((elapsed - EpsilonTimeline.LightsOutStart) / (EpsilonTimeline.LightsOutEnd - EpsilonTimeline.LightsOutStart), 0f, 1f);
                var target = (int) MathF.Ceiling(rule.Lights.Count * fraction);
                for (; rule.LightsOff < target; rule.LightsOff++)
                {
                    var light = rule.Lights[rule.LightsOff];
                    if (!TerminatingOrDeleted(light))
                        _lights.SetState(light, false);
                }

                if (elapsed >= EpsilonTimeline.EmergencyLights)
                {
                    rule.Stage = EpsilonStage.Emergency;
                    rule.NextEmergencyRefresh = now;
                }

                break;
            }

            case EpsilonStage.Emergency:
            {
                if (now >= rule.NextEmergencyRefresh)
                {
                    rule.NextEmergencyRefresh = now + EmergencyRefresh;
                    ForceEmergencyLights();
                }

                if (rule.Console is { } console
                    && TryComp<AssaultPodConsoleComponent>(console, out var pod)
                    && elapsed >= EpsilonTimeline.Landing - (pod.Countdown + pod.FlightTime).TotalSeconds)
                {
                    rule.Stage = EpsilonStage.Launched;
                    if (pod.State == Shared.Imperial.Uplink.AssaultPod.AssaultPodState.Ready)
                    {
                        // Посадка точно на 1:28 трека.
                        _pods.ForceLaunch((console, pod));
                        pod.NextStage = start + TimeSpan.FromSeconds(EpsilonTimeline.Landing) - pod.FlightTime;
                    }
                }
                else if (rule.Console == null && elapsed >= EpsilonTimeline.Landing)
                {
                    rule.Stage = EpsilonStage.Launched;
                }

                break;
            }

            case EpsilonStage.Launched:
            {
                if (now >= rule.NextEmergencyRefresh)
                {
                    rule.NextEmergencyRefresh = now + EmergencyRefresh;
                    ForceEmergencyLights();
                }

                if (elapsed < EpsilonTimeline.Landing)
                    break;

                rule.Stage = EpsilonStage.Done;
                DeclareEpsilon(rule);
                break;
            }

            case EpsilonStage.Done:
            {
                // Пока на станции «Эпсилон», аварийки держим включёнными: ванильный UpdateState при скачке питания
                // переводит их на аккумулятор, и они гаснут, когда тот сядет.
                if (now < rule.NextEmergencyRefresh)
                    break;

                rule.NextEmergencyRefresh = now + EmergencyRefresh;
                ForceEmergencyLights(rule.AlertLevel);
                ReleaseSteadyLights(rule.AlertLevel);
                break;
            }
        }
    }

    /// <summary>
    /// С 13.5 с все запитанные аварийные лампы станции горят фиолетовым кода «Эпсилон».
    /// Включаем только свет и спрайт: состояние лампы не трогаем, иначе она жжёт аккумулятор и гаснет.
    /// Без питания лампой управляет ванильная система (красный свет от аккумулятора).
    /// </summary>
    private void ForceEmergencyLights(string? requiredLevel = null)
    {
        var query = EntityQueryEnumerator<EmergencyLightComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var receiver, out var xform))
        {
            if (!receiver.Powered || CompOrNull<StationMemberComponent>(xform.GridUid)?.Station is not { } station)
                continue;

            if (requiredLevel != null && CompOrNull<AlertLevelComponent>(station)?.CurrentLevel != requiredLevel)
                continue;

            _pointLight.SetEnabled(uid, true);
            _pointLight.SetColor(uid, EpsilonTimeline.EpsilonViolet);
            _appearance.SetData(uid, EmergencyLightVisuals.Color, EpsilonTimeline.EpsilonViolet);
            _appearance.SetData(uid, EmergencyLightVisuals.On, true);
            _ambient.SetAmbience(uid, true);
            EnsureComp<EpsilonSteadyLightComponent>(uid);
        }
    }

    /// <summary>Код сменили с «Эпсилона» — аварийкам возвращается обычная мигалка.</summary>
    private void ReleaseSteadyLights(string level)
    {
        var query = EntityQueryEnumerator<EpsilonSteadyLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var station = CompOrNull<StationMemberComponent>(xform.GridUid)?.Station;
            if (station == null || CompOrNull<AlertLevelComponent>(station)?.CurrentLevel != level)
                RemCompDeferred<EpsilonSteadyLightComponent>(uid);
        }
    }

    /// <summary>1:28: под на станции, код «Эпсилон», все лампы снова горят — тускло и фиолетово.</summary>
    private void DeclareEpsilon(ImperialEpsilonRuleComponent rule)
    {
        foreach (var station in _station.GetStations())
        {
            _alert.SetLevel(station, rule.AlertLevel, playSound: false, announce: true, force: true, locked: true);
        }

        var lights = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lights.MoveNext(out var uid, out var light, out var xform))
        {
            if (!IsOnStation(xform))
                continue;

            if (_lights.GetBulb(uid, light) is { } bulb && TryComp<LightBulbComponent>(bulb, out var bulbComp))
            {
                bulbComp.LightEnergy *= rule.LightDim;
                _bulbs.SetColor(bulb, EpsilonTimeline.EpsilonViolet, bulbComp);
                Dirty(bulb, bulbComp);
            }

            _lights.SetState(uid, true, light);
        }

        Log.Info("Протокол «Эпсилон»: код сменён, станция под контролем ЦК.");
    }

    private bool IsOnStation(TransformComponent xform)
    {
        return CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != null;
    }
}
