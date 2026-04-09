using System.Linq;
using Content.Shared.Ensnaring;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Imperial.Trigger.Components.Effects;
using Content.Shared.Trigger;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Trigger.Systems;

/// <summary>
/// Обрабатывает эффект триггера импланта свободы: снимает наручники и освобождает цель от опутывающих сущностей (болы и т.д.)
/// </summary>
public sealed class FreedomImplantOnTriggerSystem : XOnTriggerSystem<FreedomImplantbolaTriggerComponent>
{
    /// <summary> Звук при снятии болы </summary>
    private static readonly SoundSpecifier BolaBreakSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");

    [Dependency] private readonly SharedEnsnareableSystem _ensnareable = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    /// <summary>
    /// при срабатывании импланта свободы, снимает наручники и удаляет все опутывающие сущности с цели.
    /// </summary>
    protected override void OnTrigger(Entity<FreedomImplantbolaTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        // Снятие опутывания влияет на скорость движения и может вызвать рассинхрон на клиенте.
        if (_net.IsClient)
            return;

        var playedBreakSound = false;

        // Освобождаем цель от всех опутывающих сущностей (болы и т.д.)
        if (TryComp<EnsnareableComponent>(target, out var ensnareable))
        {
            var ensnares = ensnareable.Container.ContainedEntities.ToList();
            foreach (var ensnareEntity in ensnares)
            {
                if (!TryComp<EnsnaringComponent>(ensnareEntity, out var ensnaring))
                    continue;

                _ensnareable.ForceFree(ensnareEntity, ensnaring);

                // ForceFree отправляет событие снятия на опутывающую сущность. импланту также нужно отправить событие на цель, чтобы корректно снять штрафы к скорости движения.
                var ev = new EnsnareRemoveEvent(ensnaring.WalkSpeed, ensnaring.SprintSpeed);
                RaiseLocalEvent(target, ev);

                playedBreakSound = true;

                QueueDel(ensnareEntity);
            }

            // Обновляем визуальное состояние: цель больше не опутана
            if (TryComp<AppearanceComponent>(target, out var appearance))
            {
                _appearance.SetData(target, EnsnareableVisuals.IsEnsnared, ensnareable.IsEnsnared, appearance);
            }
        }

        // Воспроизводим звук разрыва у позиции цели, если было снято хотя бы одно опутывание
        if (playedBreakSound)
            _audio.PlayPvs(BolaBreakSound, target);

        args.Handled = true;
    }
}
