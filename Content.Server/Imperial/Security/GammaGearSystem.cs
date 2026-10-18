using System.Linq;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Security;

/// <summary>gun/medbeam: медицинский луч, лечащий цель, пока та в 8 клетках и на виду.</summary>
[RegisterComponent]
public sealed partial class MedbeamComponent : Component
{
    /// <summary>max_range.</summary>
    [DataField]
    public float MaxRange = 8f;

    /// <summary>check_delay: 1 секунда.</summary>
    [DataField]
    public TimeSpan CheckDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public DamageSpecifier Heal = new()
    {
        DamageDict = { ["Blunt"] = -1.5f, ["Slash"] = -1.5f, ["Piercing"] = -1f, ["Heat"] = -1.5f, ["Cold"] = -1f, ["Shock"] = -1.5f },
    };

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public EntityUid? User;

    [ViewVariables]
    public TimeSpan NextCheck;

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Machines/scanning.ogg");

    [DataField]
    public SoundSpecifier LoopSound = new SoundPathSpecifier("/Audio/Ambience/Objects/emf_buzz.ogg");

    [DataField]
    public SoundSpecifier LostSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    /// <summary>Гул, пока луч держится.</summary>
    [ViewVariables]
    public EntityUid? LoopStream;
}

/// <summary>mod/module/energy_shield/gamma: три заряда, гасящих любую атаку.</summary>
[RegisterComponent]
public sealed partial class GammaEnergyShieldComponent : Component
{
    [DataField]
    public int MaxCharges = 3;

    /// <summary>shield_icon: «shield» — гамма, «shield-red» — Синдикат.</summary>
    [DataField]
    public string ShieldState = "shield";

    [ViewVariables]
    public int Charges = 3;

    /// <summary>recharge_start_delay.</summary>
    [DataField]
    public TimeSpan RechargeStartDelay = TimeSpan.FromSeconds(20);

    /// <summary>charge_increment_delay.</summary>
    [DataField]
    public TimeSpan ChargeIncrementDelay = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextRecharge;

    [ViewVariables]
    public EntityUid? Wearer;
}

/// <summary>Медлуч и энергощит гамма-РИГа из гамма-оружейки ParadiseSS13.</summary>
public sealed class GammaGearSystem : EntitySystem
{
    private static readonly SpriteSpecifier.Rsi BeamSprite =
        new(new ResPath("Imperial/Security/Gamma/medbeam_beam.rsi"), "beam");

    private static readonly SoundSpecifier ShieldSound = new SoundPathSpecifier("/Audio/Effects/sparks4.ogg");

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedbeamComponent, AfterInteractEvent>(OnMedbeamAfterInteract);
        SubscribeLocalEvent<MedbeamComponent, GotUnequippedHandEvent>(OnMedbeamUnequipped);
        SubscribeLocalEvent<MedbeamComponent, GotEquippedHandEvent>(OnMedbeamEquipped);

        SubscribeLocalEvent<GammaEnergyShieldComponent, GotEquippedEvent>(OnShieldEquipped);
        SubscribeLocalEvent<GammaEnergyShieldComponent, GotUnequippedEvent>(OnShieldUnequipped);
        SubscribeLocalEvent<GammaEnergyShieldComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnShieldDamage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var beams = EntityQueryEnumerator<MedbeamComponent>();
        while (beams.MoveNext(out var uid, out var beam))
        {
            if (beam.Target == null || now < beam.NextCheck)
                continue;

            beam.NextCheck = now + beam.CheckDelay;
            TickBeam((uid, beam));
        }

