using Robust.Shared.Random;
using System.Linq;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Drunk;
using Content.Shared.Imperial.TerrorSpider;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

public sealed partial class TerrorSpiderSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly WeldableSystem _weldable = default!;

    private static readonly EntProtoId RoyalJelly = "TerrorRoyalJelly";
    private static readonly EntProtoId Cocoon = "TerrorCocoon";
    private static readonly EntProtoId HealEffect = "TerrorHealEffect";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private const string CocoonContainer = "cocoon";
    private const string CarriedContainer = "terror_spiderlings";

    private static readonly SoundSpecifier PrySound = new SoundPathSpecifier("/Audio/Machines/airlock_creaking.ogg");
    private static readonly SoundSpecifier MeteorSound = new SoundPathSpecifier("/Audio/Effects/meteorimpact.ogg");

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<TerrorSpiderComponent, TerrorSpiderWebActionEvent>(OnWebAction);
        SubscribeLocalEvent<TerrorSpiderComponent, TerrorSpiderWebDoAfterEvent>(OnWebDoAfter);
        SubscribeLocalEvent<TerrorSpiderComponent, TerrorSpiderWrapActionEvent>(OnWrapAction);
        SubscribeLocalEvent<TerrorSpiderComponent, TerrorSpiderWrapDoAfterEvent>(OnWrapDoAfter);
        SubscribeLocalEvent<TerrorSpiderComponent, TerrorSpiderVentSmashActionEvent>(OnVentSmashAction);
        SubscribeLocalEvent<TerrorSpiderComponent, TerrorSpiderVentSmashDoAfterEvent>(OnVentSmashDoAfter);
        SubscribeLocalEvent<TerrorSpiderComponent, TerrorSpiderRemoteViewActionEvent>(OnRemoteViewAction);
        SubscribeLocalEvent<TerrorSpiderComponent, TerrorSpiderChoiceMessage>(OnChoice);

        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderRoyalJellyActionEvent>(OnRoyalJellyAction);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderJellyDoAfterEvent>(OnJellyDoAfter);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderGatherSpiderlingsActionEvent>(OnGatherSpiderlings);
        SubscribeLocalEvent<TerrorSpiderMotherComponent, TerrorSpiderIncubateEggsActionEvent>(OnIncubateEggs);

        SubscribeLocalEvent<TerrorSpiderPrinceComponent, TerrorSpiderPrinceChargeActionEvent>(OnPrinceCharge);
        SubscribeLocalEvent<TerrorSpiderPrinceComponent, ThrowDoHitEvent>(OnPrinceImpact);
        SubscribeLocalEvent<TerrorSpiderPrinceComponent, StopThrowEvent>(OnPrinceStopThrow);

        SubscribeLocalEvent<TerrorSpiderGreenComponent, TerrorSpiderGreenEggsActionEvent>(OnGreenEggsAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderQueenNestActionEvent>(OnQueenNestAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderHiveSenseActionEvent>(OnHiveSenseAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderQueenEggsActionEvent>(OnQueenEggsAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderEmpressLingsActionEvent>(OnEmpressLingsAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderEmpressEraseActionEvent>(OnEmpressEraseAction);

        SubscribeLocalEvent<MobStateChangedEvent>(OnAnyMobStateChanged);
    }

    /// <summary>Паук на полу, а не в трубе или контейнере.</summary>
    private bool OnFloor(EntityUid uid)
    {
        return !_container.IsEntityInContainer(uid) && Transform(uid).GridUid != null;
    }

    private bool IsSpace(EntityUid uid)
    {
        return _turf.GetTileRef(Transform(uid).Coordinates) is not { } tile || _turf.IsSpace(tile);
    }

    #region Паутина

    private void OnWebAction(Entity<TerrorSpiderComponent> ent, ref TerrorSpiderWebActionEvent args)
    {
        if (args.Handled || ent.Comp.Web == null)
            return;

        args.Handled = true;
        if (!OnFloor(ent))
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-web-floor"), ent, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("terror-spider-web-start", ("spider", ent.Owner)), ent);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ent.Comp.WebDelay, new TerrorSpiderWebDoAfterEvent(), ent)
        {
            BreakOnMove = true,
        });
    }

    /// <summary>Web().</summary>
    private void OnWebDoAfter(Entity<TerrorSpiderComponent> ent, ref TerrorSpiderWebDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Web is not { } web)
            return;

        args.Handled = true;
        if (IsSpace(ent))
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-web-space"), ent, ent);
            return;
        }

        if (WebAt(Transform(ent).Coordinates) != null)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-web-exists"), ent, ent);
            return;
        }

        Spawn(web, _transform.GetMoverCoordinates(ent));
    }

    public EntityUid? WebAt(EntityCoordinates coords)
    {
        foreach (var web in _lookup.GetEntitiesInRange<TerrorWebComponent>(coords, 0.3f))
        {
            return web;
        }

        return null;
    }

    private bool OnWeb(EntityUid uid)
    {
        return WebAt(Transform(uid).Coordinates) != null;
    }

    #endregion

    #region Кокон

    /// <summary>mobIsWrappable().</summary>
    private bool MobIsWrappable(EntityUid spider, EntityUid mob)
    {
        return HasComp<MobStateComponent>(mob) && _mobState.IsDead(mob) && !Transform(mob).Anchored
               && !HasComp<TerrorSpiderComponent>(mob) && InRange(spider, mob, 1.5f);
    }

    private bool InRange(EntityUid a, EntityUid b, float range)
    {
        var pa = _transform.GetMapCoordinates(a);
        var pb = _transform.GetMapCoordinates(b);
        return pa.MapId == pb.MapId && (pa.Position - pb.Position).Length() <= range;
    }

    /// <summary>FindWrapTarget(): трупы и незакреплённые предметы рядом.</summary>
    private List<EntityUid> WrapChoices(EntityUid spider)
    {
        var choices = new List<EntityUid>();
        foreach (var uid in _lookup.GetEntitiesInRange(spider, 1.5f, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (uid == spider || _container.IsEntityInContainer(uid))
                continue;

            if (HasComp<MobStateComponent>(uid))
            {
                if (MobIsWrappable(spider, uid))
                    choices.Add(uid);
                continue;
            }

            if (Transform(uid).Anchored || HasComp<TerrorWebComponent>(uid) || HasComp<TerrorEggClusterComponent>(uid)
                || HasComp<TerrorCocoonComponent>(uid) || HasComp<TerrorRoyalJellyComponent>(uid))
                continue;

            if (HasComp<ItemComponent>(uid) || HasComp<PhysicsComponent>(uid) && HasComp<MetaDataComponent>(uid) && HasComp<Content.Shared.Damage.Components.DamageableComponent>(uid))
                choices.Add(uid);
        }

        return choices;
    }

    private void OnWrapAction(Entity<TerrorSpiderComponent> ent, ref TerrorSpiderWrapActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.CocoonTarget is { } existing && Exists(existing))
        {
            DoWrap(ent);
            return;
        }

        var choices = WrapChoices(ent);
        if (choices.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-wrap-nothing"), ent, ent);
            return;
        }

        OpenChoice(ent, TerrorSpiderChoiceMode.Wrap, Loc.GetString("terror-spider-wrap-title"),
            choices.Select(c => new TerrorSpiderChoiceOption(GetNetEntity(c).ToString(), Name(c))).ToArray());
    }

    /// <summary>DoWrap(): 4 секунды на кокон.</summary>
    public void DoWrap(Entity<TerrorSpiderComponent> ent)
    {
        if (ent.Comp.CocoonTarget is not { } target || ent.Comp.Busy)
            return;

        if (!Exists(target) || Transform(target).Anchored)
        {
            ent.Comp.CocoonTarget = null;
            return;
        }

        ent.Comp.Busy = true;
        _popup.PopupEntity(Loc.GetString("terror-spider-wrap-start", ("spider", ent.Owner), ("target", target)), ent);
        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(4), new TerrorSpiderWrapDoAfterEvent(), ent, target)
        {
            BreakOnMove = true,
            DistanceThreshold = 1.5f,
        }))
        {
            ent.Comp.Busy = false;
            ent.Comp.CocoonTarget = null;
        }
    }

    private void OnWrapDoAfter(Entity<TerrorSpiderComponent> ent, ref TerrorSpiderWrapDoAfterEvent args)
    {
        ent.Comp.Busy = false;
        var target = ent.Comp.CocoonTarget;
        ent.Comp.CocoonTarget = null;
        if (args.Handled || args.Cancelled || target is not { } cocoonTarget || !Exists(cocoonTarget))
            return;

        args.Handled = true;
        if (_container.IsEntityInContainer(cocoonTarget) || !InRange(ent, cocoonTarget, 1.5f))
            return;

        var coords = _transform.GetMoverCoordinates(cocoonTarget);
        var cocoon = Spawn(Cocoon, coords);
        var container = _container.EnsureContainer<Container>(cocoon, CocoonContainer);
        var large = false;

        foreach (var uid in _lookup.GetEntitiesInRange(coords, 0.4f, LookupFlags.Dynamic | LookupFlags.Sundries).ToList())
        {
            if (uid == cocoon || uid == ent.Owner || Transform(uid).Anchored || HasComp<MobStateComponent>(uid)
                || HasComp<TerrorWebComponent>(uid) || HasComp<TerrorEggClusterComponent>(uid) || HasComp<TerrorCocoonComponent>(uid))
                continue;

            if (_container.Insert(uid, container) && !HasComp<ItemComponent>(uid))
                large = true;
        }

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.4f).ToList())
        {
            if (!MobIsWrappable(ent, mob))
                continue;

            if (HasComp<Content.Shared.Body.BodyComponent>(mob))
            {
                // Высасывает вязкую субстанцию — регенерация и сытость.
                ent.Comp.RegenPoints += ent.Comp.RegenPointsPerKill;
                ent.Comp.Fed++;
                _popup.PopupEntity(Loc.GetString("terror-spider-wrap-feed", ("spider", ent.Owner), ("target", mob.Owner)), ent, PopupType.MediumCaution);
                _popup.PopupEntity(Loc.GetString("terror-spider-wrap-invigorated"), ent, ent);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("terror-spider-wrap-mob", ("spider", ent.Owner), ("target", mob.Owner)), ent);
            }

            large = true;
            ent.Comp.LastCocoonObject = TimeSpan.Zero;
            _container.Insert(mob.Owner, container);
            break;
        }

        _appearance.SetData(cocoon, TerrorSpiderVisuals.Stage, large ? _random.Next(3, 6) : _random.Next(0, 3));
    }

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    /// <summary>LoseTarget(): если цель упала — счётчик убийств и очки регенерации.</summary>
    private void OnAnyMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive || args.Origin is not { } origin
            || !TryComp<TerrorSpiderComponent>(origin, out var spider) || HasComp<TerrorSpiderComponent>(args.Target))
            return;

        if (args.OldMobState != MobState.Alive)
            return;

        spider.KillCount++;
        spider.RegenPoints += spider.RegenPointsPerKill;
    }

    #endregion

    #region Вентиляция

    private List<EntityUid> WeldedVents(EntityUid uid, float range)
    {
        var result = new List<EntityUid>();
        foreach (var vent in _lookup.GetEntitiesInRange<WeldableComponent>(_transform.GetMapCoordinates(uid), range))
        {
            if (!vent.Comp.IsWelded)
                continue;
            if (HasComp<GasVentPumpComponent>(vent) || HasComp<GasVentScrubberComponent>(vent))
                result.Add(vent);
        }

        return result;
    }

    /// <summary>DoVentSmash().</summary>
    private void OnVentSmashAction(Entity<TerrorSpiderComponent> ent, ref TerrorSpiderVentSmashActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (WeldedVents(ent, 1.5f).Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-ventsmash-none"), ent, ent);
            return;
        }

        _audio.PlayPvs(PrySound, ent);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(4), new TerrorSpiderVentSmashDoAfterEvent(), ent)
        {
            BreakOnMove = true,
        });
    }

    private void OnVentSmashDoAfter(Entity<TerrorSpiderComponent> ent, ref TerrorSpiderVentSmashDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var vents = WeldedVents(ent, 1.5f);
        if (vents.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-ventsmash-none"), ent, ent);
            return;
        }

        SmashVent(ent, vents[0]);
        _transform.SetCoordinates(ent, Transform(vents[0]).Coordinates);
    }

    private void SmashVent(EntityUid spider, EntityUid vent)
    {
        _weldable.SetWeldedState(vent, false);
        _popup.PopupEntity(Loc.GetString("terror-spider-ventsmash", ("spider", spider), ("vent", vent)), vent, PopupType.MediumCaution);
    }

    #endregion

    #region Удалённый обзор

    /// <summary>DoRemoteView().</summary>
    private void OnRemoteViewAction(Entity<TerrorSpiderComponent> ent, ref TerrorSpiderRemoteViewActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!OnFloor(ent))
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-remote-view-floor"), ent, ent);
            return;
        }

        if (ent.Comp.Watching != null)
        {
            StopRemoteView(ent);
            return;
        }

        if (TryComp<Content.Shared.Damage.Components.DamageableComponent>(ent, out var damageable) && _damageable.GetTotalDamage((ent.Owner, damageable)) > 0)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-remote-view-health"), ent, ent);
            return;
        }

        var options = new List<TerrorSpiderChoiceOption> { new(GetNetEntity(ent).ToString(), Name(ent)) };
        foreach (var spider in LivingSpiders())
        {
            if (spider.Owner != ent.Owner)
                options.Add(new TerrorSpiderChoiceOption(GetNetEntity(spider).ToString(), Name(spider)));
        }

        OpenChoice(ent, TerrorSpiderChoiceMode.RemoteView, Loc.GetString("terror-spider-remote-view-title"), options.ToArray());
    }

    private void StartRemoteView(Entity<TerrorSpiderComponent> ent, EntityUid target)
    {
        if (target == ent.Owner)
        {
            StopRemoteView(ent);
            return;
        }

        ent.Comp.Watching = target;
        _eye.SetTarget(ent, target);
    }

    private void StopRemoteView(Entity<TerrorSpiderComponent> ent)
    {
        if (ent.Comp.Watching == null)
            return;

        ent.Comp.Watching = null;
        _eye.SetTarget(ent, null);
    }

    #endregion

    #region Мать

    /// <summary>DoCreateJelly().</summary>
    private void OnRoyalJellyAction(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderRoyalJellyActionEvent args)
    {
        if (args.Handled || !TryComp<TerrorSpiderComponent>(ent, out var spider))
            return;

        args.Handled = true;
        if (spider.RegenPoints < ent.Comp.JellyCost)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-jelly-points", ("points", (int) spider.RegenPoints), ("cost", (int) ent.Comp.JellyCost)), ent, ent);
            return;
        }

        if (!OnFloor(ent))
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-jelly-floor"), ent, ent);
            return;
        }

        if (IsSpace(ent))
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-jelly-space"), ent, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("terror-spider-jelly-start", ("spider", ent.Owner)), ent);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(10), new TerrorSpiderJellyDoAfterEvent(), ent)
        {
            BreakOnMove = true,
        });
    }

    private void OnJellyDoAfter(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderJellyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !TryComp<TerrorSpiderComponent>(ent, out var spider))
            return;

        args.Handled = true;
        if (spider.RegenPoints < ent.Comp.JellyCost)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-jelly-points", ("points", (int) spider.RegenPoints), ("cost", (int) ent.Comp.JellyCost)), ent, ent);
            return;
        }

        Spawn(RoyalJelly, _transform.GetMoverCoordinates(ent));
        spider.RegenPoints -= ent.Comp.JellyCost;
    }

    /// <summary>consume_jelly().</summary>
    private void ConsumeJelly(Entity<TerrorSpiderComponent> ent, EntityUid jelly)
    {
        if (HasComp<TerrorSpiderMotherComponent>(ent))
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-jelly-mother"), ent, ent);
            return;
        }

        if (ent.Comp.RegenPointsPerTick >= ent.Comp.RegenPointsPerHp)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-jelly-no-benefit"), ent, ent);
            return;
        }

        if (ent.Comp.RegenPoints > 200)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-jelly-not-hungry"), ent, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("terror-spider-jelly-consumed"), ent, ent);
        ent.Comp.RegenPoints += ent.Comp.RegenPointsPerJelly;
        ent.Comp.Fed++;
        QueueDel(jelly);
    }

    /// <summary>PickupSpiderlings(): паучата в радиусе 2 садятся на спину.</summary>
    private void OnGatherSpiderlings(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderGatherSpiderlingsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var container = _container.EnsureContainer<Container>(ent, CarriedContainer);
        var count = 0;
        foreach (var ling in _lookup.GetEntitiesInRange<TerrorSpiderlingComponent>(_transform.GetMapCoordinates(ent), 2.5f).ToList())
        {
            if (_container.IsEntityInContainer(ling))
                continue;
            Spawn(HealEffect, _transform.GetMoverCoordinates(ling));
            if (_container.Insert(ling.Owner, container))
                count++;
        }

        _popup.PopupEntity(count > 0
            ? Loc.GetString("terror-spider-gather", ("count", count))
            : Loc.GetString("terror-spider-gather-none"), ent, ent);
    }

    /// <summary>DropSpiderlings(): при смерти матери.</summary>
    private void DropSpiderlings(EntityUid mother)
    {
        if (_container.TryGetContainer(mother, CarriedContainer, out var container))
            _container.EmptyContainer(container, true, _transform.GetMoverCoordinates(mother));
    }

    /// <summary>IncubateEggs(): +25 к росту яиц под собой за 25 очков (нужно 50).</summary>
    private void OnIncubateEggs(Entity<TerrorSpiderMotherComponent> ent, ref TerrorSpiderIncubateEggsActionEvent args)
    {
        if (args.Handled || !TryComp<TerrorSpiderComponent>(ent, out var spider))
            return;

        args.Handled = true;
        if (spider.RegenPoints < 50)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-incubate-points", ("points", (int) spider.RegenPoints)), ent, ent);
            return;
        }

        foreach (var eggs in _lookup.GetEntitiesInRange<TerrorEggClusterComponent>(_transform.GetMapCoordinates(ent), 0.5f))
        {
            Spawn(HealEffect, _transform.GetMoverCoordinates(eggs));
            eggs.Comp.AmountGrown += 25;
            spider.RegenPoints -= 25;
            _popup.PopupEntity(Loc.GetString("terror-spider-incubate", ("eggs", eggs.Owner)), ent, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("terror-spider-incubate-none"), ent, ent);
    }

    #endregion

    #region Принц

    /// <summary>princely_charge: бросок к точке на 9 тайлов.</summary>
    private void OnPrinceCharge(Entity<TerrorSpiderPrinceComponent> ent, ref TerrorSpiderPrinceChargeActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var from = _transform.GetMapCoordinates(ent);
        var to = _transform.ToMapCoordinates(args.Target);
        if (from.MapId != to.MapId)
            return;

        var direction = to.Position - from.Position;
        if (direction.LengthSquared() < 0.01f)
            return;

        if (direction.Length() > 9)
            direction = direction.Normalized() * 9;

        ent.Comp.Charging = true;
        _throwing.TryThrow(ent, direction, 12f, ent, 0, doSpin: false, playSound: false);
    }

    private void OnPrinceStopThrow(Entity<TerrorSpiderPrinceComponent> ent, ref StopThrowEvent args)
    {
        ent.Comp.Charging = false;
    }

    /// <summary>prince/throw_impact().</summary>
    private void OnPrinceImpact(Entity<TerrorSpiderPrinceComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!ent.Comp.Charging)
            return;

        var target = args.Target;
        var hit = false;

        if (HasComp<MobStateComponent>(target))
        {
            Damage(target, "Blunt", 60, ent);
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(12), true);
            _drunk.TryApplyDrunkenness(target, TimeSpan.FromSeconds(10));
            hit = true;
        }
        else if (_tag.HasTag(target, WallTag))
        {
            // dismantle_wall()
            Damage(target, "Structural", 1000, ent);
            hit = true;
        }
        else
        {
            Damage(target, "Structural", 150, ent);
            hit = true;
        }

        if (!Transform(target).Anchored)
        {
            var dir = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(ent);
            if (dir.LengthSquared() > 0.001f)
                _throwing.TryThrow(target, dir.Normalized() * 5, 12f, ent);
        }

        if (!hit)
            return;

        ent.Comp.Charging = false;
        _popup.PopupEntity(Loc.GetString("terror-spider-prince-slam", ("spider", ent.Owner), ("target", target)), ent, PopupType.LargeCaution);
        _audio.PlayPvs(MeteorSound, ent);
    }

    #endregion

    #region Окно выбора

    public void OpenChoice(EntityUid spider, TerrorSpiderChoiceMode mode, string title, TerrorSpiderChoiceOption[] options,
        int[]? counts = null, int[]? percents = null)
    {
        if (!TryComp<ActorComponent>(spider, out var actor))
            return;

        _ui.OpenUi(spider, TerrorSpiderChoiceUiKey.Key, actor.PlayerSession);
        _ui.SetUiState(spider, TerrorSpiderChoiceUiKey.Key,
            new TerrorSpiderChoiceBoundUserInterfaceState(mode, title, options, counts ?? Array.Empty<int>(), percents ?? Array.Empty<int>()));
    }

    private void OnChoice(Entity<TerrorSpiderComponent> ent, ref TerrorSpiderChoiceMessage args)
    {
        _ui.CloseUi(ent.Owner, TerrorSpiderChoiceUiKey.Key);
        if (_mobState.IsDead(ent))
            return;

        switch (args.Mode)
        {
            case TerrorSpiderChoiceMode.Wrap:
                if (NetEntity.TryParse(args.Key, out var wrapNet) && GetEntity(wrapNet) is { Valid: true } wrapTarget
                    && WrapChoices(ent).Contains(wrapTarget))
                {
                    ent.Comp.CocoonTarget = wrapTarget;
                    DoWrap(ent);
                }
                break;
            case TerrorSpiderChoiceMode.RemoteView:
                if (NetEntity.TryParse(args.Key, out var viewNet) && GetEntity(viewNet) is { Valid: true } viewTarget
                    && HasComp<TerrorSpiderComponent>(viewTarget) && !_mobState.IsDead(viewTarget))
                    StartRemoteView(ent, viewTarget);
                break;
            case TerrorSpiderChoiceMode.Eggs:
                OnEggChoice(ent, args.Key, args.Count);
                break;
            case TerrorSpiderChoiceMode.Spiderlings:
                if (TryComp<TerrorSpiderQueenComponent>(ent, out var empress) && empress.Empress)
                    EmpressLings((ent, ent.Comp, empress), args.Count, args.Percent);
                break;
            case TerrorSpiderChoiceMode.Nest:
                if (args.Key == "yes" && TryComp<TerrorSpiderQueenComponent>(ent, out var queen) && !queen.HasNested)
                    NestMode((ent, ent.Comp, queen));
                break;
        }
    }

    #endregion
}
