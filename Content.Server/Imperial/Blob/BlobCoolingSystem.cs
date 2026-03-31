using Content.Server.Imperial.Blob.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Blob.Components;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Blob;

public sealed class BlobCoolingSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlobCoolingComponent, BlobStructureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var cooling, out var structure, out var xform))
        {
            if (structure.OwnerMind is not { } blobId)
                continue;

            cooling.EffectAccumulator += frameTime;
            if (cooling.EffectAccumulator < cooling.EffectInterval)
                continue;

            cooling.EffectAccumulator -= cooling.EffectInterval;
            ApplyCooling(uid, blobId, cooling, xform.Coordinates);
            Dirty(uid, cooling);
        }
    }

    private void ApplyCooling(EntityUid source, EntityUid blobId, BlobCoolingComponent cooling, EntityCoordinates origin)
    {
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(origin, cooling.EffectRange, nearby);

        foreach (var entity in nearby)
        {
            if (!IsFriendlyBlobEntity(entity, blobId))
                continue;

            if (!TryComp<DamageableComponent>(entity, out _))
                continue;

            var healing = new DamageSpecifier();
            healing.DamageDict.Add("Burn", -cooling.BurnHeal);
            healing.DamageDict.Add("Heat", -cooling.BurnHeal);
            _damage.TryChangeDamage(entity, healing, true, origin: source);
        }
    }

    private bool IsFriendlyBlobEntity(EntityUid entity, EntityUid blobId)
    {
        if (TryComp<BlobStructureComponent>(entity, out var structure))
            return structure.OwnerMind == blobId;

        if (TryComp<BlobMobComponent>(entity, out var mob))
            return mob.OwnerMind == blobId;

        if (TryComp<BlobOvermindComponent>(entity, out var overmind))
            return overmind.BlobId == blobId;

        return false;
    }
}