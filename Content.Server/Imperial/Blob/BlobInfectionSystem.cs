using System;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Shared.Administration.Systems;
using Content.Server.Imperial.Blob.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Server.Imperial.Blob;

public sealed class BlobInfectionSystem : EntitySystem
{
    private const string BlobFactionId = "Blob";
    private const string BlobGrowSound = "/Audio/Imperial/blob/sound_effects_splat.ogg";

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BlobMobSystem _blobMob = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobSporeComponent, MapInitEvent>(OnBlobVacuumMobMapInit);
        SubscribeLocalEvent<BlobbernautComponent, MapInitEvent>(OnBlobVacuumMobMapInit);
        SubscribeLocalEvent<BlobSporeComponent, BlobSporeLatchDoAfterEvent>(OnSporeLatchDoAfter);
    }

    private void OnBlobVacuumMobMapInit<TComponent>(EntityUid uid, TComponent component, MapInitEvent args)
        where TComponent : Component
    {
        EnsureBlobVacuumSurvival(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var infectedQuery = EntityQueryEnumerator<BlobInfectedComponent>();
        while (infectedQuery.MoveNext(out var infectedUid, out var infected))
        {
            if (infected.OwnerMind is not { })
                continue;

            if (_npcFaction.IsMember(infectedUid, BlobFactionId))
                continue;

            if (TryComp<MobStateComponent>(infectedUid, out var mobState) && mobState.CurrentState == MobState.Dead)
                continue;

            infected.TransformAccumulator += frameTime;
            if (infected.TransformAccumulator < infected.TransformDelay)
                continue;

            FinalizeInfection(infectedUid, infected);
        }

        var spores = EntityQueryEnumerator<BlobSporeComponent, BlobMobComponent, TransformComponent>();
        while (spores.MoveNext(out var uid, out var spore, out var blobMob, out var xform))
        {
            if (blobMob.OwnerMind is not { })
                continue;

            var priorityTarget = FindPriorityTarget(uid, xform, spore);
            if (priorityTarget is { } targetUid)
            {
                var changedTarget = spore.LatchTarget != targetUid;
                if (changedTarget)
                {
                    if (spore.LatchInProgress && TryComp<HTNComponent>(uid, out var wakingHtn))
                    {
                        _npc.WakeNPC(uid, wakingHtn);
                        _htn.Replan(wakingHtn);
                    }

                    spore.LatchTarget = targetUid;
                    spore.LatchInProgress = false;
                    Dirty(uid, spore);
                }

                if (changedTarget && TryComp<HTNComponent>(uid, out var htn))
                {
                    htn.Blackboard.SetValue(NPCBlackboard.CurrentOrderedTarget, targetUid);
                    _npc.WakeNPC(uid, htn);
                    _htn.Replan(htn);
                }

                if (CanLatchToTarget(uid, targetUid, xform, spore))
                {
                    if (!spore.LatchInProgress && StartLatchDoAfter(uid, targetUid, spore))
                    {
                        if (TryComp<HTNComponent>(uid, out var activeHtn))
                        {
                            activeHtn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);
                            _htn.Replan(activeHtn);
                        }
                    }
                }
            }
            else if (spore.LatchTarget != null || spore.LatchInProgress)
            {
                if (spore.LatchInProgress && TryComp<HTNComponent>(uid, out var wakingHtn))
                {
                    _npc.WakeNPC(uid, wakingHtn);
                    _htn.Replan(wakingHtn);
                }

                spore.LatchTarget = null;
                spore.LatchInProgress = false;
                Dirty(uid, spore);

                if (TryComp<HTNComponent>(uid, out var htn) &&
                    htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget))
                {
                    _htn.Replan(htn);
                }
            }
        }
    }

    private void TryAttachBlobHelmet(EntityUid target)
    {
        EntityUid? oldHead = null;
        if (_inventory.TryUnequip(target, target, "head", out var unequipped, silent: true, force: true))
            oldHead = unequipped;

        var helmet = Spawn("ClothingHeadHelmetBlob", Transform(target).Coordinates);
        if (_inventory.TryEquip(target, target, helmet, "head", silent: true, force: true))
            return;

        QueueDel(helmet);

        if (oldHead != null && Exists(oldHead.Value))
            _inventory.TryEquip(target, target, oldHead.Value, "head", silent: true, force: true);
    }

    public bool TryLatchSporeOntoTarget(EntityUid sporeUid, EntityUid target, EntityUid ownerMind, BlobChemicalType chemical)
    {
        if (!IsValidInfectionTarget(target, sporeUid))
            return false;

        TryAttachBlobHelmet(target);
        ConfigureBlobAlly(target, ownerMind, chemical);
        _rejuvenate.PerformRejuvenate(target);
        _audio.PlayPvs(BlobGrowSound, target);
        _popup.PopupEntity(Loc.GetString("blob-infection-started"), target, target, PopupType.SmallCaution);
        QueueDel(sporeUid);
        return true;
    }

    public void EnsurePendingInfection(EntityUid target, EntityUid ownerMind, BlobChemicalType chemical, float transformDelay)
    {
        if (HasComp<BlobMobComponent>(target) ||
            HasComp<BlobOvermindComponent>(target) ||
            HasComp<BlobStructureComponent>(target) ||
            _npcFaction.IsMember(target, BlobFactionId))
        {
            return;
        }

        if (TryComp<MobStateComponent>(target, out var mobState) && mobState.CurrentState == MobState.Dead)
            return;

        var firstInfection = !TryComp<BlobInfectedComponent>(target, out var infected);
        infected ??= EnsureComp<BlobInfectedComponent>(target);
        infected.OwnerMind = ownerMind;
        infected.Chemical = chemical;
        infected.TransformDelay = firstInfection
            ? transformDelay
            : Math.Min(infected.TransformDelay, transformDelay);
        Dirty(target, infected);

        if (firstInfection)
            _popup.PopupEntity(Loc.GetString("blob-infection-started"), target, target, PopupType.SmallCaution);
    }

    private bool StartLatchDoAfter(EntityUid sporeUid, EntityUid target, BlobSporeComponent spore)
    {
        var doAfter = new DoAfterArgs(EntityManager, sporeUid, TimeSpan.FromSeconds(spore.LatchDuration), new BlobSporeLatchDoAfterEvent(), sporeUid, target: target, used: sporeUid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = false,
            DistanceThreshold = spore.InfectionRange,
            BlockDuplicate = false,
            CancelDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        if (TryComp<HTNComponent>(sporeUid, out var htn))
            _npc.SleepNPC(sporeUid, htn);

        if (TryComp<PhysicsComponent>(sporeUid, out var physics))
            _physics.SetLinearVelocity(sporeUid, Vector2.Zero, body: physics);

        spore.LatchInProgress = true;
        Dirty(sporeUid, spore);
        return true;
    }

    private void OnSporeLatchDoAfter(EntityUid uid, BlobSporeComponent spore, BlobSporeLatchDoAfterEvent args)
    {
        if (!args.Cancelled && TryComp<HTNComponent>(uid, out var htn))
        {
            _npc.WakeNPC(uid, htn);
            _htn.Replan(htn);
        }

        spore.LatchInProgress = false;
        Dirty(uid, spore);

        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (spore.LatchTarget != target)
            return;

        if (!TryComp<BlobMobComponent>(uid, out var blobMob) || blobMob.OwnerMind is not { } ownerMind)
            return;

        if (TryLatchSporeOntoTarget(uid, target, ownerMind, blobMob.Chemical))
            args.Handled = true;
    }

    private void ConfigureBlobAlly(EntityUid target, EntityUid ownerMind, BlobChemicalType chemical)
    {
        _npcFaction.ClearFactions(target);
        _npcFaction.AddFaction(target, BlobFactionId);

        var infected = EnsureComp<BlobInfectedComponent>(target);
        infected.OwnerMind = ownerMind;
        infected.Chemical = chemical;

        EnsureBlobVacuumSurvival(target);
        _blobMob.EnsureBlobRadio(target);
        _blobMob.ConfigureBlobFriendlyCollision(target);
    }

    private void EnsureBlobVacuumSurvival(EntityUid uid)
    {
        RemComp<RespiratorComponent>(uid);
        EnsureComp<PressureImmunityComponent>(uid);
    }

    private void FinalizeInfection(EntityUid target, BlobInfectedComponent infected)
    {
        if (infected.OwnerMind is not { } ownerMind)
            return;

        TryAttachBlobHelmet(target);
        ConfigureBlobAlly(target, ownerMind, infected.Chemical);
        _rejuvenate.PerformRejuvenate(target);
        _audio.PlayPvs(BlobGrowSound, target);
        Dirty(target, infected);
    }

    private bool CanLatchToTarget(EntityUid sporeUid, EntityUid target, TransformComponent sporeXform, BlobSporeComponent spore)
    {
        if (!IsValidInfectionTarget(target, sporeUid))
            return false;

        var sporeCoords = _transform.ToMapCoordinates(sporeXform.Coordinates);
        var targetCoords = _transform.ToMapCoordinates(Transform(target).Coordinates);
        if (sporeCoords.MapId != targetCoords.MapId)
            return false;

        return (targetCoords.Position - sporeCoords.Position).LengthSquared() <= spore.InfectionRange * spore.InfectionRange;
    }

    private EntityUid? FindPriorityTarget(EntityUid sporeUid, TransformComponent sporeXform, BlobSporeComponent spore)
    {
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(sporeXform.Coordinates, spore.TargetSearchRange, nearby);

        EntityUid? closestInfectionTarget = null;
        EntityUid? closestCombatTarget = null;
        var sporeCoords = _transform.ToMapCoordinates(sporeXform.Coordinates);
        var closestInfectionDistance = float.MaxValue;
        var closestCombatDistance = float.MaxValue;

        foreach (var candidate in nearby)
        {
            if (!IsValidSporeTarget(candidate, sporeUid))
                continue;

            var candidateCoords = _transform.ToMapCoordinates(Transform(candidate).Coordinates);
            if (candidateCoords.MapId != sporeCoords.MapId)
                continue;

            var distance = (candidateCoords.Position - sporeCoords.Position).LengthSquared();
            if (IsValidInfectionTarget(candidate, sporeUid))
            {
                if (distance >= closestInfectionDistance)
                    continue;

                closestInfectionDistance = distance;
                closestInfectionTarget = candidate;
                continue;
            }

            if (distance >= closestCombatDistance)
                continue;

            closestCombatDistance = distance;
            closestCombatTarget = candidate;
        }

        return closestInfectionTarget ?? closestCombatTarget;
    }

    private bool IsValidInfectionTarget(EntityUid target, EntityUid sporeUid)
    {
        if (!IsValidSporeTarget(target, sporeUid))
            return false;

        return TryComp<MobStateComponent>(target, out var targetMobState) &&
               targetMobState.CurrentState == MobState.Critical;
    }

    private bool IsValidSporeTarget(EntityUid target, EntityUid sporeUid)
    {
        if (target == sporeUid || HasComp<BlobMobComponent>(target) || HasComp<BlobOvermindComponent>(target) || HasComp<BlobStructureComponent>(target))
            return false;

        if (HasComp<BlobInfectedComponent>(target))
            return false;

        if (!TryComp<MobStateComponent>(target, out var targetMobState) || targetMobState.CurrentState == MobState.Dead)
            return false;

        if (_npcFaction.IsMember(target, BlobFactionId))
            return false;

        return true;
    }
}