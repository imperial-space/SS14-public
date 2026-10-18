using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Drunk;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Uplink.Paradise;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Trigger;
using Content.Shared.Wieldable.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Uplink.Paradise;

/// <summary>
/// Позиции аплинка Paradise, которых нет в TG: Mind Batterer, граната всплывающей турели, гаррота.
/// </summary>
public sealed class UplinkParadiseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly Content.Shared.StatusEffect.StatusEffectsSystem _oldStatus = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly SoundSpecifier InterferenceSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/Paradise/interference.ogg");
    private static readonly SoundSpecifier GarroteSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/Paradise/cablecuff.ogg");

    private static readonly EntProtoId BlurryVision = "StatusEffectBlurryVision";
    private static readonly EntProtoId TemporaryBlindness = "StatusEffectTemporaryBlindness";
    private static readonly EntProtoId Delusion = "StatusEffectSeeingRainbow";

    private static readonly TimeSpan PacifyTime = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkMindBattererComponent, UseInHandEvent>(OnBattererUse);
        SubscribeLocalEvent<UplinkMindBattererComponent, UplinkBattererDoAfterEvent>(OnBattererDoAfter);
        SubscribeLocalEvent<UplinkMindBattererComponent, LandEvent>(OnBattererLand);
        SubscribeLocalEvent<UplinkMindBattererComponent, EmpPulseEvent>(OnBattererEmp);
        SubscribeLocalEvent<UplinkMindBattererComponent, ExaminedEvent>(OnBattererExamined);

        SubscribeLocalEvent<UplinkPopupTurretGrenadeComponent, UseInHandEvent>(OnTurretGrenadeUse);
        SubscribeLocalEvent<UplinkPopupTurretGrenadeComponent, TriggerEvent>(OnTurretGrenadeTrigger);

        SubscribeLocalEvent<UplinkGarroteComponent, AfterInteractEvent>(OnGarroteAfterInteract);
        SubscribeLocalEvent<UplinkGarroteComponent, ExaminedEvent>(OnGarroteExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var batterers = EntityQueryEnumerator<UplinkMindBattererComponent>();
        while (batterers.MoveNext(out var uid, out var batterer))
        {
            // process(): одна зарядка примерно за 20 секунд.
            if (batterer.TimesUsed <= 0 || now < batterer.NextRecharge)
                continue;

            batterer.TimesUsed--;
            batterer.NextRecharge = now + batterer.RechargeTime;
            _appearance.SetData(uid, UplinkBattererVisuals.Burnt, false);
        }

        var pacified = EntityQueryEnumerator<UplinkBattererPacifiedComponent>();
        while (pacified.MoveNext(out var uid, out var comp))
        {
            if (now < comp.EndTime)
                continue;

            RemComp<PacifiedComponent>(uid);
            RemComp<UplinkBattererPacifiedComponent>(uid);
        }

        var garrotes = EntityQueryEnumerator<UplinkGarroteComponent>();
        while (garrotes.MoveNext(out var uid, out var garrote))
        {
            if (garrote.Strangling is { } target)
                ProcessGarrote((uid, garrote), target, now);
        }
    }

    #region Mind Batterer

    private void OnBattererExamined(Entity<UplinkMindBattererComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-batterer-examine-warning"));
        args.PushMarkup(ent.Comp.TimesUsed >= ent.Comp.MaxUses
            ? Loc.GetString("uplink-batterer-examine-empty")
            : Loc.GetString("uplink-batterer-examine-charges", ("charges", ent.Comp.MaxUses - ent.Comp.TimesUsed)));
    }

    private void OnBattererUse(Entity<UplinkMindBattererComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.TimesUsed >= ent.Comp.MaxUses)
        {
            _popup.PopupEntity(Loc.GetString("uplink-batterer-burnt"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2), new UplinkBattererDoAfterEvent(), ent, used: ent)
        {
            Hidden = true,
            BreakOnMove = false,
            NeedHand = true,
        });
    }

    private void OnBattererDoAfter(Entity<UplinkMindBattererComponent> ent, ref UplinkBattererDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("uplink-batterer-cancel"), args.User, args.User);
            return;
        }

        args.Handled = true;
        ActivateBatterer(ent, args.User);
    }

    /// <summary>throw_impact(): брошенный батерер срабатывает сам, не чаще раза в 3 секунды.</summary>
    private void OnBattererLand(Entity<UplinkMindBattererComponent> ent, ref LandEvent args)
    {
        var now = _timing.CurTime;
        if (ent.Comp.TimesUsed >= ent.Comp.MaxUses || now < ent.Comp.ThrowCooldownUntil)
            return;

        ent.Comp.ThrowCooldownUntil = now + TimeSpan.FromSeconds(3);
        _popup.PopupEntity(Loc.GetString("uplink-batterer-thrown"), ent);
        ActivateBatterer(ent, null);
    }

    /// <summary>emp_act(): батерер разряжается последним мощным импульсом.</summary>
    private void OnBattererEmp(Entity<UplinkMindBattererComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.TimesUsed >= ent.Comp.MaxUses)
            return;

        args.Affected = true;
        _popup.PopupEntity(Loc.GetString("uplink-batterer-emp"), ent);
        ent.Comp.TimesUsed = ent.Comp.MaxUses - 1;
        ActivateBatterer(ent, null);
    }

    private void ActivateBatterer(Entity<UplinkMindBattererComponent> ent, EntityUid? user)
    {
        if (ent.Comp.TimesUsed <= 0)
            ent.Comp.NextRecharge = _timing.CurTime + ent.Comp.RechargeTime;

        ent.Comp.TimesUsed++;
        if (user != null)
            _popup.PopupEntity(Loc.GetString("uplink-batterer-triggered", ("charges", ent.Comp.MaxUses - ent.Comp.TimesUsed)), user.Value, user.Value);

        var coords = _transform.GetMapCoordinates(ent);
        foreach (var victim in _lookup.GetEntitiesInRange<ActorComponent>(coords, ent.Comp.Range))
        {
            if (victim.Owner == user || !HasComp<MobStateComponent>(victim) || _mobState.IsDead(victim))
                continue;

            BatterMind(victim);
        }

        _audio.PlayPvs(InterferenceSound, ent);
        if (ent.Comp.TimesUsed >= ent.Comp.MaxUses)
            _appearance.SetData(ent, UplinkBattererVisuals.Burnt, true);
    }

    private void BatterMind(EntityUid victim)
    {
        // Киборгов вспышка отправляет в перезагрузку, остальных — в замешательство.
        if (HasComp<BorgChassisComponent>(victim))
            _stun.TryAddStunDuration(victim, TimeSpan.FromSeconds(6));
        else
            _drunk.TryApplyDrunkenness(victim, TimeSpan.FromSeconds(45));

        string message;
        switch (_random.Next(1, 11))
        {
            case 1:
                _movementMod.TryAddMovementSpeedModDuration(victim, MovementModStatusSystem.FlashSlowdown, TimeSpan.FromSeconds(7), 0f);
                message = "uplink-batterer-effect-legs";
                break;
            case 2:
                if (!HasComp<PacifiedComponent>(victim))
                {
                    EnsureComp<PacifiedComponent>(victim);
                    EnsureComp<UplinkBattererPacifiedComponent>(victim).EndTime = _timing.CurTime + PacifyTime;
                }
                else if (TryComp<UplinkBattererPacifiedComponent>(victim, out var pacified))
                {
                    pacified.EndTime = _timing.CurTime + PacifyTime;
                }

                message = "uplink-batterer-effect-love";
                break;
            case 3:
                _status.TryAddStatusEffectDuration(victim, Delusion, TimeSpan.FromSeconds(30));
                message = "uplink-batterer-effect-delusion";
                break;
            case 4:
                if (_random.Prob(0.8f))
                {
                    _status.TryAddStatusEffectDuration(victim, BlurryVision, TimeSpan.FromSeconds(25));
                    message = "uplink-batterer-effect-blurry";
                }
                else
                {
                    _status.TryAddStatusEffectDuration(victim, TemporaryBlindness, TimeSpan.FromSeconds(15));
                    message = "uplink-batterer-effect-blind";
                }

                break;
            case 5:
                _stamina.TakeStaminaDamage(victim, 40f);
                message = "uplink-batterer-effect-tired";
                break;
            default:
                message = "uplink-batterer-effect-fade";
                break;
        }

        _popup.PopupEntity(Loc.GetString("uplink-batterer-jolt") + " " + Loc.GetString(message), victim, victim, PopupType.MediumCaution);
    }

    #endregion

    #region Всплывающая турель

    private void OnTurretGrenadeUse(Entity<UplinkPopupTurretGrenadeComponent> ent, ref UseInHandEvent args)
    {
        // activate_self(): запоминаем того, кто выдернул чеку. Сам таймер запускает ванильная граната.
        ent.Comp.Owner = args.User;
    }

    /// <summary>prime(): надувная турель стреляет во всех, кроме владельца.</summary>
    private void OnTurretGrenadeTrigger(Entity<UplinkPopupTurretGrenadeComponent> ent, ref TriggerEvent args)
    {
        var turret = Spawn(ent.Comp.Turret, _transform.GetMapCoordinates(ent));
        _transform.AnchorEntity(turret);

        if (ent.Comp.Owner is { } owner && !TerminatingOrDeleted(owner))
        {
            var exception = EnsureComp<FactionExceptionComponent>(turret);
            _faction.IgnoreEntity((turret, exception), owner);
        }

        QueueDel(ent);
    }

    #endregion

    #region Гаррота

    private void OnGarroteExamined(Entity<UplinkGarroteComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-garrote-examine"));
    }

    /// <summary>interact_with_atom(): накинуть проволоку на шею цели со спины.</summary>
    private void OnGarroteAfterInteract(Entity<UplinkGarroteComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;
        var user = args.User;
        var now = _timing.CurTime;

        if (now < ent.Comp.CooldownUntil)
            return;

        if (!HasComp<HumanoidProfileComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("uplink-garrote-no-dexterity"), user, user);
            return;
        }

        if (!TryComp<WieldableComponent>(ent, out var wield) || !wield.Wielded)
        {
            _popup.PopupEntity(Loc.GetString("uplink-garrote-need-wield", ("target", target)), user, user);
            return;
        }

        if (!HasComp<HumanoidProfileComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("uplink-garrote-not-human", ("target", target)), user, user);
            return;
        }

        if (target == user)
            return;

        var incapacitated = !_mobState.IsAlive(target) || _standing.IsDown(target);
        var targetDir = _transform.GetWorldRotation(target).GetCardinalDir();
        var userDir = _transform.GetWorldRotation(user).GetCardinalDir();
        if (targetDir != userDir && !incapacitated)
        {
            _popup.PopupEntity(Loc.GetString("uplink-garrote-wrong-angle", ("target", target)), user, user);
            return;
        }

        if (ent.Comp.Strangling != null)
        {
            _popup.PopupEntity(Loc.GetString("uplink-garrote-busy"), user, user);
            return;
        }

        ent.Comp.Strangling = target;
        ent.Comp.CooldownUntil = now + TimeSpan.FromSeconds(1);
        ent.Comp.NextTick = now + ent.Comp.TickTime;
        EnsureComp<UplinkGarrotedComponent>(target).Garrote = ent;
        _oldStatus.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromSeconds(2), true);
        _appearance.SetData(ent, UplinkGarroteVisuals.Strangling, true);
        _audio.PlayPvs(GarroteSound, ent, AudioParams.Default.WithVolume(-6f));

        _popup.PopupEntity(Loc.GetString("uplink-garrote-start-others", ("user", user), ("target", target)), target, Filter.PvsExcept(target).RemovePlayerByAttachedEntity(user), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("uplink-garrote-start-target", ("user", user)), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("uplink-garrote-start-user", ("target", target)), user, user);
    }

    /// <summary>process(): удушье, пока душитель держит гарроту двумя руками рядом с жертвой.</summary>
    private void ProcessGarrote(Entity<UplinkGarroteComponent> ent, EntityUid target, TimeSpan now)
    {
        var holder = Transform(ent).ParentUid;
        var holding = holder.IsValid()
                      && _hands.IsHolding(holder, ent)
                      && TryComp<WieldableComponent>(ent, out var wield) && wield.Wielded;

        if (TerminatingOrDeleted(target)
            || !holding
            || !_transform.InRange(Transform(holder).Coordinates, Transform(target).Coordinates, 1.5f))
        {
            if (holding && !TerminatingOrDeleted(target))
                _popup.PopupEntity(Loc.GetString("uplink-garrote-lost-grip", ("user", holder), ("target", target)), holder, PopupType.Medium);

            StopGarrote(ent);
            return;
        }

        if (now < ent.Comp.NextTick)
            return;

        ent.Comp.NextTick = now + ent.Comp.TickTime;
        _oldStatus.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromSeconds(6), true);

        var damage = new DamageSpecifier();
        damage.DamageDict["Asphyxiation"] = ent.Comp.OxyDamage;
        _damageable.TryChangeDamage(target, damage, ignoreResistances: true, origin: holder);
    }

    private void StopGarrote(Entity<UplinkGarroteComponent> ent)
    {
        if (ent.Comp.Strangling is { } target && TryComp<UplinkGarrotedComponent>(target, out var garroted) && garroted.Garrote == ent.Owner)
            RemComp<UplinkGarrotedComponent>(target);

        ent.Comp.Strangling = null;
        _appearance.SetData(ent, UplinkGarroteVisuals.Strangling, false);
    }

    #endregion
}
