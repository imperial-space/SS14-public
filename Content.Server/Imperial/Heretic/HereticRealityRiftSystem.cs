using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.Bed.Sleep;
using Content.Shared.DoAfter;
using Content.Shared.Eye;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticRealityRiftSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem         _doAfter = default!;
    [Dependency] private readonly HereticSystem         _heretic = default!;
    [Dependency] private readonly IGameTiming           _timing  = default!;
    [Dependency] private readonly IRobustRandom         _random  = default!;
    [Dependency] private readonly PopupSystem           _popup   = default!;
    [Dependency] private readonly SharedAudioSystem     _audio   = default!;
    [Dependency] private readonly SharedEyeSystem       _eye     = default!;
    [Dependency] private readonly SharedTransformSystem _xform   = default!;
    [Dependency] private readonly EntityLookupSystem    _lookup  = default!;

    private static readonly TimeSpan TriggerCooldown = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ZoomDuration    = TimeSpan.FromSeconds(10);
    private const float RiftRange     = 10f;
    private const float UpdateInterval = 2f;

    private static readonly string[] DreamMessages =
    {
        "heretic-dream-rift-1",
        "heretic-dream-rift-2",
        "heretic-dream-rift-3",
        "heretic-dream-rift-4",
        "heretic-dream-rift-5",
        "heretic-dream-rift-6",
    };

    private static readonly string[] LocationDreamMessages =
    {
        "heretic-dream-rift-loc-1",
        "heretic-dream-rift-loc-2",
        "heretic-dream-rift-loc-3",
        "heretic-dream-rift-loc-4",
    };

    // (rift uid, heretic uid) → last trigger time
    private readonly Dictionary<(EntityUid, EntityUid), TimeSpan> _lastTrigger = new();
    // heretic uid → count of active FOV effects (prevents double-toggle when 2 rifts in range)
    private readonly Dictionary<EntityUid, int>  _fovEffectCount = new();
    private readonly Dictionary<EntityUid, bool> _fovOriginalState = new();
    private float _updateTimer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, ComponentStartup>(OnHereticStartup);
        SubscribeLocalEvent<HereticComponent, ComponentShutdown>(OnHereticShutdown);
        SubscribeLocalEvent<HereticComponent, GetVisMaskEvent>(OnHereticVisMask);
        SubscribeLocalEvent<HereticComponent, SleepStateChangedEvent>(OnHereticSleep);

        SubscribeLocalEvent<HereticRealityRiftComponent, ComponentRemove>(OnRiftRemoved);
        SubscribeLocalEvent<HereticRealityRiftComponent, InteractHandEvent>(OnRiftInteract);
        SubscribeLocalEvent<HereticComponent, AbsorbRealityRiftDoAfterEvent>(OnAbsorbDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;
        _updateTimer = 0f;

        var now = _timing.CurTime;
        var riftQuery = EntityQueryEnumerator<HereticRealityRiftComponent, TransformComponent>();
        while (riftQuery.MoveNext(out var riftUid, out _, out var riftXform))
        {
            var riftPos = _xform.GetMapCoordinates(riftUid, riftXform);

            foreach (var hEnt in _lookup.GetEntitiesInRange<HereticComponent>(riftPos, RiftRange))
            {
                var key = (riftUid, hEnt.Owner);
                if (_lastTrigger.TryGetValue(key, out var last) && now - last < TriggerCooldown)
                    continue;

                _lastTrigger[key] = now;
                TriggerRiftEffect(hEnt.Owner);
            }
        }
    }

    private void TriggerRiftEffect(EntityUid hUid)
    {
        if (!TryComp<EyeComponent>(hUid, out var eye))
            return;

        _fovEffectCount.TryGetValue(hUid, out var count);
        if (count == 0)
        {
            _fovOriginalState[hUid] = eye.DrawFov;
            _eye.SetDrawFov(hUid, !eye.DrawFov, eye);
        }
        _fovEffectCount[hUid] = count + 1;

        Timer.Spawn(ZoomDuration, () =>
        {
            if (!_fovEffectCount.TryGetValue(hUid, out var c))
                return;
            c--;
            if (c <= 0)
            {
                _fovEffectCount.Remove(hUid);
                if (_fovOriginalState.Remove(hUid, out var origFov) && TryComp<EyeComponent>(hUid, out var eyeNow))
                    _eye.SetDrawFov(hUid, origFov, eyeNow);
            }
            else
            {
                _fovEffectCount[hUid] = c;
            }
        });

        _audio.PlayGlobal(
            new SoundPathSpecifier("/Audio/Imperial/heretic/i_see_you1.ogg"),
            Filter.Entities(hUid),
            false);
    }

    private void OnHereticSleep(EntityUid uid, HereticComponent comp, SleepStateChangedEvent args)
    {
        if (!args.FellAsleep)
            return;

        var rifts = new List<EntityUid>();
        var riftQuery = EntityQueryEnumerator<HereticRealityRiftComponent>();
        while (riftQuery.MoveNext(out var riftUid, out _))
            rifts.Add(riftUid);

        if (rifts.Count == 0)
            return;

        var rift = rifts[_random.Next(rifts.Count)];
        var location = GetNearestBeaconText(rift);

        string msg;
        if (location != null)
        {
            var template = LocationDreamMessages[_random.Next(LocationDreamMessages.Length)];
            msg = Loc.GetString(template, ("location", location));
        }
        else
        {
            msg = Loc.GetString(DreamMessages[_random.Next(DreamMessages.Length)]);
        }

        _heretic.SendHereticMessage(uid, msg);
    }

    private string? GetNearestBeaconText(EntityUid riftUid)
    {
        var riftPos = _xform.GetMapCoordinates(riftUid);
        var bestDistSq = float.MaxValue;
        string? bestText = null;

        var beaconQuery = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (beaconQuery.MoveNext(out var beaconUid, out var beacon, out var beaconXform))
        {
            if (!beacon.Enabled)
                continue;

            var beaconPos = _xform.GetMapCoordinates(beaconUid, beaconXform);
            if (beaconPos.MapId != riftPos.MapId)
                continue;

            var distSq = (beaconPos.Position - riftPos.Position).LengthSquared();
            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            var text = beacon.Text;
            if (string.IsNullOrWhiteSpace(text) && beacon.DefaultText != null)
                text = Loc.GetString(beacon.DefaultText);
            if (string.IsNullOrWhiteSpace(text))
                text = MetaData(beaconUid).EntityName;
            bestText = text;
        }

        return string.IsNullOrWhiteSpace(bestText) ? null : bestText;
    }

    private void OnRiftRemoved(EntityUid uid, HereticRealityRiftComponent _, ComponentRemove args)
    {
        var toRemove = new List<(EntityUid, EntityUid)>();
        foreach (var key in _lastTrigger.Keys)
        {
            if (key.Item1 == uid)
                toRemove.Add(key);
        }
        foreach (var key in toRemove)
            _lastTrigger.Remove(key);
    }

    private void OnHereticStartup(EntityUid uid, HereticComponent _, ComponentStartup args)
        => _eye.RefreshVisibilityMask(uid);

    private void OnHereticShutdown(EntityUid uid, HereticComponent _, ComponentShutdown args)
    {
        _eye.RefreshVisibilityMask(uid);
        _fovEffectCount.Remove(uid);
        _fovOriginalState.Remove(uid);
    }

    private void OnHereticVisMask(EntityUid uid, HereticComponent _, ref GetVisMaskEvent args)
        => args.VisibilityMask |= (int) VisibilityFlags.HereticRift;

    private void OnRiftInteract(EntityUid riftUid, HereticRealityRiftComponent rift, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<HereticComponent>(args.User, out _))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("heretic-rift-absorbing"), args.User, args.User);

        var absorbTime = _heretic.HasMorbiusCodex(args.User)
            ? rift.AbsorbTime * 0.5f
            : rift.AbsorbTime;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, absorbTime,
            new AbsorbRealityRiftDoAfterEvent(),
            eventTarget: args.User,
            used: riftUid)
        {
            BreakOnMove   = true,
            BreakOnDamage = true,
            NeedHand      = false,
            Hidden        = true,
        });
    }

    private void OnAbsorbDoAfter(EntityUid uid, HereticComponent comp, AbsorbRealityRiftDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        if (args.Used == null || !TryComp<HereticRealityRiftComponent>(args.Used, out var rift))
            return;

        args.Handled = true;
        _heretic.AddKnowledgePoints(uid, comp, rift.KnowledgeGain);
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), Filter.Entities(uid), false);
        _popup.PopupEntity(Loc.GetString("heretic-rift-absorbed"), uid, uid, PopupType.Medium);
        _heretic.SendHereticMessage(uid, Loc.GetString("heretic-rift-absorbed-chat"));

        var coords = _xform.GetMapCoordinates(args.Used.Value);
        var delay  = TimeSpan.FromSeconds(rift.RespawnDelay);
        QueueDel(args.Used.Value);

        Timer.Spawn(delay, () => SpawnBreach(coords));
    }

    // ─── Spawn API ────────────────────────────────────────────────────────────

    public EntityUid SpawnRift(EntityCoordinates coords)
        => Spawn("HereticRealityRift", coords);

    private void SpawnBreach(MapCoordinates coords)
        => Spawn("HereticRealityBreach", coords);
}
