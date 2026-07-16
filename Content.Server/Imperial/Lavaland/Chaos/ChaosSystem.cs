using Content.Server.Body.Systems;
using Content.Server.Objectives.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Lavaland.Chaos;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Chaos;

public sealed class ChaosSystem : EntitySystem
{
    private const string ChaosRageStatusEffect = "StatusEffectChaosRage";
    private static readonly SoundSpecifier ChaosUseSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_misc_e1m1.ogg");
    private static readonly ProtoId<ReagentPrototype> StimulantsId = "Stimulants";

    private sealed class ContractState
    {
        public EntityUid Berserker;
        public EntityUid? BerserkerMind;
        public TimeSpan EndTime;
        public HashSet<EntityUid> Hunters = new();
    }

    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<ContractState> _activeContracts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChaosBottleComponent, UseInHandEvent>(OnBottleUseInHand);
        SubscribeLocalEvent<BloodContractComponent, AfterInteractEvent>(OnContractAfterInteract);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_activeContracts.Count == 0)
            return;

        var now = _timing.CurTime;

        for (var i = _activeContracts.Count - 1; i >= 0; i--)
        {
            var contract = _activeContracts[i];
            var berserkerDead = !Exists(contract.Berserker) || !IsAlive(contract.Berserker);

            if (!berserkerDead && now < contract.EndTime)
                continue;

            EndContract(contract);
            _activeContracts.RemoveAt(i);
        }
    }

    private void OnBottleUseInHand(EntityUid uid, ChaosBottleComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!IsAlive(args.User))
            return;

        _audio.PlayPvs(ChaosUseSound, args.User);

        ApplyBerserkEffect(args.User, comp.ChainsawPrototype, comp.StimulantsAmount, comp.RageDurationSeconds, comp.RipAndTearObjective);
        AssignKillAllObjectives(args.User, comp.RipAndTearObjective);

        QueueDel(uid);
        args.Handled = true;
    }

    private void OnContractAfterInteract(EntityUid uid, BloodContractComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        var target = args.Target.Value;
        if (!IsAlive(target))
            return;

        _audio.PlayGlobal(ChaosUseSound, Filter.Broadcast(), true);

        ApplyBerserkEffect(target, comp.BerserkerWeaponPrototype, comp.BerserkerStimulantsAmount, comp.BerserkerRageDurationSeconds, comp.BerserkerObjective);
        AssignKillAllObjectives(target, comp.BerserkerObjective);

        var contract = new ContractState
        {
            Berserker = target,
            BerserkerMind = _mind.TryGetMind(target, out var berserkerMindId, out _) ? berserkerMindId : null,
            EndTime = _timing.CurTime + TimeSpan.FromSeconds(MathF.Max(1f, comp.BerserkerRageDurationSeconds))
        };

        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } attached)
                continue;

            if (attached == target || !IsAlive(attached))
                continue;

            GiveWeapon(attached, comp.HunterWeaponPrototype);
            TryAddSpecificTargetObjective(attached, target, comp.HunterObjective);
            contract.Hunters.Add(attached);
        }

        _activeContracts.Add(contract);

        QueueDel(uid);
        args.Handled = true;
    }

    private void ApplyBerserkEffect(
        EntityUid target,
        EntProtoId weaponPrototype,
        float stimulantsAmount,
        float rageDurationSeconds,
        string objective)
    {
        TryAddRageStatus(target, rageDurationSeconds);
        GiveWeapon(target, weaponPrototype);
        InjectStimulants(target, stimulantsAmount);
        TryAddObjective(target, objective);
    }

    private void EndContract(ContractState contract)
    {
        _statusEffects.TryRemoveStatusEffect(contract.Berserker, ChaosRageStatusEffect);

        RemoveObjectivesByPrototype(contract.Berserker, "ChaosRipAndTearTargetObjective");

        foreach (var hunter in contract.Hunters)
        {
            if (contract.BerserkerMind != null)
                RemoveTargetedObjectives(hunter, "ChaosRipAndTearTargetObjective", contract.BerserkerMind.Value);
        }
    }

    private void AssignKillAllObjectives(EntityUid owner, string objectiveProto)
    {
        foreach (var target in GetAlivePlayers())
        {
            if (target == owner)
                continue;

            TryAddSpecificTargetObjective(owner, target, objectiveProto);
        }
    }

    private List<EntityUid> GetAlivePlayers()
    {
        var players = new List<EntityUid>();

        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } attached ||
                !IsAlive(attached))
                continue;

            players.Add(attached);
        }

        return players;
    }

    private void TryAddRageStatus(EntityUid target, float durationSeconds)
    {
        var duration = TimeSpan.FromSeconds(MathF.Max(0.1f, durationSeconds));
        _statusEffects.TryAddStatusEffectDuration(target, ChaosRageStatusEffect, duration);
    }

    private void GiveWeapon(EntityUid target, EntProtoId weaponPrototype)
    {
        var weapon = Spawn(weaponPrototype, Transform(target).Coordinates);
        _hands.PickupOrDrop(target, weapon, checkActionBlocker: false);
    }

    private void InjectStimulants(EntityUid target, float amount)
    {
        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        var solution = new Solution();
        solution.AddReagent(StimulantsId, FixedPoint2.New(amount));
        _bloodstream.TryAddToBloodstream((target, bloodstream), solution);
    }

    private void TryAddObjective(EntityUid target, string objectiveProto)
    {
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return;

        _mind.TryAddObjective(mindId, mind, objectiveProto);
    }

    private void TryAddSpecificTargetObjective(EntityUid owner, EntityUid target, string objectiveProto)
    {
        if (!_mind.TryGetMind(owner, out var mindId, out var mind) ||
            !_mind.TryGetMind(target, out var targetMindId, out _) ||
            mind.OwnedEntity is not { } controlled)
            return;

        var hadOverride = TryComp<TargetOverrideComponent>(controlled, out var targetOverride);
        var previousTarget = targetOverride?.Target;

        targetOverride ??= EnsureComp<TargetOverrideComponent>(controlled);
        targetOverride.Target = targetMindId;

        _mind.TryAddObjective(mindId, mind, objectiveProto);

        if (hadOverride)
            targetOverride.Target = previousTarget;
        else
            RemComp<TargetOverrideComponent>(controlled);
    }

    private void RemoveObjectivesByPrototype(EntityUid owner, string objectiveProto)
    {
        if (!_mind.TryGetMind(owner, out var mindId, out var mind))
            return;

        for (var i = mind.Objectives.Count - 1; i >= 0; i--)
        {
            var objective = mind.Objectives[i];
            if (MetaData(objective).EntityPrototype?.ID != objectiveProto)
                continue;

            _mind.TryRemoveObjective(mindId, mind, i);
        }
    }

    private void RemoveTargetedObjectives(EntityUid owner, string objectiveProto, EntityUid target)
    {
        if (!_mind.TryGetMind(owner, out var mindId, out var mind))
            return;

        for (var i = mind.Objectives.Count - 1; i >= 0; i--)
        {
            var objective = mind.Objectives[i];
            if (MetaData(objective).EntityPrototype?.ID != objectiveProto)
                continue;

            if (!TryComp<TargetObjectiveComponent>(objective, out var targetComp) || targetComp.Target != target)
                continue;

            _mind.TryRemoveObjective(mindId, mind, i);
        }
    }

    private bool IsAlive(EntityUid uid)
    {
        return TryComp<MobStateComponent>(uid, out var state) && state.CurrentState == MobState.Alive;
    }
}
