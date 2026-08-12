using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.Turrets;
using Content.Shared.Damage;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Turrets;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Делает цель враждебной станционным NPC и турелям через штатную систему фракций.
/// </summary>
public sealed class DeathNoteHostileFactionPresetHandlerSystem : DeathNotePresetHandlerSystem, IDeathNotePresetHandler
{
    [Dependency] private readonly BatteryWeaponFireModesSystem _fireModes = default!;
    [Dependency] private readonly DeployableTurretSystem _deployableTurrets = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.HostileFaction;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathNoteFactionOverrideComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DeathNoteFactionOverrideComponent, ComponentShutdown>(OnOverrideShutdown);
        SubscribeLocalEvent<DeathNoteEnergyTurretComponent, AmmoShotEvent>(OnEnergyTurretShot);
        SubscribeLocalEvent<DeathNoteEnergyTurretProjectileComponent, ProjectileHitEvent>(OnEnergyTurretProjectileHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DeathNoteFactionOverrideComponent>();
        while (query.MoveNext(out var uid, out var factionOverride))
        {
            if (_timing.CurTime >= factionOverride.RestoreAt)
                RemCompDeferred<DeathNoteFactionOverrideComponent>(uid);
        }

        var turretQuery = EntityQueryEnumerator<
            DeathNoteEnergyTurretComponent,
            BatteryWeaponFireModesComponent,
            DeployableTurretComponent>();
        while (turretQuery.MoveNext(
                   out var turret,
                   out var deathNoteTurret,
                   out var fireModes,
                   out var deployableTurret))
        {
            deathNoteTurret.Targets.RemoveWhere(target =>
                Deleted(target) ||
                !HasComp<DeathNoteFactionOverrideComponent>(target));

            if (deathNoteTurret.Targets.Count > 0)
            {
                if (!deployableTurret.Enabled)
                    _deployableTurrets.TrySetState((turret, deployableTurret), true);

                if (TryComp(turret, out HTNComponent? htn) && htn.Enabled)
                    _htn.SetHTNEnabled((turret, htn), false);

                if (fireModes.CurrentFireMode != deathNoteTurret.LethalFireMode)
                    _fireModes.TrySetFireMode((turret, fireModes), deathNoteTurret.LethalFireMode);

                if (_timing.CurTime >= deployableTurret.AnimationCompletionTime &&
                    TryGetMarkedTargetInRange(turret, deathNoteTurret, out var markedTarget))
                {
                    var rangedCombat = EnsureComp<NPCRangedCombatComponent>(turret);
                    rangedCombat.Target = markedTarget;
                    rangedCombat.UseOpaqueForLOSChecks = true;
                    rangedCombat.RotationSpeed = new Angle(Math.PI);
                }
                else if (TryComp(turret, out NPCRangedCombatComponent? rangedCombat) &&
                         deathNoteTurret.Targets.Contains(rangedCombat.Target))
                {
                    RemComp<NPCRangedCombatComponent>(turret);
                }

                continue;
            }

            RestoreRangedCombat(turret, deathNoteTurret);

            if (fireModes.CurrentFireMode != deathNoteTurret.OriginalFireMode)
            {
                _fireModes.TrySetFireMode((turret, fireModes), deathNoteTurret.OriginalFireMode);
                continue;
            }

            if (deployableTurret.Enabled != deathNoteTurret.OriginalEnabled)
            {
                _deployableTurrets.TrySetState(
                    (turret, deployableTurret),
                    deathNoteTurret.OriginalEnabled);
                continue;
            }

            RestoreHtn(turret, deathNoteTurret);
            RemCompDeferred<DeathNoteEnergyTurretComponent>(turret);
        }
    }

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before faction assignment.");
        if (parameters.Faction is not { } faction)
            return DeathNotePresetExecutionResult.Failed("Hostile faction preset has no faction configured.");
        if (parameters.TurretMinimumTargetHits <= 0 ||
            parameters.TurretMaximumTargetHits < parameters.TurretMinimumTargetHits ||
            parameters.TurretTargetRange <= 0f)
        {
            return DeathNotePresetExecutionResult.Failed(
                "Hostile faction preset has an invalid energy-turret hit range.");
        }
        if (!DeathNoteDamageHelper.TryCreate(
                parameters.Damage,
                parameters.RandomDamageMin,
                parameters.RandomDamageMax,
                _random,
                out var turretDamage))
        {
            return DeathNotePresetExecutionResult.Failed("Hostile faction preset has invalid energy-turret damage.");
        }

