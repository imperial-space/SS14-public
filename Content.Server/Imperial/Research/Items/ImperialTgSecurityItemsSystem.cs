using System.Linq;
using System.Numerics;
using Content.Server.Electrocution;
using Content.Server.Popups;
using Content.Server.Temperature.Systems;
using Content.Shared.Actions;
using Content.Shared.Contraband;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Ensnaring;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Research.Items;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Projectiles;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Research.Items;

/// <summary>
/// Предметы СБ из исследований TG: медвежий капкан, бумеранг, особые пули, перчатки для захвата,
/// N-spect сканер и ручка СБ.
/// </summary>
public sealed class ImperialTgSecurityItemsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedEnsnareableSystem _ensnare = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    private static readonly SoundSpecifier TrapSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");
    private static readonly SoundSpecifier TackleSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialBearTrapComponent, UseInHandEvent>(OnTrapUse);
        SubscribeLocalEvent<ImperialBearTrapComponent, ImperialBearTrapArmDoAfterEvent>(OnTrapArmed);
        SubscribeLocalEvent<ImperialBearTrapComponent, StepTriggerAttemptEvent>(OnTrapStepAttempt);
        SubscribeLocalEvent<ImperialBearTrapComponent, StepTriggeredOnEvent>(OnTrapStep);

        SubscribeLocalEvent<ImperialBoomerangComponent, LandEvent>(OnBoomerangLand);
        SubscribeLocalEvent<ImperialBoomerangComponent, ThrowDoHitEvent>(OnBoomerangHit);

        SubscribeLocalEvent<ImperialTgProjectileEffectsComponent, ProjectileHitEvent>(OnProjectileHit);

        SubscribeLocalEvent<ImperialTacklerComponent, ClothingGotEquippedEvent>(OnTacklerEquipped);
        SubscribeLocalEvent<ImperialTacklerComponent, ClothingGotUnequippedEvent>(OnTacklerUnequipped);
        SubscribeLocalEvent<ImperialTacklerComponent, ImperialTackleActionEvent>(OnTackle);
        SubscribeLocalEvent<ImperialTacklingComponent, ThrowDoHitEvent>(OnTackleHit);
        SubscribeLocalEvent<ImperialTacklingComponent, LandEvent>(OnTackleLand);

        SubscribeLocalEvent<ImperialInspectorComponent, AfterInteractEvent>(OnInspectorInteract);
        SubscribeLocalEvent<ImperialInspectorComponent, ImperialInspectorDoAfterEvent>(OnInspectorDoAfter);

        SubscribeLocalEvent<ImperialSecurityPenComponent, GetVerbsEvent<UtilityVerb>>(OnPenVerbs);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialTgGlowComponent>();
        while (query.MoveNext(out var uid, out var glow))
        {
            if (now < glow.EndTime)
                continue;

            if (glow.AddedLight)
                RemComp<PointLightComponent>(uid);
            RemCompDeferred<ImperialTgGlowComponent>(uid);
        }
    }

    #region Медвежий капкан

    private void OnTrapUse(Entity<ImperialBearTrapComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Armed)
        {
            SetArmed(ent, false);
            _popup.PopupEntity(Loc.GetString("imperial-beartrap-disarmed"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.ArmTime,
            new ImperialBearTrapArmDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnTrapArmed(Entity<ImperialBearTrapComponent> ent, ref ImperialBearTrapArmDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        SetArmed(ent, true);
        _audio.PlayPvs(TrapSound, ent);
        _popup.PopupEntity(Loc.GetString("imperial-beartrap-armed"), ent, args.User);
    }

    private void SetArmed(Entity<ImperialBearTrapComponent> ent, bool armed)
    {
        ent.Comp.Armed = armed;
        Dirty(ent);
        _appearance.SetData(ent, ImperialBearTrapVisuals.Armed, armed);
    }

    private void OnTrapStepAttempt(Entity<ImperialBearTrapComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue |= ent.Comp.Armed && !_container.IsEntityInContainer(ent);
    }

    private void OnTrapStep(Entity<ImperialBearTrapComponent> ent, ref StepTriggeredOnEvent args)
    {
        if (!ent.Comp.Armed || !HasComp<MobStateComponent>(args.Tripper))
            return;

        SetArmed(ent, false);
        _audio.PlayPvs(TrapSound, ent);

        var damage = new DamageSpecifier();
        damage.DamageDict["Piercing"] = ent.Comp.Damage;
        _damageable.TryChangeDamage(args.Tripper, damage, origin: ent);

        if (TryComp<EnsnaringComponent>(ent, out var ensnaring))
            _ensnare.TryEnsnare(args.Tripper, ent, ensnaring);

        _popup.PopupEntity(Loc.GetString("imperial-beartrap-triggered", ("target", args.Tripper)), args.Tripper, Robust.Shared.Player.Filter.Pvs(args.Tripper), true, Content.Shared.Popups.PopupType.MediumCaution);
    }

    #endregion

    #region Бумеранг

    private void OnBoomerangHit(Entity<ImperialBoomerangComponent> ent, ref ThrowDoHitEvent args)
    {
        Return(ent, args.Component.Thrower);
    }

    private void OnBoomerangLand(Entity<ImperialBoomerangComponent> ent, ref LandEvent args)
    {
        Return(ent, args.User);
    }

    private void Return(Entity<ImperialBoomerangComponent> ent, EntityUid? thrower)
    {
        if (ent.Comp.Returning)
        {
            ent.Comp.Returning = false;
            if (thrower != null && _hands.IsHolding(thrower.Value, null) == false)
                _hands.TryPickupAnyHand(thrower.Value, ent);
            return;
        }

        if (thrower == null || TerminatingOrDeleted(thrower))
            return;

        var from = _transform.GetMapCoordinates(ent);
        var to = _transform.GetMapCoordinates(thrower.Value);
        if (from.MapId != to.MapId || (from.Position - to.Position).Length() > ent.Comp.ReturnRange)
            return;

        ent.Comp.Returning = true;
        _throwing.TryThrow(ent, to.Position - from.Position, ent.Comp.ReturnSpeed, thrower, recoil: false);
    }

    #endregion

    #region Пули

    private void OnProjectileHit(Entity<ImperialTgProjectileEffectsComponent> ent, ref ProjectileHitEvent args)
    {
        var target = args.Target;
        var comp = ent.Comp;

        if (comp.SpawnOnHit is { } spawn)
            Spawn(spawn, _transform.GetMapCoordinates(target));

        if (!HasComp<MobStateComponent>(target))
            return;

        if (comp.BodyTemperature is { } temp)
            _temperature.ForceChangeTemperature(target, temp);

        if (comp.Implant is { } implant && !HasImplant(target, implant))
            _implants.AddImplant(target, implant);

        if (comp.Shock > 0)
            _electrocution.TryDoElectrocution(target, ent, comp.Shock, TimeSpan.FromSeconds(1), true, ignoreInsulation: true);

        if (comp.GlowTime > 0)
        {
            var glow = EnsureComp<ImperialTgGlowComponent>(target);
            glow.EndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.GlowTime);
            if (!HasComp<PointLightComponent>(target))
            {
                var light = _light.EnsureLight(target);
                _light.SetColor(target, comp.GlowColor, light);
                _light.SetRadius(target, 2.5f, light);
                _light.SetEnergy(target, 2f, light);
                glow.AddedLight = true;
            }
        }

        if (comp.Knockback > 0 && args.Shooter is { } shooter)
        {
            var dir = _transform.GetMapCoordinates(target).Position - _transform.GetMapCoordinates(shooter).Position;
            if (dir.LengthSquared() > 0.01f)
                _throwing.TryThrow(target, Vector2.Normalize(dir) * comp.Knockback, 10f, shooter, recoil: false);
        }
    }

    private bool HasImplant(EntityUid target, string proto)
    {
        if (!_container.TryGetContainer(target, ImplanterComponent.ImplantSlotId, out var container))
            return false;

        return container.ContainedEntities.Any(e => MetaData(e).EntityPrototype?.ID == proto);
    }

    #endregion

    #region Перчатки для захвата

    private void OnTacklerEquipped(Entity<ImperialTacklerComponent> ent, ref ClothingGotEquippedEvent args)
    {
        _actions.AddAction(args.Wearer, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }

    private void OnTacklerUnequipped(Entity<ImperialTacklerComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        _actions.RemoveAction(args.Wearer, ent.Comp.ActionEntity);
    }

    private void OnTackle(Entity<ImperialTacklerComponent> ent, ref ImperialTackleActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        var from = _transform.GetMapCoordinates(user);
        var to = _transform.ToMapCoordinates(args.Target);
        if (from.MapId != to.MapId)
            return;

        var dir = to.Position - from.Position;
        if (dir.LengthSquared() < 0.01f)
            return;

        if (dir.Length() > ent.Comp.Range)
            dir = Vector2.Normalize(dir) * ent.Comp.Range;

        args.Handled = true;

        _stamina.TakeStaminaDamage(user, ent.Comp.StaminaCost, visual: false);
        EnsureComp<ImperialTacklingComponent>(user).Gloves = ent;
        _popup.PopupEntity(Loc.GetString("imperial-tackle-leap", ("user", user)), user, Robust.Shared.Player.Filter.Pvs(user), true);
        _throwing.TryThrow(user, dir, ent.Comp.Speed, user, recoil: false, animated: false, doSpin: false);
    }

    private void OnTackleHit(Entity<ImperialTacklingComponent> ent, ref ThrowDoHitEvent args)
    {
        var user = ent.Owner;
        var target = args.Target;
        if (!TryComp<ImperialTacklerComponent>(ent.Comp.Gloves, out var gloves))
        {
            RemCompDeferred<ImperialTacklingComponent>(user);
            return;
        }

        RemCompDeferred<ImperialTacklingComponent>(user);

        if (!HasComp<MobStateComponent>(target))
        {
            Crash(user, gloves);
            return;
        }

        _audio.PlayPvs(TackleSound, user);

        // Упрощённая таблица roll из tackle.dm: чем выше skill_mod, тем удачнее.
        var roll = _random.Next(-3, 4) + gloves.SkillMod;
        var knock = TimeSpan.FromSeconds(gloves.Knockdown);
        switch (roll)
        {
            case <= -2:
                _stun.TryKnockdown(user, knock * 3);
                _popup.PopupEntity(Loc.GetString("imperial-tackle-fail", ("user", user), ("target", target)), user, Robust.Shared.Player.Filter.Pvs(user), true);
                break;
            case <= 0:
                _stun.TryKnockdown(user, knock * 2);
                _stun.TryKnockdown(target, knock);
                _popup.PopupEntity(Loc.GetString("imperial-tackle-both", ("user", user), ("target", target)), user, Robust.Shared.Player.Filter.Pvs(user), true);
                break;
            case <= 2:
                _stun.TryKnockdown(user, knock);
                _stun.TryKnockdown(target, knock * 2);
                _popup.PopupEntity(Loc.GetString("imperial-tackle-success", ("user", user), ("target", target)), user, Robust.Shared.Player.Filter.Pvs(user), true);
                break;
            default:
                _stun.TryKnockdown(target, knock * 3);
                _popup.PopupEntity(Loc.GetString("imperial-tackle-perfect", ("user", user), ("target", target)), user, Robust.Shared.Player.Filter.Pvs(user), true);
                break;
        }
    }

    private void OnTackleLand(Entity<ImperialTacklingComponent> ent, ref LandEvent args)
    {
        RemCompDeferred<ImperialTacklingComponent>(ent);
    }

    private void Crash(EntityUid user, ImperialTacklerComponent gloves)
    {
        if (gloves.CrashChance <= 0 || !_random.Prob(gloves.CrashChance))
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = 15;
        _damageable.TryChangeDamage(user, damage);
        _stun.TryUpdateParalyzeDuration(user, TimeSpan.FromSeconds(3));
        _popup.PopupEntity(Loc.GetString("imperial-tackle-crash", ("user", user)), user, Robust.Shared.Player.Filter.Pvs(user), true, Content.Shared.Popups.PopupType.MediumCaution);
    }

    #endregion

    #region N-spect сканер

    private void OnInspectorInteract(Entity<ImperialInspectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new ImperialInspectorDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnInspectorDoAfter(Entity<ImperialInspectorComponent> ent, ref ImperialInspectorDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        var found = new List<string>();
        foreach (var item in EnumerateContents(target))
        {
            if (TryComp<ContrabandComponent>(item, out var contraband) && contraband.Severity != "Minor")
                found.Add(Loc.GetString("imperial-inspector-entry", ("item", item)));
        }

        var report = Spawn(ent.Comp.Report, _transform.GetMapCoordinates(args.User));
        var text = Loc.GetString("imperial-inspector-report-header", ("target", target)) + "\n" +
                   (found.Count == 0
                       ? Loc.GetString("imperial-inspector-report-clean")
                       : string.Join("\n", found));
        if (TryComp<PaperComponent>(report, out var paper))
            _paper.SetContent((report, paper), text);
        _hands.TryPickupAnyHand(args.User, report);

        _popup.PopupEntity(Loc.GetString(found.Count == 0 ? "imperial-inspector-clean" : "imperial-inspector-found", ("count", found.Count)), args.User, args.User);
    }

    private IEnumerable<EntityUid> EnumerateContents(EntityUid uid, int depth = 0)
    {
        if (depth > 4 || !TryComp<ContainerManagerComponent>(uid, out var manager))
            yield break;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var child in container.ContainedEntities)
            {
                yield return child;
                foreach (var inner in EnumerateContents(child, depth + 1))
                    yield return inner;
            }
        }
    }

    #endregion

    #region Ручка СБ

    private void OnPenVerbs(Entity<ImperialSecurityPenComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<MobStateComponent>(args.Target))
            return;

        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("imperial-security-pen-verb"),
            Act = () =>
            {
                if (_timing.CurTime < ent.Comp.NextUse)
                {
                    _popup.PopupEntity(Loc.GetString("imperial-security-pen-cooldown"), user, user);
                    return;
                }

                ent.Comp.NextUse = _timing.CurTime + ent.Comp.Cooldown;
                var holo = Spawn(ent.Comp.Hologram, Transform(target).Coordinates);
                _transform.SetParent(holo, target);
                _popup.PopupEntity(Loc.GetString("imperial-security-pen-halt", ("target", target)), target, Robust.Shared.Player.Filter.Pvs(target), true, Content.Shared.Popups.PopupType.LargeCaution);
            },
        });
    }

    #endregion
}
