using System.Linq;
using Content.Server.Atmos.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Voidwalker;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Voidwalker;

/// <summary>Серверные данные проклятия /datum/brain_trauma/voided.</summary>
[RegisterComponent]
public sealed partial class ImperialVoidedStateComponent : Component
{
    /// <summary>banned_from_space: последние клетки не в космосе.</summary>
    [ViewVariables]
    public readonly List<EntityCoordinates> LastTiles = new();

    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>Аллергия на планеты: только если проклятие получено не на планете.</summary>
    [ViewVariables]
    public bool PlanetAllergy;

    [ViewVariables]
    public bool AddedPacified;
}

/// <summary>/obj/item/clothing/head/helmet/skull/cosmic.</summary>
[RegisterComponent]
public sealed partial class ImperialCosmicSkullComponent : Component
{
    [DataField]
    public int Uses = 1;
}

/// <summary>/obj/item/void_eater: временно разбивает стекло касанием.</summary>
[RegisterComponent]
public sealed partial class ImperialVoidEaterComponent : Component;

public sealed class ImperialVoidedSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedImperialGlassPasserSystem _glass = default!;

    /// <summary>space_colors.</summary>
    private static readonly string[] SpaceColors =
    {
        "#00ccff", "#b12bff", "#ff7f3a", "#ff1c55", "#ff7597", "#28ff94", "#0fcfff", "#ff8b4c", "#ffc425", "#2dff96", "#1770ff",
        "#ff3f31", "#ffba3b",
    };

    private static readonly SoundSpecifier ShatterSound = new SoundCollectionSpecifier("GlassBreak");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialVoidedComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<ImperialVoidedComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<ImperialVoidedComponent, MobStateChangedEvent>(OnStateChanged);

        SubscribeLocalEvent<ImperialCosmicSkullComponent, UseInHandEvent>(OnSkullUse);
        SubscribeLocalEvent<ImperialCosmicSkullComponent, ImperialCosmicSkullDoAfterEvent>(OnSkullDoAfter);
        SubscribeLocalEvent<ImperialVoidEaterComponent, AfterInteractEvent>(OnEaterInteract);
        SubscribeLocalEvent<ImperialGlassPasserComponent, StartCollideEvent>(OnPasserBump);
        SubscribeLocalEvent<ImperialGlassPasserComponent, ImperialGlassPassDoAfterEvent>(OnPassDoAfter);
    }

    /// <summary>gain_trauma(/datum/brain_trauma/voided[/stable]).</summary>
    public void MakeVoided(EntityUid uid, bool stable)
    {
        var voided = EnsureComp<ImperialVoidedComponent>(uid);
        voided.Stable = stable;
        if (voided.SpaceColor == Color.White && _random.Prob(0.5f))
            voided.SpaceColor = Color.FromHex(_random.Pick(SpaceColors));
        Dirty(uid, voided);

        var state = EnsureComp<ImperialVoidedStateComponent>(uid);
        state.PlanetAllergy = !IsOnPlanet(uid);

        if (stable)
        {
            // TRAIT_RESISTLOWPRESSURE, TRAIT_RESISTCOLD и проход сквозь стекло за 2 секунды.
            if (state.AddedPacified)
            {
                RemComp<PacifiedComponent>(uid);
                state.AddedPacified = false;
            }

            EnsureComp<PressureImmunityComponent>(uid);
            var passer = EnsureComp<ImperialGlassPasserComponent>(uid);
            passer.PassTime = TimeSpan.FromSeconds(2);
            passer.DeformGlass = TimeSpan.FromSeconds(2);
            Dirty(uid, passer);
            return;
        }

        if (!HasComp<PacifiedComponent>(uid))
        {
            EnsureComp<PacifiedComponent>(uid);
            state.AddedPacified = true;
        }
    }

    public void RemoveVoided(EntityUid uid)
    {
        if (TryComp<ImperialVoidedStateComponent>(uid, out var state) && state.AddedPacified)
            RemComp<PacifiedComponent>(uid);

        RemComp<ImperialVoidedComponent>(uid);
        RemComp<ImperialVoidedStateComponent>(uid);
    }

    private bool IsOnPlanet(EntityUid uid)
    {
        return Transform(uid).MapUid is { } map && HasComp<BiomeComponent>(map);
    }

    private bool IsSpace(EntityUid uid)
    {
        return EntityManager.System<ImperialVoidwalkerSystem>().IsSpace(uid);
    }

    /// <summary>physiology.brute_mod *= 1.1: стеклянные, хрупкие.</summary>
    private void OnDamageModify(Entity<ImperialVoidedComponent> ent, ref DamageModifyEvent args)
    {
        foreach (var type in new[] { "Blunt", "Slash", "Piercing" })
        {
            if (args.Damage.DamageDict.TryGetValue(type, out var value) && value > 0)
                args.Damage.DamageDict[type] = value * 1.1f;
        }
    }

    /// <summary>debris_bleeder: из ран сыплется стекло.</summary>
    private void OnDamaged(Entity<ImperialVoidedComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        var brute = delta.DamageDict.Where(d => d.Key.Id is "Blunt" or "Slash" or "Piercing").Sum(d => d.Value.Float());
        if (brute >= 5f && _random.Prob(0.3f))
            Spawn("ShardGlass", Transform(ent).Coordinates);
        if (brute >= 20f)
            _audio.PlayPvs(ShatterSound, ent);
    }

    /// <summary>on_death(): на планете проклятие уходит.</summary>
    private void OnStateChanged(Entity<ImperialVoidedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && IsOnPlanet(ent))
            RemoveVoided(ent);
    }

    #region Космический череп

    /// <summary>cosmic/attack_self(): 10 секунд вглядываться — стабильное проклятие или пожиратель пустоты.</summary>
    private void OnSkullUse(Entity<ImperialCosmicSkullComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (HasComp<ImperialVoidwalkerComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("imperial-voidwalker-skull-horror"), args.User, args.User, PopupType.LargeCaution);
            return;
        }

        if (ent.Comp.Uses <= 0 || !HasComp<HumanoidProfileComponent>(args.User))
            return;

        _popup.PopupEntity(Loc.GetString("imperial-voidwalker-skull-start", ("skull", ent.Owner)), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(10), new ImperialCosmicSkullDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnSkullDoAfter(Entity<ImperialCosmicSkullComponent> ent, ref ImperialCosmicSkullDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Uses <= 0)
            return;

        args.Handled = true;
        var user = args.User;
        ent.Comp.Uses--;
        if (ent.Comp.Uses <= 0)
        {
            _appearance.SetData(ent, ImperialCosmicSkullVisuals.Drained, true);
            RemComp<Robust.Server.GameObjects.PointLightComponent>(ent);
        }

        if (TryComp<ImperialVoidedComponent>(user, out var voided) && voided.Stable)
        {
            var eater = Spawn("ImperialVoidEater", Transform(user).Coordinates);
            _hands.TryPickupAnyHand(user, eater);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Voidwalker/blobattack.ogg"), user);
        }
        else
        {
            MakeVoided(user, true);
        }

        _popup.PopupEntity(Loc.GetString("imperial-voidwalker-skull-world"), user, user, PopupType.Large);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Voidwalker/curse5.ogg"), user);
    }

    /// <summary>temporary_glass_shatterer: касание размягчает стекло на 5 секунд.</summary>
    private void OnEaterInteract(Entity<ImperialVoidEaterComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !_glass.IsGlass(target))
            return;

        args.Handled = true;
        Deform(target, TimeSpan.FromSeconds(5));
        _audio.PlayPvs(ShatterSound, target);
    }

    public void Deform(EntityUid glass, TimeSpan time)
    {
        var deformed = EnsureComp<ImperialDeformedGlassComponent>(glass);
        deformed.Until = _timing.CurTime + time;
        Dirty(glass, deformed);
    }

    #endregion

    #region Проход сквозь стекло

    /// <summary>glass_passer/bumped(): упёрся в стекло — продавиться за pass_time.</summary>
    private void OnPasserBump(Entity<ImperialGlassPasserComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.PassTime <= TimeSpan.Zero || !_glass.IsGlass(args.OtherEntity) || _timing.CurTime < ent.Comp.PhasingUntil)
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ent.Comp.PassTime, new ImperialGlassPassDoAfterEvent(), ent, args.OtherEntity)
        {
            BreakOnMove = false,
            RequireCanInteract = false,
            DistanceThreshold = 1.5f,
        });
    }

    private void OnPassDoAfter(Entity<ImperialGlassPasserComponent> ent, ref ImperialGlassPassDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.PhasingUntil = _timing.CurTime + TimeSpan.FromSeconds(1);
        Dirty(ent);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        // on_moved(): стекло, через которое прошёл пустоходец, остаётся мягким deform_glass секунд.
        var passers = EntityQueryEnumerator<ImperialGlassPasserComponent, TransformComponent>();
        while (passers.MoveNext(out var uid, out var passer, out var xform))
        {
            if (passer.DeformGlass <= TimeSpan.Zero)
                continue;

            foreach (var near in EntityManager.System<EntityLookupSystem>().GetEntitiesInRange(xform.Coordinates, 0.4f, LookupFlags.Static))
            {
                if (near != uid && _glass.IsGlass(near) && (!TryComp<ImperialDeformedGlassComponent>(near, out var d) || d.Until < now + passer.DeformGlass - TimeSpan.FromSeconds(1)))
                    Deform(near, passer.DeformGlass);
            }
        }

        var query = EntityQueryEnumerator<ImperialVoidedComponent, ImperialVoidedStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var voided, out var state, out var xform))
        {
            if (voided.Stable)
                continue;

            // banned_from_space: в космос не пускают — возвращает на прежние клетки.
            if (IsSpace(uid))
            {
                if (state.LastTiles.Count > 0)
                {
                    _transform.SetCoordinates(uid, state.LastTiles[0]);
                    _popup.PopupEntity(Loc.GetString("imperial-voidwalker-banned-from-space"), uid, uid, PopupType.MediumCaution);
                }
            }
            else if (xform.GridUid is { } onGrid && xform.ParentUid == onGrid)
            {
                var here = xform.Coordinates;
                if (state.LastTiles.Count == 0 || !state.LastTiles[^1].InRange(EntityManager, _transform, here, 0.9f))
                {
                    state.LastTiles.Add(here);
                    if (state.LastTiles.Count > 4)
                        state.LastTiles.RemoveAt(0);
                }
            }

            if (now < state.NextTick)
                continue;

            state.NextTick = now + TimeSpan.FromSeconds(2);

            // on_life(): 2% — туманная рвота с падением.
            if (_random.Prob(0.02f))
            {
                Spawn("ImperialNebulaVomit", xform.Coordinates);
                _stun.TryKnockdown(uid, TimeSpan.FromSeconds(2), true);
                _popup.PopupEntity(Loc.GetString("imperial-voidwalker-vomit", ("user", uid)), uid, PopupType.MediumCaution);
            }

            if (state.PlanetAllergy && IsOnPlanet(uid))
                _damage.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { ["Blunt"] = 2 } }, true);
        }
    }
}