        var hadFactionComponent = TryComp(context.Target, out NpcFactionMemberComponent? component);
        var hadOverride = TryComp(context.Target, out DeathNoteFactionOverrideComponent? factionOverride);
        factionOverride ??= EnsureComp<DeathNoteFactionOverrideComponent>(context.Target);
        if (!hadOverride)
        {
            factionOverride.OwnsFactionComponent = !hadFactionComponent;
            if (component != null)
                factionOverride.OriginalFactions.UnionWith(component.Factions);
        }

        component ??= EnsureComp<NpcFactionMemberComponent>(context.Target);
        _factions.ClearFactions((context.Target, component), dirty: false);
        _factions.AddFaction((context.Target, component), faction);
        var restoreAt = _timing.CurTime + parameters.EffectTrackingDuration;
        if (restoreAt > factionOverride.RestoreAt)
            factionOverride.RestoreAt = restoreAt;

        var requiredHits = _random.Next(
            parameters.TurretMinimumTargetHits,
            parameters.TurretMaximumTargetHits + 1);
        factionOverride.EnergyTurretDamageRemaining = new DamageSpecifier(turretDamage);
        factionOverride.EnergyTurretHitsRemaining = requiredHits;
        AggroEnergyTurrets(context.Target, factionOverride, parameters.TurretTargetRange);

