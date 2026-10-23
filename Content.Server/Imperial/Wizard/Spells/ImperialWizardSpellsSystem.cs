using System.Numerics;
using Content.Server.Imperial.Antimagic;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>Эффект заклинания: поднимается на действии, обрабатывают компоненты эффекта.</summary>
[ByRefEvent]
public record struct ImperialSpellEffectEvent(EntityUid Caster, EntityUid? Target, EntityCoordinates? Coords)
{
    public bool Handled;
}

/// <summary>Рука заклинания коснулась цели: эффект касания (cast_on_hand_hit).</summary>
[ByRefEvent]
public record struct ImperialTouchHitEvent(EntityUid Caster, EntityUid Target, EntityUid Hand)
{
    /// <summary>true — заклинание сработало, рука исчезает, начинается откат.</summary>
    public bool Success;
}

/// <summary>/datum/action/cooldown/spell/pointed/projectile: снаряд в точку (несколько выстрелов за активацию).</summary>
[RegisterComponent]
public sealed partial class ImperialSpellProjectileComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>projectile_amount: сколько выстрелов за одну активацию.</summary>
    [DataField]
    public int Amount = 1;

    /// <summary>projectiles_per_fire.</summary>
    [DataField]
    public int PerFire = 1;

    /// <summary>Разброс снарядов одного выстрела, градусы.</summary>
    [DataField]
    public float Spread;

    [DataField]
    public float Speed = 20f;

    /// <summary>range: дальность в клетках (null — по прототипу).</summary>
    [DataField]
    public float? Range;

    /// <summary>Дальность растёт с уровнем (у огненного шара 6 + 2 × уровень).</summary>
    [DataField]
    public float RangePerLevel;

    /// <summary>Самонаведение на ближайшее существо у точки клика.</summary>
    [DataField]
    public bool Homing;

    [DataField]
    public float HomingTurnRate = 200f;

    [ViewVariables]
    public int ShotsLeft;
}

/// <summary>Самонаводящийся снаряд (homing у карт заклинаний и волшебной ракеты).</summary>
[RegisterComponent]
public sealed partial class ImperialHomingProjectileComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    /// <summary>Градусов в секунду.</summary>
    [DataField]
    public float TurnRate = 200f;
}

/// <summary>/datum/action/cooldown/spell/touch: заклинание вызывает руку, действующую по касанию.</summary>
[RegisterComponent]
public sealed partial class ImperialSpellTouchComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Hand;

    /// <summary>can_cast_on_self.</summary>
    [DataField]
    public bool CanCastOnSelf;

    /// <summary>Только живые цели (is_valid_target = isliving).</summary>
    [DataField]
    public bool LivingOnly = true;

    [ViewVariables]
    public EntityUid? AttachedHand;
}

