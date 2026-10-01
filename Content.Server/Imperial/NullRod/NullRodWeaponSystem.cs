using Content.Shared.Body.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.NullRod.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Server.Bible.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.NullRod;

/// <summary>
/// Уникальные механики отдельных форм святого оружия из SS13.
/// </summary>
public sealed class NullRodWeaponSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodRandomDamageComponent, MeleeHitEvent>(OnRandomDamageHit);
        SubscribeLocalEvent<NullRodNullbladeComponent, MeleeHitEvent>(OnNullbladeHit);
        SubscribeLocalEvent<NullRodChemicalTransferComponent, MeleeHitEvent>(OnChemicalTransferHit);
        SubscribeLocalEvent<NullRodSpiritHoldingComponent, UseInHandEvent>(OnSpiritUseInHand);
        SubscribeLocalEvent<NullRodFactionGranterComponent, UseInHandEvent>(OnFactionUseInHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Аритмичный нож: раз в интервал случайно меняет скорость держащего (process() в SS13).
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NullRodArrhythmicComponent>();
        while (query.MoveNext(out var uid, out var knife))
        {
            if (now < knife.NextChange)
                continue;

            knife.NextChange = now + knife.Interval;
            knife.SpeedModifier = _random.NextFloat(knife.MinSpeedModifier, knife.MaxSpeedModifier);
            Dirty(uid, knife);

            var holder = Transform(uid).ParentUid;
            if (_hands.IsHolding(holder, uid))
                _movement.RefreshMovementSpeedModifiers(holder);
        }
    }

    // ─── Иномерный клинок ────────────────────────────────────────────────────

    private void OnRandomDamageHit(Entity<NullRodRandomDamageComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var multiplier = _random.NextFloat(ent.Comp.MinMultiplier, ent.Comp.MaxMultiplier);
        args.ModifiersList.Add(ScaleAll(args.BaseDamage, multiplier));
    }

    // ─── Нулевой клинок ──────────────────────────────────────────────────────

    private void OnNullbladeHit(Entity<NullRodNullbladeComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        // Урон удара — 1d6 + сила вместо фиксированного.
        var rolled = RollDice(1, 6) + ent.Comp.Strength;
        args.ModifiersList.Add(ScaleAll(args.BaseDamage, rolled / ent.Comp.BaseDamage.Float()));

        foreach (var target in args.HitEntities)
        {
            if (target == args.User || !HasComp<MobStateComponent>(target) || _mobState.IsDead(target))
                continue;

            TrySneakAttack(ent, args.User, target);
        }
    }

    /// <summary>
    /// Скрытая атака: по ослеплённой, связанной, оглушённой цели или со спины — дополнительные 3d6.
    /// </summary>
    private void TrySneakAttack(Entity<NullRodNullbladeComponent> blade, EntityUid user, EntityUid target)
    {
        var vulnerable = IsBlind(target)
            || TryComp<CuffableComponent>(target, out var cuffs) && cuffs.CuffedHandCount > 0
            || HasComp<StunnedComponent>(target)
            || IsBehind(user, target);

        if (!vulnerable)
            return;

        // Вслепую точно ударить нельзя.
        if (IsBlind(user))
        {
            _popup.PopupEntity(Loc.GetString("null-rod-sneak-attack-avoided"), target, user);
            return;
        }

        var dice = RollDice(3, 6);

        // Добить беззащитного: без сознания — ещё 1d6.
        if (_mobState.IsCritical(target) || HasComp<SleepingComponent>(target))
            dice += RollDice(1, 6);

        var damage = new DamageSpecifier();
        damage.DamageDict[blade.Comp.SneakDamageType] = FixedPoint2.New(dice);
        _damageable.TryChangeDamage(target, damage, origin: user);

        _popup.PopupEntity(Loc.GetString("null-rod-sneak-attack"), target, user, PopupType.MediumCaution);
        _audio.PlayPvs(blade.Comp.SneakSound, target);
    }

    private bool IsBlind(EntityUid uid)
    {
        return TryComp<BlindableComponent>(uid, out var blindable) && blindable.IsBlind;
    }

    private bool IsBehind(EntityUid user, EntityUid target)
    {
        var toUser = _xform.GetWorldPosition(user) - _xform.GetWorldPosition(target);
        if (toUser.LengthSquared() < 0.001f)
            return false;

        var angleDiff = (Angle.FromWorldVec(toUser) - _xform.GetWorldRotation(target)).Reduced().FlipPositive();
        return angleDiff > Math.PI / 2 && angleDiff < 3 * Math.PI / 2;
    }

    // ─── Молот гордыни ───────────────────────────────────────────────────────

    private void OnChemicalTransferHit(Entity<NullRodChemicalTransferComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !_random.Prob(ent.Comp.Chance))
            return;

        foreach (var target in args.HitEntities)
        {
            if (target == args.User)
                continue;

            var chemicals = _bloodstream.FlushChemicals(args.User, FixedPoint2.MaxValue);
            if (chemicals == null)
                return;

            _bloodstream.TryAddToBloodstream(target, chemicals);
            _popup.PopupEntity(Loc.GetString("null-rod-pride-transfer-user", ("target", target)), args.User, args.User);
            _popup.PopupEntity(Loc.GetString("null-rod-pride-transfer-target", ("user", args.User)), target, target, PopupType.MediumCaution);
            return;
        }
    }

    // ─── Одержимый клинок ────────────────────────────────────────────────────

    private void OnSpiritUseInHand(Entity<NullRodSpiritHoldingComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Awakening)
        {
            _popup.PopupEntity(Loc.GetString("null-rod-spirit-already"), ent, args.User);
            return;
        }

        ent.Comp.Awakening = true;
        Dirty(ent);
        EntityManager.AddComponents(ent, ent.Comp.AwakenComponents);
        _popup.PopupEntity(Loc.GetString("null-rod-spirit-channeling"), ent, args.User);
    }

    // ─── Плюшевый Карп-сие ───────────────────────────────────────────────────

    private void OnFactionUseInHand(Entity<NullRodFactionGranterComponent> ent, ref UseInHandEvent args)
    {
        // Handled не проверяем: родительская плюшка сама помечает использование (EmitSoundOnUse).
        args.Handled = true;
        if (ent.Comp.Used)
        {
            _popup.PopupEntity(Loc.GetString("null-rod-carp-used"), ent, args.User);
            return;
        }

        if (!HasComp<BibleUserComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("null-rod-carp-not-holy"), ent, args.User);
            return;
        }

        _faction.AddFaction(args.User, ent.Comp.Faction);
        ent.Comp.Used = true;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.GrantMessage), args.User, args.User, PopupType.Medium);
    }

    // ─── Вспомогательное ─────────────────────────────────────────────────────

    private int RollDice(int count, int sides)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            total += _random.Next(1, sides + 1);
        }

        return total;
    }

    private static DamageModifierSet ScaleAll(DamageSpecifier damage, float multiplier)
    {
        var set = new DamageModifierSet();
        foreach (var type in damage.DamageDict.Keys)
        {
            set.Coefficients[type] = multiplier;
        }

        return set;
    }
}
