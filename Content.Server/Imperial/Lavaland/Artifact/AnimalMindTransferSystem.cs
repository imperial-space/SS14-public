using Content.Server.Actions;
using Content.Shared.Imperial.Lavaland.Artifact;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Lavaland.Artifact;

/// <summary>
/// Система обработки возврата сознания из животного в оригинальное тело
/// </summary>
public sealed class AnimalMindTransferSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();

        // Подписываемся на событие возврата сознания
        SubscribeLocalEvent<AnimalMindTransferComponent, RevertAnimalMindTransferEvent>(OnRevertMindTransfer);
    }

    /// <summary>
    /// Обработчик события возврата сознания
    /// </summary>
    private void OnRevertMindTransfer(EntityUid uid, AnimalMindTransferComponent comp, RevertAnimalMindTransferEvent args)
    {
        if (comp.OriginalBody == null || !Exists(comp.OriginalBody.Value) || TerminatingOrDeleted(comp.OriginalBody.Value))
            return;

        // TODO: Реализовать передачу управления/сознания обратно в оригинальное тело
        // Это может включать:
        // 1. Передачу контроля игроока обратно в оригинальное тело
        // 2. Удаление животного или превращение его в NPC
        // 3. Восстановление состояния оригинального тела

        Log.Debug($"AnimalMindTransferSystem: Сознание возвращено из {uid} в {comp.OriginalBody}");
        
        // Удаляем компонент
        RemComp<AnimalMindTransferComponent>(uid);
    }
}
