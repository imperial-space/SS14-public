using System.Linq;
using Robust.Shared.Random;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Chemistry;
using Content.Shared.Damage;
using Content.Shared.Emp;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology;

public sealed partial class SlimeSystem
{
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;

    private static readonly ProtoId<StackPrototype> PlasmaStack = "Plasma";
    private static readonly EntProtoId HeartEffect = "SlimeHeartEffect";
    private static readonly SoundSpecifier PunchMiss = new SoundPathSpecifier("/Audio/Weapons/punchmiss.ogg");
    private static readonly SoundSpecifier Shove = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");
    private static readonly SoundSpecifier Sparks = new SoundCollectionSpecifier("sparks");

    private void InitializeCombat()
    {
        SubscribeLocalEvent<SlimeComponent, MeleeHitEvent>(OnSlimeMeleeHit);
        SubscribeLocalEvent<SlimeComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<SlimeComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<SlimeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SlimeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<SlimeComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<SlimeComponent, EmpPulseEvent>(OnEmp);
    }

    /// <summary>melee_damage 7–17 у детёныша, вдвое больше у взрослого.</summary>
    private void OnGetMeleeDamage(Entity<SlimeComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (args.Weapon != ent.Owner)
            return;

        var adult = ent.Comp.Stage == SlimeLifeStage.Adult;
        var roll = _random.Next(7, 18) * (adult ? 2 : 1);
        var current = args.Damage.GetTotal().Float();
        if (current > 0f)
            args.Damage *= roll / current;
    }

    /// <summary>on_slime_pre_attack(): удар током, отбирание еды у других слаймов, кормление.</summary>
    private void OnSlimeMeleeHit(Entity<SlimeComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var target = args.HitEntities[0];
        if (!TryComp<SlimeBrainComponent>(ent, out var brain))
            return;

        // Атака по жертве, на которой слайм висит, — отцепиться.
        if (brain.FeedingOn == target)
        {
            StopFeeding(ent, brain);
            args.Handled = true;
            return;
        }

        if (TryComp<BorgChassisComponent>(target, out _))
        {
            ShockBorg(ent, target);
            args.Handled = true;
            return;
        }

        if (TryComp<SlimeComponent>(target, out var targetSlime))
        {
            AttackSlime(ent, (target, targetSlime));
            return;
        }

        if (HasComp<HumanoidProfileComponent>(target) && ent.Comp.PowerLevel > SlimeConstants.MinPower)
            ShockCarbon(ent, target);

        OnTargetCaught(ent, brain, target);
    }

    /// <summary>Удар током по органике: оглушение, падение, заикание и ожоги при большом заряде.</summary>
    private void ShockCarbon(Entity<SlimeComponent> ent, EntityUid target)
    {
        var stunProb = ent.Comp.PowerLevel * SlimeConstants.ShockPercentagePerLevel + SlimeConstants.BaseShockPercentage;
        if (!_random.Prob(stunProb / 100f))
            return;

        _popup.PopupEntity(Loc.GetString("slime-shocks", ("slime", ent.Owner), ("target", target)), target, PopupType.MediumCaution);
        _audio.PlayPvs(Sparks, target);

        var power = ent.Comp.PowerLevel + _random.Next(0, 4);
        _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(2));
        _stun.TryKnockdown(target, TimeSpan.FromSeconds(power * 2));
        _stutter.DoStutter(target, TimeSpan.FromSeconds(power * 2), refresh: false);

