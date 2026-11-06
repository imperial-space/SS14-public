using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Interaction.Events;

namespace Content.Shared.Imperial.Blob;

/// <summary>
/// Оверманд не взаимодействует с миром руками: его клики — это expand/shield/rally/remove
/// (обрабатываются на сервере до обычного взаимодействия). Так клиент не предсказывает лишнего.
/// </summary>
public sealed class SharedBlobOvermindSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobOvermindComponent, InteractionAttemptEvent>(OnInteractionAttempt);
    }

    private void OnInteractionAttempt(Entity<BlobOvermindComponent> ent, ref InteractionAttemptEvent args)
    {
        // Свой интерфейс (смена штамма, узлы) оставляем доступным.
        if (args.Target != ent.Owner)
            args.Cancelled = true;
    }
}
