using System.Numerics;
using Content.Server.Imperial.Blob.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Blob;

public sealed class BlobLauncherSystem : EntitySystem
{
    private const string BlobAttackSound = "/Audio/Imperial/blob/sound_effects_attackblob.ogg";

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BlobChemistrySystem _chemistry = default!;
    [Dependency] private readonly BlobInfectionSystem _infection = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlobLauncherComponent, BlobStructureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var launcher, out var structure, out var xform))
        {
            if (structure.OwnerMind is not { } blobId)
                continue;

            launcher.AttackAccumulator += frameTime;
            if (launcher.AttackAccumulator < launcher.AttackInterval)
                continue;

            launcher.AttackAccumulator -= launcher.AttackInterval;

            if (!TryFindTarget(xform.Coordinates, launcher.AttackRange, out var target))
                continue;

            var chemical = GetChemicalForBlob(blobId);
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Blunt", launcher.BaseDamage);

            switch (chemical)
            {
                case BlobChemicalType.Toxin:
                    damage.DamageDict.Add("Poison", 4);
                    break;
                case BlobChemicalType.Incendiary:
                    damage.DamageDict.Add("Heat", 5);
                    break;
                case BlobChemicalType.Electromagnetic:
                    damage.DamageDict.Add("Heat", 6);
                    break;
                case BlobChemicalType.DistributedNeurons:
                    damage.DamageDict.Add("Poison", 5);
                    break;
                case BlobChemicalType.KineticGelatin:
                    damage.DamageDict.Add("Stamina", 10);
                    break;
                case BlobChemicalType.RadioactiveGel:
                    damage.DamageDict.Add("Poison", 3);
                    damage.DamageDict.Add("Radiation", 3);
                    break;
                case BlobChemicalType.LexorinJelly:
                    damage.DamageDict.Add("Asphyxiation", 8);
                    break;
                case BlobChemicalType.CryogenicLiquid:
                    damage.DamageDict.Add("Cold", 4);
                    damage.DamageDict.Add("Stamina", 8);
                    break;
                case BlobChemicalType.Sorium:
                    damage.DamageDict.Add("Stamina", 9);
                    break;
                case BlobChemicalType.EnvenomedFilaments:
                    damage.DamageDict.Add("Poison", 5);
                    damage.DamageDict.Add("Stamina", 6);
                    break;
                case BlobChemicalType.ParalyticToxins:
                    damage.DamageDict.Add("Poison", 4);
                    damage.DamageDict.Add("Stamina", 4);
                    break;
                case BlobChemicalType.Regenerative:
                    damage.DamageDict.Add("Poison", 2);
                    break;
            }

            if (!_damage.TryChangeDamage(target, damage, true, origin: uid))
                continue;

            Spawn("BlobAttackEffect", Transform(target).Coordinates);
            _audio.PlayPvs(BlobAttackSound, target);
            ApplySecondaryEffects(target, xform.Coordinates, blobId, chemical);
            Dirty(uid, launcher);
        }
    }

    private void ApplySecondaryEffects(EntityUid targetUid, EntityCoordinates sourceCoordinates, EntityUid blobId, BlobChemicalType chemical)
    {
        if (!TryComp<MobStateComponent>(targetUid, out var mobState) || mobState.CurrentState == Shared.Mobs.MobState.Dead)
            return;

        switch (chemical)
        {
            case BlobChemicalType.Toxin:
                _chemistry.ApplyChemicalEffect(targetUid, chemical, 4f);
                EnsureInfection(targetUid, blobId, chemical, 8f);
                break;
            case BlobChemicalType.Incendiary:
                _chemistry.ApplyChemicalEffect(targetUid, chemical, 4f);
                break;
            case BlobChemicalType.Electromagnetic:
                _chemistry.ApplyChemicalEffect(targetUid, chemical, 3f);
                break;
            case BlobChemicalType.DistributedNeurons:
                _chemistry.ApplyChemicalEffect(targetUid, chemical, 3f);
                EnsureInfection(targetUid, blobId, chemical, 3f);
                break;
            case BlobChemicalType.RadioactiveGel:
                _chemistry.ApplyChemicalEffect(targetUid, chemical, 5f);
                break;
            case BlobChemicalType.LexorinJelly:
                _chemistry.ApplyChemicalEffect(targetUid, chemical, 5f);
                break;
            case BlobChemicalType.CryogenicLiquid:
                _chemistry.ApplyChemicalEffect(targetUid, chemical, 4f);
                break;
            case BlobChemicalType.Sorium:
                ApplySoriumKnockback(sourceCoordinates, targetUid, 2.25f);
                break;
            case BlobChemicalType.EnvenomedFilaments:
                _chemistry.ApplyChemicalEffect(targetUid, chemical, 5f);
                _stun.TryAddStunDuration(targetUid, TimeSpan.FromSeconds(1.25));
                break;
            case BlobChemicalType.ParalyticToxins:
                _chemistry.ApplyChemicalEffect(targetUid, chemical, 4f);
                _stun.TryKnockdown(targetUid, TimeSpan.FromSeconds(1.0), true);
                break;
        }
    }

    private void ApplySoriumKnockback(EntityCoordinates sourceCoordinates, EntityUid targetUid, float distance)
    {
        var sourceCoords = _transform.ToMapCoordinates(sourceCoordinates);
        var targetCoords = _transform.ToMapCoordinates(Transform(targetUid).Coordinates);
        if (sourceCoords.MapId != targetCoords.MapId)
            return;

        var pushDir = targetCoords.Position - sourceCoords.Position;
        if (pushDir.LengthSquared() < 0.01f)
            pushDir = new Vector2(1f, 0f);
        else
            pushDir = pushDir.Normalized() * distance;

        _throwing.TryThrow(targetUid, pushDir, 8f);
    }

    private bool TryFindTarget(EntityCoordinates origin, float range, out EntityUid targetUid)
    {
        targetUid = EntityUid.Invalid;
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(origin, range, nearby);
        var originCoords = _transform.ToMapCoordinates(origin);
        var bestDistance = float.MaxValue;

        foreach (var entity in nearby)
        {
            if (HasComp<BlobStructureComponent>(entity) || HasComp<BlobMobComponent>(entity) || HasComp<BlobOvermindComponent>(entity))
                continue;

            if (!TryComp<MobStateComponent>(entity, out var mobState) || mobState.CurrentState == Shared.Mobs.MobState.Dead)
                continue;

            if (!TryComp<DamageableComponent>(entity, out _) || !TryComp(entity, out TransformComponent? xform))
                continue;

            var targetCoords = _transform.ToMapCoordinates(xform.Coordinates);
            if (targetCoords.MapId != originCoords.MapId)
                continue;

            var distance = (targetCoords.Position - originCoords.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            targetUid = entity;
        }

        return targetUid != EntityUid.Invalid;
    }

    private BlobChemicalType GetChemicalForBlob(EntityUid blobId)
    {
        var query = EntityQueryEnumerator<BlobOvermindComponent>();
        while (query.MoveNext(out _, out var overmind))
        {
            if (overmind.BlobId == blobId)
                return overmind.Chemical;
        }

        return BlobChemicalType.Toxin;
    }

    private void EnsureInfection(EntityUid targetUid, EntityUid blobId, BlobChemicalType chemical, float transformDelay)
    {
        _infection.EnsurePendingInfection(targetUid, blobId, chemical, transformDelay);
    }
}