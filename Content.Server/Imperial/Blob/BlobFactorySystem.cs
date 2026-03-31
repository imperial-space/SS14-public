using Content.Server.Imperial.Blob.Components;
using Content.Shared.Imperial.Blob.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Blob;

public sealed class BlobFactorySystem : EntitySystem
{
    private const string BlobGrowSound = "/Audio/Imperial/blob/sound_effects_splat.ogg";

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BlobMobSystem _blobMob = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobSporeComponent, ComponentShutdown>(OnSporeShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var factories = EntityQueryEnumerator<BlobFactoryComponent, BlobStructureComponent, TransformComponent>();
        while (factories.MoveNext(out var uid, out var factory, out var structure, out var xform))
        {
            if (structure.OwnerMind is not { } mindId)
                continue;

            factory.ActiveSpores.RemoveWhere(spore => Deleted(spore));

            factory.SpawnAccumulator += frameTime;
            if (factory.SpawnAccumulator < factory.SpawnInterval)
                continue;

            if (factory.ActiveSpores.Count >= factory.MaxActiveSpores)
                continue;

            factory.SpawnAccumulator -= factory.SpawnInterval;

            var spore = Spawn(factory.SporePrototype, xform.Coordinates);
            factory.ActiveSpores.Add(spore);
            _audio.PlayPvs(BlobGrowSound, spore);

            if (TryComp<BlobSporeComponent>(spore, out var sporeComp))
            {
                sporeComp.SourceFactory = uid;
                Dirty(spore, sporeComp);
            }

            if (TryComp<BlobMobComponent>(spore, out var blobMob))
            {
                blobMob.OwnerMind = mindId;
                _blobMob.ConfigureMobForOwner(spore, blobMob);
                Dirty(spore, blobMob);
            }

            Dirty(uid, factory);
        }
    }

    private void OnSporeShutdown(EntityUid uid, BlobSporeComponent component, ComponentShutdown args)
    {
        if (component.SourceFactory is not { } factoryUid)
            return;

        if (!TryComp<BlobFactoryComponent>(factoryUid, out var factory))
            return;

        if (!factory.ActiveSpores.Remove(uid))
            return;

        Dirty(factoryUid, factory);
    }
}