        if (_random.Prob(stunProb / 100f) && ent.Comp.PowerLevel >= SlimeConstants.ExtraShockCost)
        {
            ent.Comp.PowerLevel = Math.Clamp(ent.Comp.PowerLevel - SlimeConstants.ExtraShockCost, SlimeConstants.MinPower, SlimeConstants.MaxPower);
            Dirty(ent);
            Damage(target, "Heat", ent.Comp.PowerLevel * _random.Next(6, 11), ent);
        }
    }

    /// <summary>Киборгов слайм только трясёт током.</summary>
    private void ShockBorg(Entity<SlimeComponent> ent, EntityUid borg)
    {
        _audio.PlayPvs(Sparks, borg);
        var stunProb = ent.Comp.PowerLevel * SlimeConstants.ShockPercentagePerLevel + SlimeConstants.BaseShockPercentage;
        if (_random.Prob(stunProb / 100f) && ent.Comp.PowerLevel >= SlimeConstants.ExtraShockCost)
        {
            ent.Comp.PowerLevel = Math.Clamp(ent.Comp.PowerLevel - SlimeConstants.ExtraShockCost, SlimeConstants.MinPower, SlimeConstants.MaxPower);
            Dirty(ent);
            Damage(borg, "Blunt", ent.Comp.PowerLevel * _random.Next(6, 11), ent);
            _popup.PopupEntity(Loc.GetString("slime-shocks", ("slime", ent.Owner), ("target", borg)), borg, PopupType.MediumCaution);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("slime-shock-fails", ("slime", ent.Owner), ("target", borg)), borg);
        }
    }

    /// <summary>Слайм бьёт слайма: сдёргивает с жертвы, крадёт питательность и лечится.</summary>
    private void AttackSlime(Entity<SlimeComponent> ent, Entity<SlimeComponent> target)
    {
        if (TryComp<SlimeBrainComponent>(target, out var targetBrain) && targetBrain.FeedingOn != null)
        {
            StopFeeding(target, targetBrain, silent: true);
            _popup.PopupEntity(Loc.GetString("slime-pulls-off", ("slime", ent.Owner), ("target", target.Owner)), target);
            return;
        }

        var adult = ent.Comp.Stage == SlimeLifeStage.Adult;
        if (target.Comp.Nutrition >= 100f)
        {
            var stolen = Math.Min(adult ? 90f : 50f, target.Comp.Nutrition);
            AdjustNutrition(target, -stolen);
            AdjustNutrition(ent, stolen);
        }

        if (!_mobState.IsDead(target))
            Damage(ent, "Blunt", adult ? -20 : -10);
    }

    /// <summary>on_attack_hand(): попытка стащить слайма с жертвы рукой.</summary>
    private void OnInteractHand(Entity<SlimeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !TryComp<SlimeBrainComponent>(ent, out var brain) || brain.FeedingOn is not { } victim)
            return;

        args.Handled = true;
        var self = victim == args.User;
        if (self ? _random.Prob(0.6f) : _random.Prob(0.3f))
        {
            _popup.PopupEntity(Loc.GetString(self ? "slime-wrestle-fail-self" : "slime-wrestle-fail", ("user", args.User), ("slime", ent.Owner), ("victim", victim)), ent, PopupType.Medium);
            _audio.PlayPvs(PunchMiss, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("slime-wrestle-success", ("user", args.User), ("slime", ent.Owner)), ent, PopupType.Medium);
        _audio.PlayPvs(Shove, ent);
        Discipline(ent, brain);
    }

    /// <summary>attackby(): предмет может пройти насквозь, удар может укротить слайма.</summary>
    private void OnAttacked(Entity<SlimeComponent> ent, ref AttackedEvent args)
    {
        if (args.Used == args.User || !TryComp<MeleeWeaponComponent>(args.Used, out var melee))
            return;

        var damage = _melee.GetDamage(args.Used, args.User, melee);
        var force = damage.GetTotal().Float();
        if (force <= 0f)
            return;

        // check_item_passthrough(): 25% — предмет проходит насквозь.
        if (_random.Prob(0.25f))
        {
            args.BonusDamage -= damage;
            _popup.PopupEntity(Loc.GetString("slime-passthrough", ("item", args.Used), ("slime", ent.Owner)), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (TryComp<SlimeBrainComponent>(ent, out var brain))
        {
            Retaliate(ent, brain, args.User);

            // try_discipline_slime()
            if (force >= 3f)
            {
                var effect = force * (ent.Comp.Stage == SlimeLifeStage.Baby ? 2f : 1f);
                if (_random.Prob(Math.Min(1f, (10f + effect) / 100f)))
                    Discipline(ent, brain);
            }
        }
    }

    /// <summary>Плазма — лакомство (дружба), сумка экстрактов — кроссбридинг.</summary>
    private void OnInteractUsing(Entity<SlimeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || _mobState.IsDead(ent))
            return;

        if (TryComp<StackComponent>(args.Used, out var stack) && stack.StackTypeId == PlasmaStack)
        {
            args.Handled = true;
            if (!_stack.TryUse((args.Used, stack), 1))
                return;
            Befriend(ent, args.User);
            _popup.PopupEntity(Loc.GetString("slime-feed-plasma"), ent, args.User);
            Spawn(HeartEffect, _transform.GetMoverCoordinates(ent));
            return;
        }

        if (HasComp<SlimeExtractBagComponent>(args.Used))
        {
            args.Handled = true;
            UseExtractBag(ent, args.Used, args.User);
        }
    }

    /// <summary>use_xeno_bag(): скормить слайму все подходящие экстракты из научной сумки.</summary>
    private void UseExtractBag(Entity<SlimeComponent> ent, EntityUid bag, EntityUid user)
    {
        if (ent.Comp.CrossbreedModification == null)
        {
            _popup.PopupEntity(Loc.GetString("slime-not-mutating"), ent, user);
            return;
        }

        if (!_container.TryGetContainer(bag, "storagebase", out var container))
            return;

        var found = false;
        foreach (var item in container.ContainedEntities.ToList())
        {
            if (!TryComp<SlimeExtractComponent>(item, out var extract) || extract.CrossbreedModification != ent.Comp.CrossbreedModification)
                continue;

            QueueDel(item);
            ent.Comp.AppliedCrossbreedAmount++;
            found = true;
            if (ent.Comp.AppliedCrossbreedAmount >= SlimeConstants.ExtractCrossingRequired)
            {
                Dirty(ent);
                _popup.PopupEntity(Loc.GetString("slime-bag-mutates"), ent, user);
                _audio.PlayPvs(AttackBlob, ent);
                SpawnCorecross(ent);
                return;
            }
        }

        Dirty(ent);
        if (!found)
        {
            _popup.PopupEntity(Loc.GetString("slime-bag-none"), ent, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("slime-bag-fed"), ent, user);
        _audio.PlayPvs(AttackBlob, ent);
    }

    /// <summary>apply_water(): вода обжигает слайма и сбивает с него ярость.</summary>
    private void OnReaction(Entity<SlimeComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || args.Reagent.ID != "Water" || args.ReagentQuantity.Quantity < 1)
            return;

        Damage(ent, "Blunt", _random.Next(15, 21));
        if (TryComp<SlimeBrainComponent>(ent, out var brain))
            Discipline(ent, brain);
    }

    /// <summary>emp_act(): ЭМИ обнуляет заряд.</summary>
    private void OnEmp(Entity<SlimeComponent> ent, ref EmpPulseEvent args)
    {
        ent.Comp.PowerLevel = 0;
        Dirty(ent);
    }

    /// <summary>discipline_slime(): слайм отпускает жертву, детёныш забывает цель, иногда пропадает ярость.</summary>
    public void Discipline(Entity<SlimeComponent> ent, SlimeBrainComponent brain)
    {
        StopFeeding(ent, brain, silent: true);
        if (ent.Comp.Stage == SlimeLifeStage.Baby && _random.Prob(0.8f))
        {
            brain.Target = null;
            brain.HuntTarget = null;
        }

        if (_random.Prob(0.1f))
            SetRabid(ent, false);
    }
}