/// <summary>
/// Заклинания мага: общие виды (снаряды, касания) и эффекты отдельных заклинаний по компонентам на действии.
/// </summary>
public sealed partial class ImperialWizardSpellsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ImperialSpellSystem _spells = default!;
    [Dependency] private readonly ImperialAntimagicSystem _antimagic = default!;
    [Dependency] private readonly SharedGunSystem _guns = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialSpellProjectileComponent, ImperialSpellEffectEvent>(OnProjectileSpell);
        SubscribeLocalEvent<ImperialSpellTouchComponent, ImperialSpellEffectEvent>(OnTouchSpell);
        SubscribeLocalEvent<ImperialSpellTouchComponent, ComponentShutdown>(OnTouchShutdown);
        SubscribeLocalEvent<ImperialTouchHandComponent, AfterInteractEvent>(OnHandInteract);
        SubscribeLocalEvent<ImperialTouchHandComponent, GotUnequippedHandEvent>(OnHandDropped);

        InitializeOffensive();
        InitializeDefensive();
        InitializeMobility();
        InitializeMagicGuns();
        InitializeAssistance();
    }

    /// <summary>can_block_magic() цели: с сообщением «заклинание не подействовало».</summary>
    public bool Blocked(EntityUid caster, EntityUid target, ImperialMagicResistance magic = ImperialMagicResistance.Magic)
    {
        if (target == caster || !_antimagic.CanBlockMagic(target, magic))
            return false;

        _popup.PopupEntity(Loc.GetString("imperial-spell-no-effect"), caster, caster, PopupType.SmallCaution);
        return true;
    }

    #region Снаряды

    private void OnProjectileSpell(Entity<ImperialSpellProjectileComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (args.Coords is not { } coords || !TryComp<ImperialSpellComponent>(ent, out var spell))
            return;

        if (ent.Comp.ShotsLeft <= 0)
            ent.Comp.ShotsLeft = ent.Comp.Amount;

        var from = _transform.GetMapCoordinates(args.Caster);
        var to = _transform.ToMapCoordinates(coords);
        if (from.MapId != to.MapId)
            return;

        var direction = to.Position - from.Position;
        if (direction.LengthSquared() < 0.01f)
            direction = _transform.GetWorldRotation(args.Caster).ToWorldVec();

        EntityUid? homingTarget = null;
        if (ent.Comp.Homing)
        {
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(to, 1.5f))
            {
                if (mob.Owner != args.Caster && !_mobs.IsDead(mob))
                {
                    homingTarget = mob;
                    break;
                }
            }
        }

        for (var i = 0; i < ent.Comp.PerFire; i++)
        {
            var angle = ent.Comp.PerFire > 1 || ent.Comp.Spread > 0
                ? Angle.FromDegrees(_random.NextFloat(-ent.Comp.Spread / 2, ent.Comp.Spread / 2))
                : Angle.Zero;
            var dir = angle.RotateVec(direction.Normalized());
            var projectile = Spawn(ent.Comp.Proto, from);
            _guns.ShootProjectile(projectile, dir, Vector2.Zero, ent, args.Caster, ent.Comp.Speed);

            // ready_projectile(): дальность — время жизни снаряда.
            if (ent.Comp.Range is { } range)
            {
                var tiles = range + ent.Comp.RangePerLevel * spell.Level;
                EnsureComp<TimedDespawnComponent>(projectile).Lifetime = tiles / ent.Comp.Speed;
            }

            if (homingTarget is { } target)
            {
                var homing = EnsureComp<ImperialHomingProjectileComponent>(projectile);
                homing.Target = target;
                homing.TurnRate = ent.Comp.HomingTurnRate;
            }
        }

        ent.Comp.ShotsLeft--;
        if (ent.Comp.ShotsLeft > 0)
            spell.SkipCooldown = true;

        args.Handled = true;
    }

    private void UpdateHoming(float frameTime)
    {
        var query = EntityQueryEnumerator<ImperialHomingProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var homing, out var physics, out var xform))
        {
            if (TerminatingOrDeleted(homing.Target))
                continue;

            var velocity = physics.LinearVelocity;
            var speed = velocity.Length();
            if (speed < 0.1f)
                continue;

            var want = _transform.GetWorldPosition(homing.Target) - _transform.GetWorldPosition(xform);
            if (want.LengthSquared() < 0.01f)
                continue;

            var current = Angle.FromWorldVec(velocity);
            var target = Angle.FromWorldVec(want);
            var diff = Angle.ShortestDistance(current, target);
            var max = Angle.FromDegrees(homing.TurnRate * frameTime);
            if (Math.Abs(diff.Theta) > max.Theta)
                diff = new Angle(Math.Sign(diff.Theta) * max.Theta);

            var newAngle = current + diff;
            _physics.SetLinearVelocity(uid, newAngle.ToWorldVec() * speed, body: physics);
            _transform.SetWorldRotation(uid, newAngle + Angle.FromDegrees(90));
        }
    }

    #endregion

    #region Касания

    /// <summary>cast(): вызвать руку или убрать её (откат возвращается).</summary>
    private void OnTouchSpell(Entity<ImperialSpellTouchComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (!TryComp<ImperialSpellComponent>(ent, out var spell))
            return;

        args.Handled = true;
        spell.SkipCooldown = true;
        spell.SkipFeedback = true;

        var caster = args.Caster;
        if (ent.Comp.AttachedHand is { } existing && !TerminatingOrDeleted(existing))
        {
            RemoveHand(ent, existing);
            _popup.PopupEntity(Loc.GetString("imperial-spell-touch-drop"), caster, caster);
            return;
        }

        var hand = Spawn(ent.Comp.Hand, Transform(caster).Coordinates);
        if (!_hands.TryPickupAnyHand(caster, hand))
        {
            QueueDel(hand);
            _popup.PopupEntity(Loc.GetString("imperial-spell-touch-hands-full"), caster, caster);
            return;
        }

        EnsureComp<ImperialTouchHandComponent>(hand).Spell = ent;
        ent.Comp.AttachedHand = hand;
        _popup.PopupEntity(Loc.GetString("imperial-spell-touch-draw"), caster, caster);
    }

    private void RemoveHand(Entity<ImperialSpellTouchComponent> spell, EntityUid hand)
    {
        spell.Comp.AttachedHand = null;
        if (TryComp<ImperialTouchHandComponent>(hand, out var comp))
            comp.Spell = null;
        QueueDel(hand);
    }

    private void OnTouchShutdown(Entity<ImperialSpellTouchComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.AttachedHand is { } hand)
            QueueDel(hand);
    }

    private void OnHandDropped(Entity<ImperialTouchHandComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.Spell is { } spell && TryComp<ImperialSpellTouchComponent>(spell, out var touch))
            touch.AttachedHand = null;
        QueueDel(ent);
    }

    /// <summary>on_hand_hit(): проверки, произнесение, эффект, откат.</summary>
    private void OnHandInteract(Entity<ImperialTouchHandComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || ent.Comp.Spell is not { } spellUid
            || !TryComp<ImperialSpellComponent>(spellUid, out var spell) || !TryComp<ImperialSpellTouchComponent>(spellUid, out var touch))
            return;

        var caster = args.User;
        if (!touch.CanCastOnSelf && target == caster || touch.LivingOnly && !HasComp<MobStateComponent>(target))
            return;

        if (!_spells.CanCast((spellUid, spell), caster, true))
            return;

        args.Handled = true;
        var hit = new ImperialTouchHitEvent(caster, target, ent);
        RaiseLocalEvent(spellUid, ref hit);
        if (!hit.Success)
            return;

        // spell_feedback() и откат — только после касания.
        _spells.Invoke((spellUid, spell), caster);
        if (spell.CastSound != null)
            _audio.PlayPvs(spell.CastSound, caster);
        _actions.SetCooldown(spellUid, _spells.GetCooldown(spell));
        RemoveHand((spellUid, touch), ent);
        var cast = new ImperialSpellCastEvent(caster, spellUid);
        RaiseLocalEvent(caster, ref cast);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateHoming(frameTime);
        UpdateOffensive();
        UpdateDefensive();
        UpdateMobility();
        UpdateMagicGuns();
    }
}
