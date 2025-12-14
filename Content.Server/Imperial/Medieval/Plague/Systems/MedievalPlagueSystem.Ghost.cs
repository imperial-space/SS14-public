using System.Linq;
using Content.Server.Body.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private void InitializeGhost()
    {
        SubscribeLocalEvent<MedievalPlagueGhostComponent, ComponentInit>(OnGhostInit);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, ComponentShutdown>(OnGhostShutdown);


        SubscribeLocalEvent<MedievalPlagueGhostComponent, InfectTargetActionEvent>(OnInfectAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueForcedVomitActionEvent>(OnVomitAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueAsthmaticActionEvent>(OnAsthmaAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueDizzinessActionEvent>(OnDizzinessAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueSleepyActionEvent>(OnSleepAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueBreakImmunityActionEvent>(OnImmunityAction);

        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlaguePolymorphMouseActionEvent>(OnMousuePolymorphAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueTeleportInfectedActionEvent>(OnTeleportInfectedAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueTeleportNotInfectedActionEvent>(OnTeleportNotInfectedAction);

        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueSpawnEntityActionEvent>(OnSpawnEntityAction);

        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueInjuryActionEvent>(OnInjuryAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueCataractActionEvent>(OnCataractAction);
        SubscribeLocalEvent<MedievalPlagueGhostComponent, PlagueHeartAttackActionEvent>(OnHeartAttackAction);
    }

    private void OnGhostInit(EntityUid uid, MedievalPlagueGhostComponent comp, ComponentInit args)
    {
        _actions.AddAction(uid, "OpenPlagueMenuAction");
        _actions.AddAction(uid, ref comp.InfectAction, "PlagueInfectAction");

        UpdateInfectAction();
        _alerts.ShowAlert(uid, comp.AlertId);

        _data.PlagueGhosts++;
        _data.Points += comp.Points;

        foreach (var item in _symptoms.Where(x => x.Value.Unlocked))
        {
            AddPrototypeActions(uid, item.Key);
        }
    }

    private void OnGhostShutdown(EntityUid uid, MedievalPlagueGhostComponent comp, ComponentShutdown args)
    {
        _data.PlagueGhosts--;
    }

    private void OnInfectAction(EntityUid uid, MedievalPlagueGhostComponent comp, InfectTargetActionEvent args)
    {
        if (args.Handled)
            return;

        var cost = GetInfectionCost();

        if (comp.FreeInfections > 0)
            comp.FreeInfections--;
        else if (comp.Points < cost)
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-not-enough-points-popup"), args.Target, uid);
            return;
        }
        else
        {
            TryChangePoints(uid, -cost, comp);
        }

        if (TryInfect(args.Target, addPoint: false))
        {
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/infect.ogg"), Filter.Empty().FromEntities(uid), false);
            _popup.PopupEntity(Loc.GetString("medieval-plague-infected-success-popup", ("target", Name(args.Target))), args.Target, uid);
        }
        else
            _popup.PopupEntity(Loc.GetString("medieval-plague-infected-failure-popup", ("target", Name(args.Target))), args.Target, uid);
    }

    private void OnVomitAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueForcedVomitActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _vomit.Vomit(args.Target);

        if (_symptoms.Where(x => x.Value.Unlocked).ToDictionary().ContainsKey("BloodVomit") && TryComp<BloodstreamComponent>(uid, out var bloodstream))
        {
            Solution sol = new(bloodstream.BloodReagent, 20f);
            _puddle.TrySpillAt(Transform(uid).Coordinates, sol, out _, false);
            DamageSpecifier damage = new()
            {
                DamageDict = new()
                {
                    { "Bloodloss", 10 }
                }
            };

            _damageable.TryChangeDamage(args.Target, damage, true);
        }

        _popup.PopupEntity(Loc.GetString("medieval-plague-forced-vomit-popup", ("target", Name(args.Target))), args.Target, uid);
    }

    private void OnAsthmaAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueAsthmaticActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        var block = EnsureComp<PlagueBlockBreathingComponent>(uid);
        block.EndTime = _timing.CurTime + TimeSpan.FromSeconds(25);
        _popup.PopupEntity(Loc.GetString("medieval-plague-asthma-ghost-popup", ("target", Name(args.Target))), args.Target, uid);
        _popup.PopupEntity(Loc.GetString("medieval-plague-asthma-target-popup"), args.Target, args.Target, Shared.Popups.PopupType.MediumCaution);
    }

    private void OnDizzinessAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueDizzinessActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _jitter.DoJitter(args.Target, TimeSpan.FromSeconds(6), true, 10, 3);
        _drunk.TryApplyDrunkenness(args.Target, TimeSpan.FromSeconds(600));
        var modifier = EnsureComp<MovespeedModifierMetabolismComponent>(uid);
        modifier.SprintSpeedModifier = 0.7f;
        modifier.WalkSpeedModifier = 0.7f;
        modifier.ModifierTimer = _timing.CurTime + TimeSpan.FromSeconds(6);
        Dirty(uid, modifier);

        _audio.PlayGlobal(new SoundCollectionSpecifier("PlagueDizziness"), Filter.Empty().FromEntities(args.Target), false);
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/dizzy.ogg"), Filter.Empty().FromEntities(uid), false);

        _popup.PopupEntity(Loc.GetString("medieval-plague-dizziness-ghost-popup", ("target", Name(args.Target))), args.Target, uid);
        _popup.PopupEntity(Loc.GetString("medieval-plague-dizziness-target-popup"), args.Target, args.Target, Shared.Popups.PopupType.MediumCaution);
    }

    private void OnSleepAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueSleepyActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _status.TryAddStatusEffect<ForcedSleepingStatusEffectComponent>(args.Target, "ForcedSleep", TimeSpan.FromSeconds(15), true);

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/sleepy.ogg"), Filter.Empty().FromEntities(uid, args.Target), false);
        _popup.PopupEntity(Loc.GetString("medieval-plague-forced-sleep-ghost-popup", ("target", Name(args.Target))), args.Target, uid);
    }

    private void OnImmunityAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueBreakImmunityActionEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<MedievalPlagueImmuneComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-not-immune", ("target", Name(args.Target))), args.Target, uid);
            return;
        }

        if (!TryChangePoints(uid, -args.Cost))
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-not-enough-points-popup"), args.Target, uid);
            return;
        }

        args.Handled = true;

        if (!_symptoms.Where(x => x.Value.Unlocked).ToDictionary().ContainsKey("ImmunityBreak2") &&
            TryComp<MedievalPlagueImmuneComponent>(args.Target, out var immune) &&
            (immune.StartTime + TimeSpan.FromMinutes(15) < _timing.CurTime || immune.HardImmunity))
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-break-immunity-failure-popup", ("target", Name(args.Target))), args.Target, uid);
            return;
        }

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/immune_break.ogg"), Filter.Empty().FromEntities(uid), false);
        _popup.PopupEntity(Loc.GetString("medieval-plague-break-immunity-success-popup", ("target", Name(args.Target))), args.Target, uid);
        RemComp<MedievalPlagueImmuneComponent>(args.Target);
    }

    private void OnMousuePolymorphAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlaguePolymorphMouseActionEvent args)
    {
        if (args.Handled)
            return;

        var xform = Transform(uid);

        if (_lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(xform.Coordinates, 8f).Any())
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-mouse-polymorph-nearby-popup"), uid, uid);
            return;
        }

        if (!TryUseAbility(uid, null, args.Cost, true))
            return;

        args.Handled = true;

        if (_symptoms.Where(x => x.Value.Unlocked).ToDictionary().ContainsKey("MorphRatsAction"))
        {
            for (var i = 0; i < 2; i++)
                Spawn("MedievalMobPlagueMouse", xform.Coordinates);
        }

        _polymorph.PolymorphEntity(uid, "MedievalPlagueMousePolymorph");
    }

    private void OnTeleportInfectedAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueTeleportInfectedActionEvent args)
    {
        if (args.Handled)
            return;

        var targets = EntityManager.AllEntities<MedievalPlagueInfectedComponent>().Where(x => _mobState.IsAlive(x.Owner));
        if (!targets.Any())
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-teleport-infected-none-popup"), uid, uid);
            return;
        }

        args.Handled = true;

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/teleport.ogg"), Filter.Empty().FromEntities(uid), false);
        _transform.SetCoordinates(uid, Transform(_random.Pick(targets.ToList())).Coordinates);
    }

    private void OnTeleportNotInfectedAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueTeleportNotInfectedActionEvent args)
    {
        if (args.Handled)
            return;

        var targets = EntityManager.AllEntities<MedievalCanBeInfectedComponent>().Where(x => !HasComp<MedievalPlagueInfectedComponent>(x.Owner) && _mobState.IsAlive(x.Owner));
        if (!targets.Any())
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-teleport-not-infected-none-popup"), uid, uid);
            return;
        }

        args.Handled = true;

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/teleport.ogg"), Filter.Empty().FromEntities(uid), false);
        _transform.SetCoordinates(uid, Transform(_random.Pick(targets.ToList())).Coordinates);
    }

    private void OnSpawnEntityAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueSpawnEntityActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, null, args.Cost, true))
            return;

        args.Handled = true;
        Spawn(args.Prototype, args.Target);
    }

    private void OnInjuryAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueInjuryActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("medieval-plague-injury-ghost-popup", ("target", Name(args.Target))), args.Target, uid);
        _popup.PopupEntity(Loc.GetString("medieval-plague-injury-target-popup"), args.Target, args.Target, Shared.Popups.PopupType.MediumCaution);

        _blood.TryModifyBleedAmount(args.Target, 7.5f);
    }

    private void OnCataractAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueCataractActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("medieval-plague-cataract-ghost-popup", ("target", Name(args.Target))), args.Target, uid);
        _popup.PopupEntity(Loc.GetString("medieval-plague-cataract-target-popup"), args.Target, args.Target, Shared.Popups.PopupType.MediumCaution);

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/injury.ogg"), Filter.Empty().FromEntities(uid), false);
        _blind.SetMinDamage(args.Target, 3);
    }

    private void OnHeartAttackAction(EntityUid uid, MedievalPlagueGhostComponent comp, PlagueHeartAttackActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseAbility(uid, args.Target, args.Cost, args.AllowIncubation))
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("medieval-plague-heart-attack-ghost-popup", ("target", Name(args.Target))), args.Target, uid);

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/heart_stop.ogg"), Filter.Empty().FromEntities(uid), false);
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/my_heart_stopped.ogg"), Filter.Empty().FromEntities(args.Target), false);

        _jitter.DoJitter(args.Target, TimeSpan.FromSeconds(4), true, frequency: 6);
        _mobState.ChangeMobState(args.Target, Shared.Mobs.MobState.Dead);
    }

    private bool TryUseAbility(EntityUid uid, EntityUid? target, int cost = 0, bool allowIncubation = false)
    {
        if (!TryComp<MedievalPlagueGhostComponent>(uid, out var comp))
            return false;

        if (target.HasValue)
        {
            if (!TryComp<MedievalPlagueInfectedComponent>(target, out var infected))
            {
                _popup.PopupEntity(Loc.GetString("popup-plague-action-fail-not-infected", ("target", Name(target.Value))), target ?? uid, uid);
                return false;
            }

            if (infected.Incubation && !allowIncubation)
            {
                _popup.PopupEntity(Loc.GetString("popup-plague-action-fail-incubation", ("target", Name(target.Value))), target ?? uid, uid);
                return false;
            }
        }

        if (comp.Points < cost)
        {
            _popup.PopupEntity(Loc.GetString("medieval-plague-not-enough-points-popup"), target ?? uid, uid);
            return false;
        }

        return TryChangePoints(uid, -cost, comp);
    }

    private int GetInfectionCost()
    {
        var prototypes = _proto.EnumeratePrototypes<MedievalPlagueSymptomPrototype>();
        var unlocked = _symptoms.Where(x => x.Value.Unlocked);

        var max = 1;
        for (var i = 1; i <= prototypes.Select(x => x.Tier).Max(); i++)
        {
            var protos = prototypes.Where(x => x.Tier == i);
            var unlockedProtos = unlocked.Where(x => _proto.Index(x.Key).Tier == i);

            if (unlockedProtos.Count() / protos.Count() >= 0.6f)
                max = i + 1;
        }

        return max * 2;
    }

    private void UpdateInfectAction()
    {
        var query = EntityQueryEnumerator<MedievalPlagueGhostComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.InfectAction.HasValue)
                continue;

            _meta.SetEntityDescription(comp.InfectAction.Value, Loc.GetString("plague-infect-action-desc", ("cost", GetInfectionCost())));
        }
    }
}
