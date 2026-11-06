using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Armor;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Blob;

/// <summary>Реагент штамма при контакте (облако спор) действует как атака блоба.</summary>
public sealed class BlobStrainExposureEffectSystem : EntityEffectSystem<MobStateComponent, BlobStrainExposure>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly BlobStrainSystem _strain = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<BlobStrainExposure> args)
    {
        if (!_proto.TryIndex(args.Effect.Strain, out var strain))
            return;

        _strain.ExposeMob(entity, strain, args.Scale, _strain.GetBioProtection(entity), null, primary: false);
    }
}