        var shields = EntityQueryEnumerator<GammaEnergyShieldComponent>();
        while (shields.MoveNext(out var uid, out var shield))
        {
            if (shield.Charges >= shield.MaxCharges || now < shield.NextRecharge)
                continue;

            shield.Charges++;
            shield.NextRecharge = now + shield.ChargeIncrementDelay;
            UpdateShieldVisuals((uid, shield));
        }
    }

    #region Медлуч

    /// <summary>process_fire(): навести луч на живую цель.</summary>
    private void OnMedbeamAfterInteract(Entity<MedbeamComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || target == args.User || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;
        LoseTarget(ent);
        if (!InBeamRange(args.User, target, ent.Comp.MaxRange))
            return;

        ent.Comp.Target = target;
        ent.Comp.User = args.User;
        ent.Comp.NextCheck = _timing.CurTime + ent.Comp.CheckDelay;

        _audio.PlayPvs(ent.Comp.StartSound, ent, AudioParams.Default.WithVolume(-4f));
        ent.Comp.LoopStream = _audio.PlayPvs(ent.Comp.LoopSound, ent, AudioParams.Default.WithLoop(true).WithVolume(-8f))?.Entity;

        var visuals = EnsureComp<JointVisualsComponent>(ent);
        visuals.Sprite = BeamSprite;
        visuals.Target = target;
        Dirty(ent, visuals);
    }

    private void OnMedbeamUnequipped(Entity<MedbeamComponent> ent, ref GotUnequippedHandEvent args)
    {
        LoseTarget(ent);
    }

    private void OnMedbeamEquipped(Entity<MedbeamComponent> ent, ref GotEquippedHandEvent args)
    {
        LoseTarget(ent);
    }

    private void LoseTarget(Entity<MedbeamComponent> ent)
    {
        ent.Comp.LoopStream = _audio.Stop(ent.Comp.LoopStream);
        ent.Comp.Target = null;
        ent.Comp.User = null;
        RemComp<JointVisualsComponent>(ent);
    }

    private bool InBeamRange(EntityUid user, EntityUid target, float range)
    {
        return _interaction.InRangeUnobstructed(user, target, range, CollisionGroup.Opaque);
    }

    /// <summary>process() + on_beam_tick(): проверка дистанции и видимости, лечение, пересечение лучей.</summary>
    private void TickBeam(Entity<MedbeamComponent> ent)
    {
        if (ent.Comp.Target is not { } target || ent.Comp.User is not { } user ||
            TerminatingOrDeleted(target) || TerminatingOrDeleted(user) || !InBeamRange(user, target, ent.Comp.MaxRange))
        {
            if (ent.Comp.User is { } holder && !TerminatingOrDeleted(holder))
            {
                _popup.PopupEntity(Loc.GetString("medbeam-lost"), holder, holder, PopupType.SmallCaution);
                _audio.PlayPvs(ent.Comp.LostSound, holder, AudioParams.Default.WithVolume(-4f));
            }

            LoseTarget(ent);
            return;
        }

        // Don't cross the beams!
        if (FindCrossing(ent) is { } crossing)
        {
            var (other, point) = crossing;
            var mapCoords = new MapCoordinates(point, _xform.GetMapId(user));
            _popup.PopupCoordinates(Loc.GetString("medbeam-crossed"), _xform.ToCoordinates(mapCoords), PopupType.LargeCaution);
            _explosion.QueueExplosion(mapCoords, "Default", 40f, 4f, 10f, user);
            LoseTarget(ent);
            LoseTarget(other);
            return;
        }

        _damageable.TryChangeDamage(target, ent.Comp.Heal, true, origin: user);
    }

    private (Entity<MedbeamComponent>, Vector2)? FindCrossing(Entity<MedbeamComponent> ent)
    {
        var map = _xform.GetMapId(ent.Comp.User!.Value);
        var a1 = _xform.GetWorldPosition(ent.Comp.User.Value);
        var a2 = _xform.GetWorldPosition(ent.Comp.Target!.Value);

        var query = EntityQueryEnumerator<MedbeamComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == ent.Owner || other.Target is not { } otherTarget || other.User is not { } otherUser ||
                _xform.GetMapId(otherUser) != map || TerminatingOrDeleted(otherTarget))
            {
                continue;
            }

            var b1 = _xform.GetWorldPosition(otherUser);
            var b2 = _xform.GetWorldPosition(otherTarget);
            if (SegmentIntersection(a1, a2, b1, b2) is { } point)
                return ((uid, other), point);
        }

        return null;
    }

    private static Vector2? SegmentIntersection(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        var r = p2 - p1;
        var s = q2 - q1;
        var denom = r.X * s.Y - r.Y * s.X;
        if (MathF.Abs(denom) < 0.0001f)
            return null;

        var t = ((q1.X - p1.X) * s.Y - (q1.Y - p1.Y) * s.X) / denom;
        var u = ((q1.X - p1.X) * r.Y - (q1.Y - p1.Y) * r.X) / denom;
        if (t is <= 0.05f or >= 0.95f || u is <= 0.05f or >= 0.95f)
            return null;

        return p1 + r * t;
    }

    #endregion

    #region Энергощит

    private void OnShieldEquipped(Entity<GammaEnergyShieldComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & (SlotFlags.BACK | SlotFlags.OUTERCLOTHING)) == 0)
            return;

        ent.Comp.Wearer = args.EquipTarget;
        var wearerVisuals = EnsureComp<EnergyShieldWearerComponent>(args.EquipTarget);
        wearerVisuals.State = ent.Comp.ShieldState;
        Dirty(args.EquipTarget, wearerVisuals);
        UpdateShieldVisuals(ent);
    }

    private void OnShieldUnequipped(Entity<GammaEnergyShieldComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.Wearer != args.EquipTarget)
            return;

        ent.Comp.Wearer = null;
        RemComp<EnergyShieldWearerComponent>(args.EquipTarget);
    }

    /// <summary>shield_reaction(): заряд гасит атаку целиком.</summary>
    private void OnShieldDamage(Entity<GammaEnergyShieldComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        var ev = args.Args;
        if (ent.Comp.Charges <= 0 || ent.Comp.Wearer is not { } wearer || ev.Origin is not { } origin || origin == wearer)
            return;

        if (ev.Damage.GetTotal() <= 0)
            return;

        ev.Damage = new DamageSpecifier();
        ent.Comp.Charges--;
        ent.Comp.NextRecharge = _timing.CurTime + ent.Comp.RechargeStartDelay;

        _popup.PopupEntity(Loc.GetString("gamma-shield-block"), wearer, PopupType.Medium);
        _audio.PlayPvs(ShieldSound, wearer);
        Spawn("EffectSparks", Transform(wearer).Coordinates);
        UpdateShieldVisuals(ent);
    }

    private void UpdateShieldVisuals(Entity<GammaEnergyShieldComponent> ent)
    {
        if (ent.Comp.Wearer is not { } wearer || !TryComp<EnergyShieldWearerComponent>(wearer, out var visuals))
            return;

        var active = ent.Comp.Charges > 0;
        if (visuals.Active == active)
            return;

        visuals.Active = active;
        Dirty(wearer, visuals);
    }

    #endregion
}
