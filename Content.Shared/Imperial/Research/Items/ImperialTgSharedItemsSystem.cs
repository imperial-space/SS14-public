using Content.Shared.Doors;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>
/// Общая часть предметов TG, которую должен знать и клиент (перетаскивание тела в мясорубку).
/// </summary>
public sealed class ImperialTgSharedItemsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialGibberComponent, CanDropTargetEvent>(OnGibberCanDrop);
        SubscribeLocalEvent<ImperialSealedDoorComponent, BeforeDoorOpenedEvent>(OnSealedDoorOpen);
    }

    private void OnGibberCanDrop(Entity<ImperialGibberComponent> ent, ref CanDropTargetEvent args)
    {
        args.CanDrop = HasComp<MobStateComponent>(args.Dragged);
        args.Handled = true;
    }

    /// <summary>Затвор держит дверь закрытой; проверка общая, чтобы клиент не предсказывал открытие.</summary>
    private void OnSealedDoorOpen(Entity<ImperialSealedDoorComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        args.Cancel();
    }
}
