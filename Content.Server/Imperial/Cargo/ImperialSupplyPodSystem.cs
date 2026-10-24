using Content.Server.Explosion.EntitySystems;
using Content.Server.Stack;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Imperial.Cargo;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cargo;

/// <summary>
/// Капсулы снабжения TG (code/modules/cargo/supplypod.dm): метка посадки, падение под углом,
/// лёгкий взрыв при посадке, раскрытие через delays[POD_OPENING] и улёт блюспейс-капсулы.
/// </summary>
public sealed class ImperialSupplyPodSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public static readonly EntProtoId LandingZone = "ImperialPodLandingZone";
    public static readonly EntProtoId StandardPod = "ImperialSupplyPod";
    public static readonly EntProtoId BluespacePod = "ImperialSupplyPodBluespace";
    private static readonly EntProtoId Sparks = "EffectSparks";
    private const string WeldingQuality = "Welding";

    /// <summary>material_drop: /obj/item/stack/sheet/iron, 2 листа.</summary>
    private const string MaterialDrop = "Steel";
    private const int MaterialDropAmount = 2;

    /// <summary>use_tool(src, user, 40): 4 секунды резки.</summary>
    private const float CutTime = 4f;

    private static readonly SoundSpecifier LongWhistle = new SoundPathSpecifier("/Audio/Imperial/Cargo/mortar_long_whistle.ogg");
    private static readonly SoundSpecifier ShortWhistle = new SoundPathSpecifier("/Audio/Imperial/Cargo/mortar_whistle.ogg");
    private static readonly SoundSpecifier ExplosionSound = new SoundCollectionSpecifier("Explosion");

    /// <summary>fallingSoundLength: 1.1 с у длинного свиста, 0.3 у короткого.</summary>
    private const float LongWhistleLength = 1.1f;

    /// <summary>Пока капсула открывается системой, открытие не отменяется.</summary>
    private bool _opening;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialSupplyPodComponent, StorageOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<ImperialSupplyPodComponent, StorageCloseAttemptEvent>(OnCloseAttempt);
        SubscribeLocalEvent<ImperialSupplyPodComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialSupplyPodComponent, ImperialSupplyPodCutDoAfterEvent>(OnCut);
        SubscribeLocalEvent<ImperialSupplyPodComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>new /obj/effect/pod_landingzone(turf, pod_type, order).</summary>
    public EntityUid Launch(EntityCoordinates target, EntProtoId? cargo, bool bluespace)
    {
        var zone = Spawn(LandingZone, target);
        var comp = EnsureComp<ImperialPodLandingZoneComponent>(zone);
        comp.Pod = bluespace ? BluespacePod : StandardPod;
        comp.Cargo = cargo;
        comp.Start = _timing.CurTime;
        comp.Arrival = comp.Start + TimeSpan.FromSeconds(comp.Transit + FallingOf(comp.Pod));
        Dirty(zone, comp);
        return zone;
    }

    private float FallingOf(EntProtoId pod)
    {
        return IoCManager.Resolve<IPrototypeManager>().Index(pod).TryGetComponent<ImperialSupplyPodComponent>(out var comp, EntityManager.ComponentFactory)
            ? comp.Falling
            : 0.4f;
    }

    private void OnOpenAttempt(Entity<ImperialSupplyPodComponent> ent, ref StorageOpenAttemptEvent args)
    {
        // toggle(): капсулу нельзя открыть руками.
        if (!_opening)
            args.Cancelled = true;
    }

    private void OnCloseAttempt(Entity<ImperialSupplyPodComponent> ent, ref StorageCloseAttemptEvent args)
    {
        args.Cancelled = true;
    }

    /// <summary>Капсула открыта и не блюспейс (tool_interact() у блюспейс-капсулы ничего не делает).</summary>
    private bool CanCut(Entity<ImperialSupplyPodComponent> ent)
    {
        return !ent.Comp.Bluespace && TryComp<EntityStorageComponent>(ent, out var storage) && storage.Open;
    }

    private void OnInteractUsing(Entity<ImperialSupplyPodComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !CanCut(ent) || !_tool.HasQuality(args.Used, WeldingQuality))
            return;

        args.Handled = _tool.UseTool(args.Used, args.User, ent, CutTime, WeldingQuality, new ImperialSupplyPodCutDoAfterEvent(), fuel: 1);
    }

    /// <summary>deconstruct(TRUE): листы железа на месте капсулы.</summary>
    private void OnCut(Entity<ImperialSupplyPodComponent> ent, ref ImperialSupplyPodCutDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !CanCut(ent))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-supplypod-cut-others", ("user", args.User), ("pod", ent.Owner)),
            ent, Robust.Shared.Player.Filter.PvsExcept(args.User), true);
        _popup.PopupEntity(Loc.GetString("imperial-supplypod-cut-self", ("pod", ent.Owner)), ent, args.User);
        _stack.SpawnAtPosition(MaterialDropAmount, MaterialDrop, Transform(ent).Coordinates);
        QueueDel(ent);
    }

    private void OnExamined(Entity<ImperialSupplyPodComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Bluespace)
            args.PushMarkup(Loc.GetString("imperial-supplypod-examine-cut"));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var zones = EntityQueryEnumerator<ImperialPodLandingZoneComponent>();
        while (zones.MoveNext(out var uid, out var zone))
        {
            UpdateZone((uid, zone), now);
        }

        var pods = EntityQueryEnumerator<ImperialSupplyPodComponent>();
        while (pods.MoveNext(out var uid, out var pod))
        {
            if (pod.OpenAt is { } open && now >= open)
            {
                pod.OpenAt = null;
                OpenPod((uid, pod));
            }

            if (pod.LeaveAt is { } leave && now >= leave)
            {
                pod.LeaveAt = null;
                // start_exit_sequence(): блюспейс-капсула исчезает в искрах.
                Spawn(Sparks, Transform(uid).Coordinates);
                QueueDel(uid);
            }
        }
    }

    private void UpdateZone(Entity<ImperialPodLandingZoneComponent> zone, TimeSpan now)
    {
        var comp = zone.Comp;
        var arrival = (float) (comp.Arrival - comp.Start).TotalSeconds;
        var falling = arrival - comp.Transit;

        if (!comp.SoundPlayed)
        {
            var longWhistle = arrival >= LongWhistleLength;
            var soundAt = comp.Transit - (longWhistle ? LongWhistleLength : 0.3f) + falling;
            if (now >= comp.Start + TimeSpan.FromSeconds(Math.Max(soundAt, 0.1f)))
            {
                comp.SoundPlayed = true;
                _audio.PlayPvs(longWhistle ? LongWhistle : ShortWhistle, zone, AudioParams.Default.WithVolume(4).WithMaxDistance(15));
            }
        }

        // begin_launch(): капсула появляется и падает.
        if (comp.PodEntity == null && now >= comp.Start + TimeSpan.FromSeconds(comp.Transit))
        {
            var pod = Spawn(comp.Pod, Transform(zone).Coordinates);
            comp.PodEntity = pod;
            var podComp = EnsureComp<ImperialSupplyPodComponent>(pod);
            podComp.Angle = _random.NextFloat(70, 110);
            podComp.LandTime = comp.Arrival;
            Dirty(pod, podComp);

            _physics.SetCanCollide(pod, false);
            _appearance.SetData(pod, ImperialSupplyPodVisuals.State, ImperialSupplyPodState.Falling);

            if (comp.Cargo is { } cargo && TryComp<EntityStorageComponent>(pod, out var storage))
            {
                var crate = Spawn(cargo, Transform(pod).Coordinates);
                _storage.Insert(crate, pod, storage);
            }
        }

        if (now < comp.Arrival || comp.PodEntity is not { } landed)
            return;

        QueueDel(zone);
        if (TryComp<ImperialSupplyPodComponent>(landed, out var landedComp))
            Land((landed, landedComp));
    }

    /// <summary>end_launch() и pre_open().</summary>
    private void Land(Entity<ImperialSupplyPodComponent> pod)
    {
        var comp = pod.Comp;
        comp.Landed = true;
        _physics.SetCanCollide(pod, true);
        _appearance.SetData(pod, ImperialSupplyPodVisuals.State, ImperialSupplyPodState.Closed);

        // try_make_rubble(): не на космосе.
        var coords = Transform(pod).Coordinates;
        var tile = _turf.GetTileRef(coords);
        _appearance.SetData(pod, ImperialSupplyPodVisuals.Rubble, tile is { } t && !t.Tile.IsEmpty && !_turf.IsSpace(t));

        if (comp.LightRange > 0 || comp.FlashRange > 0)
        {
            // explosion(turf, 0, 0, light, flash): только лёгкий урон, без пробоин.
            var light = comp.LightRange;
            _explosion.QueueExplosion(_transform.ToMapCoordinates(coords), "Default",
                totalIntensity: light * light * 3f, slope: 2f, maxTileIntensity: 2f, cause: pod, tileBreakScale: 0f, canCreateVacuum: false);
        }
        else
        {
            _audio.PlayPvs(ExplosionSound, pod);
        }

        comp.OpenAt = _timing.CurTime + TimeSpan.FromSeconds(comp.Opening);
    }

    /// <summary>open_pod(): содержимое выходит наружу.</summary>
    private void OpenPod(Entity<ImperialSupplyPodComponent> pod)
    {
        _opening = true;
        _storage.OpenStorage(pod);
        _opening = false;

        _appearance.SetData(pod, ImperialSupplyPodVisuals.State, ImperialSupplyPodState.Open);
        if (pod.Comp.Bluespace)
            pod.Comp.LeaveAt = _timing.CurTime + TimeSpan.FromSeconds(pod.Comp.Leaving * 4 / 5);
    }
}
