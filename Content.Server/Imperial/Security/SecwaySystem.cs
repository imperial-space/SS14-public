using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.ImperialVehicle.Components;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// vehicle/ridden/secway: едет с ключом СБ; банан в выхлопной трубе глушит мотор;
/// повреждённый наполовину дымит.
/// </summary>
public sealed class SecwaySystem : EntitySystem
{
    private static readonly SoundSpecifier StallSound = new SoundPathSpecifier("/Audio/Imperial/Security/stall.ogg");
    private static readonly SoundSpecifier StartupSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehiclestartup.ogg");

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly Content.Shared.Audio.SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const float MaxIntegrity = 60f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SecwayComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SecwayComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<SecwayComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<SecwayComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SecwayComponent, SecwayBananaDoAfterEvent>(OnBananaDoAfter);
        SubscribeLocalEvent<SecwayComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<SecwayComponent, SecwayCleanDoAfterEvent>(OnCleanDoAfter);
        SubscribeLocalEvent<SecwayComponent, Content.Shared.Examine.ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SecwayComponent, ImperialVehicleComponent>();
        while (query.MoveNext(out var uid, out var secway, out var vehicle))
        {
            // Мотор: заводится, когда седок на месте, ключ в замке и труба чистая.
            var running = vehicle.Rider != null && !secway.Blocked;
            if (running != secway.EngineRunning)
            {
                secway.EngineRunning = running;
                _ambient.SetAmbience(uid, running);
                if (running)
                    _audio.PlayPvs(StartupSound, uid, AudioParams.Default.WithVolume(-4f));
            }

            // driver_move(): банан в трубе — мотор глохнет.
            if (vehicle.Rider != null && HasBanana(uid) && now >= secway.NextStallMessage &&
                TryComp<InputMoverComponent>(uid, out var mover) && mover.HeldMoveButtons != MoveButtons.None)
            {
                secway.NextStallMessage = now + TimeSpan.FromSeconds(0.75);
                _popup.PopupEntity(Loc.GetString("secway-stall", ("secway", uid)), uid, PopupType.SmallCaution);
                _audio.PlayPvs(StallSound, uid, AudioParams.Default.WithVolume(-2f));
            }

            // process(): при половине прочности иногда пускает дым.
            if (now < secway.NextSmoke)
                continue;

            secway.NextSmoke = now + TimeSpan.FromSeconds(1);
            if (!TryComp<DamageableComponent>(uid, out var damageable) ||
                _damageable.GetTotalDamage((uid, damageable)) < MaxIntegrity * 0.5f || _random.Prob(0.1f))
            {
                continue;
            }

            var smoke = Spawn("Smoke", Transform(uid).Coordinates);
            _smoke.StartSmoke(smoke, new Solution(), 1f, 0);
        }
    }

    private void OnMapInit(Entity<SecwayComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<Container>(ent, SecwayComponent.ExhaustContainer);
        UpdateBlocked(ent);
    }

    private void OnContainerChanged(Entity<SecwayComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateBlocked(ent);
    }

    private void OnContainerChanged(Entity<SecwayComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateBlocked(ent);
    }

    /// <summary>keycheck() и eddie_murphy: без ключа или с бананом секвей стоит.</summary>
    private void UpdateBlocked(Entity<SecwayComponent> ent)
    {
        var blocked = _itemSlots.GetItemOrNull(ent, SecwayComponent.KeySlot) == null || HasBanana(ent);
        if (ent.Comp.Blocked == blocked)
            return;

        ent.Comp.Blocked = blocked;
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private bool HasBanana(EntityUid uid)
    {
        return _container.TryGetContainer(uid, SecwayComponent.ExhaustContainer, out var exhaust) && exhaust.ContainedEntities.Count > 0;
    }

    /// <summary>item_interaction(): засунуть банан в трубу.</summary>
    private void OnInteractUsing(Entity<SecwayComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, "Banana") || HasBanana(ent))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("secway-banana-start", ("user", args.User), ("item", args.Used), ("secway", ent.Owner)),
            ent, PopupType.SmallCaution);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), new SecwayBananaDoAfterEvent(),
            ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnBananaDoAfter(Entity<SecwayComponent> ent, ref SecwayBananaDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } banana || HasBanana(ent))
            return;

        args.Handled = true;
        var exhaust = _container.EnsureContainer<Container>(ent, SecwayComponent.ExhaustContainer);
        if (_container.Insert(banana, exhaust))
            _popup.PopupEntity(Loc.GetString("secway-banana-done", ("user", args.User), ("item", banana), ("secway", ent.Owner)), ent, PopupType.SmallCaution);
    }

    /// <summary>attack_hand(): вычистить банан из трубы (6 секунд).</summary>
    private void OnVerbs(Entity<SecwayComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasBanana(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("secway-verb-clean"),
            Act = () =>
            {
                _popup.PopupEntity(Loc.GetString("secway-clean-start", ("user", user), ("secway", ent.Owner)), ent);
                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(6), new SecwayCleanDoAfterEvent(), ent, ent)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                });
            },
        });
    }

    private void OnCleanDoAfter(Entity<SecwayComponent> ent, ref SecwayCleanDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_container.TryGetContainer(ent, SecwayComponent.ExhaustContainer, out var exhaust))
            return;

        args.Handled = true;
        foreach (var item in new List<EntityUid>(exhaust.ContainedEntities))
        {
            _container.Remove(item, exhaust, destination: Transform(ent).Coordinates);
        }

        _popup.PopupEntity(Loc.GetString("secway-clean-done", ("user", args.User), ("secway", ent.Owner)), ent);
    }

    private void OnExamined(Entity<SecwayComponent> ent, ref Content.Shared.Examine.ExaminedEvent args)
    {
        if (HasBanana(ent))
            args.PushMarkup(Loc.GetString("secway-examine-banana"));
    }
}
