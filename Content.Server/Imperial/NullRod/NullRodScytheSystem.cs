using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.NullRod.Components;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Jittering;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.NullRod;

/// <summary>
/// Зловещий осколок и порочная коса (chaplain_vorpal_scythe.dm из SS13).
/// Коса призывается из руки, насыщается ударами и «похоронным звоном» и ранит владельца,
/// если убрать её, не дав вкусить крови.
/// </summary>
public sealed class NullRodScytheSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier KnellSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    /// <summary>Отсечение головы в SS13 смертельно, в SS14 его заменяет смертельный режущий урон.</summary>
    private static readonly DamageSpecifier KnellDamage = new()
    {
        DamageDict = { ["Slash"] = 200 },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodScytheShardComponent, UseInHandEvent>(OnShardUse);
        SubscribeLocalEvent<NullRodScytheArmComponent, NullRodToggleScytheActionEvent>(OnToggleScythe);
        SubscribeLocalEvent<NullRodScytheArmComponent, ComponentShutdown>(OnArmShutdown);
        SubscribeLocalEvent<NullRodVorpalScytheComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<NullRodVorpalScytheComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<NullRodVorpalScytheComponent, GetVerbsEvent<UtilityVerb>>(OnGetVerbs);
        SubscribeLocalEvent<NullRodVorpalScytheComponent, NullRodDeathKnellDoAfterEvent>(OnDeathKnell);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NullRodVorpalScytheComponent>();
        while (query.MoveNext(out var scythe))
        {
            if (scythe.Empowerment != ScytheEmpowerment.Weak && now > scythe.EmpowermentEnd)
                scythe.Empowerment = ScytheEmpowerment.Weak;
        }
    }

    private void OnShardUse(Entity<NullRodScytheShardComponent> shard, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        if (HasComp<NullRodScytheArmComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("null-rod-shard-already"), user, user);
            return;
        }

        var arm = AddComp<NullRodScytheArmComponent>(user);
        arm.Scythe = shard.Comp.Scythe;
        _actions.AddAction(user, ref arm.ActionEntity, arm.Action);

        _popup.PopupEntity(Loc.GetString("null-rod-shard-implant"), user, user, PopupType.LargeCaution);
        _audio.PlayPvs(shard.Comp.ImplantSound, user);
        QueueDel(shard);
    }

    private void OnArmShutdown(Entity<NullRodScytheArmComponent> arm, ref ComponentShutdown args)
    {
        _actions.RemoveAction(arm.Owner, arm.Comp.ActionEntity);
        if (arm.Comp.ScytheEntity is { } scythe)
            QueueDel(scythe);
    }

    private void OnToggleScythe(Entity<NullRodScytheArmComponent> arm, ref NullRodToggleScytheActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = arm.Owner;

        if (arm.Comp.ScytheEntity is { } existing && !TerminatingOrDeleted(existing))
        {
            Retract(arm, existing);
            return;
        }

        var scythe = Spawn(arm.Comp.Scythe, Transform(user).Coordinates);
        if (!_hands.TryPickupAnyHand(user, scythe))
        {
            Del(scythe);
            _popup.PopupEntity(Loc.GetString("null-rod-scythe-hands-full"), user, user);
            return;
        }

        EnsureComp<UnremoveableComponent>(scythe);
        arm.Comp.ScytheEntity = scythe;
        _audio.PlayPvs(arm.Comp.ExtendSound, user);
    }

    private void Retract(Entity<NullRodScytheArmComponent> arm, EntityUid scythe)
    {
        var user = arm.Owner;
        if (TryComp<NullRodVorpalScytheComponent>(scythe, out var comp) && comp.Empowerment == ScytheEmpowerment.Weak)
        {
            _popup.PopupEntity(Loc.GetString("null-rod-scythe-punish", ("scythe", scythe)), user, user, PopupType.LargeCaution);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/demon_attack1.ogg"), user);
            var damage = new DamageSpecifier { DamageDict = { ["Slash"] = arm.Comp.RetractDamage } };
            _damageable.TryChangeDamage(user, damage, true, origin: scythe);
        }

        arm.Comp.ScytheEntity = null;
        _audio.PlayPvs(arm.Comp.RetractSound, user);
        QueueDel(scythe);
    }

    private void OnMeleeHit(Entity<NullRodVorpalScytheComponent> scythe, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        if (scythe.Comp.Empowerment == ScytheEmpowerment.Empowered)
            args.BonusDamage += args.BaseDamage * (scythe.Comp.EmpoweredMultiplier - 1f);

        foreach (var target in args.HitEntities)
        {
            if (target == args.User || !HasComp<MobStateComponent>(target) || _mobState.IsDead(target))
                continue;

            Empower(scythe, ScytheEmpowerment.Sated);
            break;
        }
    }

    /// <summary>Сила косы только растёт, а таймер продлевается лишь не меньшим насыщением (scythe_empowerment).</summary>
    private void Empower(Entity<NullRodVorpalScytheComponent> scythe, ScytheEmpowerment empowerment)
    {
        if (scythe.Comp.Empowerment > empowerment || empowerment == ScytheEmpowerment.Weak)
            return;

        scythe.Comp.Empowerment = empowerment;
        scythe.Comp.EmpowermentEnd = _timing.CurTime + scythe.Comp.EmpowermentDuration / (int) empowerment;
    }

    private void OnExamined(Entity<NullRodVorpalScytheComponent> scythe, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("null-rod-scythe-examine-knell"));
        args.PushMarkup(Loc.GetString(scythe.Comp.Empowerment switch
        {
            ScytheEmpowerment.Empowered => "null-rod-scythe-examine-empowered",
            ScytheEmpowerment.Sated => "null-rod-scythe-examine-sated",
            _ => "null-rod-scythe-examine-weak",
        }));
    }

    private void OnGetVerbs(Entity<NullRodVorpalScytheComponent> scythe, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Target == args.User
            || !HasComp<MobStateComponent>(args.Target) || !HasComp<MindContainerComponent>(args.Target))
        {
            return;
        }

        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("null-rod-scythe-knell-verb"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/priest/null/vorpal-scythe.rsi"), "icon"),
            Act = () => StartDeathKnell(scythe, user, target),
            Impact = Content.Shared.Database.LogImpact.High,
        });
    }

    private void StartDeathKnell(Entity<NullRodVorpalScytheComponent> scythe, EntityUid user, EntityUid target)
    {
        // Беспомощную жертву обезглавить проще, дёргающуюся — труднее.
        var modifier = 1f;
        if (!_mobState.IsAlive(target))
            modifier *= 0.5f;
        if (_mobState.IsAlive(target) && HasComp<JitteringComponent>(target))
            modifier *= 1.5f;
        if (scythe.Comp.Empowerment == ScytheEmpowerment.Empowered)
            modifier *= 0.5f;

        var args = new DoAfterArgs(EntityManager,
            user,
            scythe.Comp.DeathKnellTime * modifier,
            new NullRodDeathKnellDoAfterEvent(),
            scythe,
            target,
            scythe)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        _popup.PopupEntity(Loc.GetString("null-rod-scythe-knell-start", ("user", user), ("scythe", scythe.Owner), ("target", target)),
            target,
            PopupType.LargeCaution);
    }

    private void OnDeathKnell(Entity<NullRodVorpalScytheComponent> scythe, ref NullRodDeathKnellDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        var user = args.User;

        // Разумная жертва (сейчас или когда-то) усиливает косу, бездушная лишь насыщает.
        // Разум проверяется до удара: после смерти игрок может уйти призраком.
        var sentient = _mind.TryGetMind(target, out _, out _)
            || TryComp<MindContainerComponent>(target, out var container) && container.Mind != null;
        var empowerment = sentient ? ScytheEmpowerment.Empowered : ScytheEmpowerment.Sated;

        _audio.PlayPvs(KnellSound, target);
        _damageable.TryChangeDamage(target, KnellDamage, true, origin: user);
        Empower(scythe, empowerment);

        _popup.PopupEntity(Loc.GetString("null-rod-scythe-knell-done", ("user", user), ("scythe", scythe.Owner), ("target", target)),
            target,
            PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString(sentient ? "null-rod-scythe-knell-empowered" : "null-rod-scythe-knell-sated"),
            user,
            user,
            PopupType.Large);
    }
}