        return DeathNotePresetExecutionResult.Succeeded(
            $"Target faction was changed to {faction}; energy turrets will directly engage this target.");
    }

    private void AggroEnergyTurrets(
        EntityUid target,
        DeathNoteFactionOverrideComponent factionOverride,
        float targetRange)
    {
        var query = EntityQueryEnumerator<
            DeployableTurretComponent,
            BatteryWeaponFireModesComponent,
            TurretTargetSettingsComponent>();
        while (query.MoveNext(out var turret, out var deployableTurret, out var fireModes, out _))
        {
            if (!TryGetLethalFireMode(fireModes, out var lethalFireMode))
                continue;

            var hadOverride = TryComp(turret, out DeathNoteEnergyTurretComponent? deathNoteTurret);
            deathNoteTurret ??= EnsureComp<DeathNoteEnergyTurretComponent>(turret);
            if (!hadOverride)
            {
                deathNoteTurret.OriginalFireMode = fireModes.CurrentFireMode;
                deathNoteTurret.LethalFireMode = lethalFireMode;
                deathNoteTurret.OriginalEnabled = deployableTurret.Enabled;
                if (TryComp(turret, out HTNComponent? htn))
                {
                    deathNoteTurret.HasHtn = true;
                    deathNoteTurret.OriginalHtnEnabled = htn.Enabled;
                }

                deathNoteTurret.CreatedRangedCombat =
                    !TryComp(turret, out NPCRangedCombatComponent? rangedCombat);
                deathNoteTurret.OriginalRangedTarget = rangedCombat?.Target;
            }

            deathNoteTurret.TargetRange = Math.Max(deathNoteTurret.TargetRange, targetRange);
            deathNoteTurret.Targets.Add(target);
            _factions.AggroEntity(turret, target);
            factionOverride.EnergyTurrets.Add(turret);
            _deployableTurrets.TrySetState((turret, deployableTurret), true);
            if (TryComp(turret, out HTNComponent? currentHtn) && currentHtn.Enabled)
                _htn.SetHTNEnabled((turret, currentHtn), false);
            _fireModes.TrySetFireMode((turret, fireModes), deathNoteTurret.LethalFireMode);
        }
    }

    private bool TryGetMarkedTargetInRange(
        EntityUid turret,
        DeathNoteEnergyTurretComponent deathNoteTurret,
        out EntityUid target)
    {
        target = default;
        var turretCoordinates = Transform(turret).Coordinates;
        var closestDistance = float.MaxValue;

        foreach (var candidate in deathNoteTurret.Targets)
        {
            if (!TryComp(candidate, out MobStateComponent? mobState) ||
                mobState.CurrentState is not (MobState.Alive or MobState.Critical) ||
                !turretCoordinates.TryDistance(
                    EntityManager,
                    Transform(candidate).Coordinates,
                    out var distance) ||
                distance > deathNoteTurret.TargetRange ||
                distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            target = candidate;
        }

        return target.IsValid();
    }

    private void RestoreRangedCombat(
        EntityUid turret,
        DeathNoteEnergyTurretComponent deathNoteTurret)
    {
        if (deathNoteTurret.CreatedRangedCombat)
        {
            RemComp<NPCRangedCombatComponent>(turret);
            return;
        }

        if (deathNoteTurret.OriginalRangedTarget is not { } originalTarget)
            return;

        EnsureComp<NPCRangedCombatComponent>(turret).Target = originalTarget;
    }

    private void RestoreHtn(
        EntityUid turret,
        DeathNoteEnergyTurretComponent deathNoteTurret)
    {
        if (!deathNoteTurret.HasHtn ||
            !TryComp(turret, out HTNComponent? htn) ||
            htn.Enabled == deathNoteTurret.OriginalHtnEnabled)
        {
            return;
        }

        _htn.SetHTNEnabled((turret, htn), deathNoteTurret.OriginalHtnEnabled);
    }

    private bool TryGetLethalFireMode(
        BatteryWeaponFireModesComponent fireModes,
        out int lethalFireMode)
    {
        lethalFireMode = -1;
        var highestDamage = 0f;

        for (var index = 0; index < fireModes.FireModes.Count; index++)
        {
            var mode = fireModes.FireModes[index];
            if (!_prototypeManager.TryIndex<EntityPrototype>(mode.Prototype, out var prototype) ||
                !prototype.TryGetComponent<ProjectileComponent>(out var projectile, Factory))
            {
                continue;
            }

            var damage = projectile.Damage.GetTotal().Float();
            if (damage <= highestDamage)
                continue;

            highestDamage = damage;
            lethalFireMode = index;
        }

        return lethalFireMode >= 0;
    }

    private void OnEnergyTurretShot(
        Entity<DeathNoteEnergyTurretComponent> ent,
        ref AmmoShotEvent args)
    {
        if (!TryComp(ent, out GunComponent? gun) ||
            gun.Target is not { } target ||
            !ent.Comp.Targets.Contains(target) ||
            !HasComp<DeathNoteFactionOverrideComponent>(target))
        {
            return;
        }

        foreach (var projectileUid in args.FiredProjectiles)
        {
            var deathNoteProjectile = EnsureComp<DeathNoteEnergyTurretProjectileComponent>(projectileUid);
            deathNoteProjectile.Target = target;
        }
    }

    private void OnEnergyTurretProjectileHit(
        Entity<DeathNoteEnergyTurretProjectileComponent> ent,
        ref ProjectileHitEvent args)
    {
        if (args.Target != ent.Comp.Target)
            return;

        if (!TryComp(args.Target, out DeathNoteFactionOverrideComponent? targetOverride) ||
            targetOverride.EnergyTurretHitsRemaining <= 0 ||
            targetOverride.EnergyTurretDamageRemaining.Empty)
        {
            args.Damage = new DamageSpecifier();
            return;
        }

        args.Damage =
            targetOverride.EnergyTurretDamageRemaining / targetOverride.EnergyTurretHitsRemaining;
        targetOverride.EnergyTurretDamageRemaining -= args.Damage;
        targetOverride.EnergyTurretHitsRemaining--;

        if (TryComp(ent, out ProjectileComponent? projectile))
            projectile.IgnoreResistances = true;
    }

    private void OnMobStateChanged(
        Entity<DeathNoteFactionOverrideComponent> ent,
        ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            RemComp<DeathNoteFactionOverrideComponent>(ent.Owner);
    }

    private void OnOverrideShutdown(
        Entity<DeathNoteFactionOverrideComponent> ent,
        ref ComponentShutdown args)
    {
        foreach (var turret in ent.Comp.EnergyTurrets)
        {
            if (Deleted(turret))
                continue;

            _factions.DeAggroEntity(turret, ent.Owner);
            if (TryComp(turret, out DeathNoteEnergyTurretComponent? deathNoteTurret))
                deathNoteTurret.Targets.Remove(ent.Owner);
        }

        if (MetaData(ent.Owner).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (!TryComp(ent.Owner, out NpcFactionMemberComponent? factionMember))
            return;

        _factions.ClearFactions((ent.Owner, factionMember), dirty: false);
        if (ent.Comp.OwnsFactionComponent)
        {
            RemComp<NpcFactionMemberComponent>(ent.Owner);
            return;
        }

        _factions.AddFactions((ent.Owner, factionMember), ent.Comp.OriginalFactions);
    }
}
