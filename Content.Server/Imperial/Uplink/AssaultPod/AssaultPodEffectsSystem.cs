using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Imperial.Uplink.AssaultPod;

namespace Content.Server.Imperial.Uplink.AssaultPod;

/// <summary>Состояние звуков и меток полёта пода (капсулы или консоли большого пода).</summary>
[RegisterComponent]
public sealed partial class AssaultPodFlightComponent : Component
{
    [ViewVariables]
    public bool StartupPlayed;

    [ViewVariables]
    public bool ArrivalStarted;

    /// <summary>Зацикленный гул гиперпространства.</summary>
    [ViewVariables]
    public EntityUid? TravelStream;

    [ViewVariables]
    public readonly List<EntityUid> Markers = new();
}

/// <summary>
/// Звуки и метки полёта штурмовых подов в духе FTL шаттлов SS14:
/// разгон гипердвигателя за 5.5 с до старта, гул всю дорогу, выход из гиперпространства и метка посадки за 5 с до касания.
/// </summary>
public sealed class AssaultPodEffectsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    /// <summary>shuttle.startup_time и shuttle.arrival_time.</summary>
    public static readonly TimeSpan StartupTime = TimeSpan.FromSeconds(5.5);
    public static readonly TimeSpan ArrivalTime = TimeSpan.FromSeconds(5);

    private static readonly SoundSpecifier StartupSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg", AudioParams.Default.WithVolume(-5f));
    private static readonly SoundSpecifier TravelSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_progress.ogg", AudioParams.Default.WithVolume(-3f).WithLoop(true));
    private static readonly SoundSpecifier ArrivalSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg", AudioParams.Default.WithVolume(-5f));
    private static readonly SoundSpecifier IncomingSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg", AudioParams.Default.WithVolume(2f).WithMaxDistance(20f));
    private static readonly SoundSpecifier ImpactSound = new SoundCollectionSpecifier("ShuttleImpactSound", AudioParams.Default.WithVolume(5f).WithMaxDistance(30f));

    private static readonly EntProtoId Marker = "ImperialAssaultPodLandingMarker";

    /// <summary>Отсчёт: разгон гипердвигателя за StartupTime до старта.</summary>
    public void UpdateCountdown(EntityUid emitter, TimeSpan launchAt, bool grid)
    {
        var flight = EnsureComp<AssaultPodFlightComponent>(emitter);
        if (flight.StartupPlayed || _timing.CurTime < launchAt - StartupTime)
            return;

        flight.StartupPlayed = true;
        Play(StartupSound, emitter, grid);
    }

    /// <summary>Старт: гул гиперпространства до самой посадки.</summary>
    public void StartTravel(EntityUid emitter, bool grid)
    {
        var flight = EnsureComp<AssaultPodFlightComponent>(emitter);
        StopTravel(flight);
        flight.TravelStream = Play(TravelSound, emitter, grid);
    }

    /// <summary>
    /// За ArrivalTime до касания: выход из гиперпространства для десанта, метки на месте посадки и гул прилёта для станции.
    /// Возвращает true, если пора готовить место посадки (вызывается один раз).
    /// </summary>
    public bool ShouldBeginArrival(EntityUid emitter, TimeSpan landAt)
    {
        var flight = EnsureComp<AssaultPodFlightComponent>(emitter);
        return !flight.ArrivalStarted && _timing.CurTime >= landAt - ArrivalTime;
    }

    public void BeginArrival(EntityUid emitter, bool grid, IReadOnlyList<EntityCoordinates> spots, TimeSpan landAt)
    {
        var flight = EnsureComp<AssaultPodFlightComponent>(emitter);
        flight.ArrivalStarted = true;
        Play(ArrivalSound, emitter, grid);

        var now = _timing.CurTime;
        for (var i = 0; i < spots.Count; i++)
        {
            var marker = Spawn(Marker, spots[i]);
            var comp = EnsureComp<AssaultPodLandingMarkerComponent>(marker);
            comp.Start = now;
            comp.Arrival = landAt;
            Dirty(marker, comp);
            flight.Markers.Add(marker);
        }

        if (spots.Count > 0)
            _audio.PlayPvs(IncomingSound, spots[0]);
    }

    /// <summary>Посадка сорвалась: метки гаснут, следующая попытка покажет их заново.</summary>
    public void ResetArrival(EntityUid emitter)
    {
        if (!TryComp<AssaultPodFlightComponent>(emitter, out var flight))
            return;

        foreach (var marker in flight.Markers)
        {
            QueueDel(marker);
        }

        flight.Markers.Clear();
        flight.ArrivalStarted = false;
    }

    /// <summary>Касание: гул стихает, метки гаснут, удар о станцию.</summary>
    public void Landed(EntityUid emitter, EntityCoordinates coords)
    {
        if (TryComp<AssaultPodFlightComponent>(emitter, out var flight))
        {
            StopTravel(flight);
            foreach (var marker in flight.Markers)
            {
                QueueDel(marker);
            }

            flight.Markers.Clear();
            RemComp<AssaultPodFlightComponent>(emitter);
        }

        _audio.PlayPvs(ImpactSound, coords);
    }

    private void StopTravel(AssaultPodFlightComponent flight)
    {
        if (flight.TravelStream is { } stream)
            _audio.Stop(stream);
        flight.TravelStream = null;
    }

    /// <summary>У большого пода звук слышен на всём гриде, у капсулы — рядом с ней (десант внутри).</summary>
    private EntityUid? Play(SoundSpecifier sound, EntityUid emitter, bool grid)
    {
        var target = grid && Transform(emitter).GridUid is { } gridUid ? gridUid : emitter;
        var audio = _audio.PlayPvs(sound, target);
        if (grid && audio != null)
            _audio.SetGridAudio(audio);
        return audio?.Entity;
    }
}
