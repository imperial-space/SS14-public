using Content.Server.Popups;
using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Imperial.Xenobiology.EntityEffects;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Система зелья телепортации ксенобиологии.
///
/// Первое применение: запоминает текущие координаты, добавляет XenoTeleportSavedComponent.
/// Второе применение: телепортирует обратно, удаляет компонент.
/// </summary>
public sealed partial class XenoTeleportPotionSystem
    : EntityEffectSystem<MetaDataComponent, XenoTeleportPotionEffect>
{
    [Dependency] private readonly TransformSystem    _transform = default!;
    [Dependency] private readonly PopupSystem        _popup     = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<XenoTeleportPotionEffect> args)
    {
        var uid = entity.Owner;

        if (!TryComp<XenoTeleportSavedComponent>(uid, out var saved))
        {
            // Первое использование — сохраняем точку
            var coords = _transform.GetMapCoordinates(uid);
            var comp = AddComp<XenoTeleportSavedComponent>(uid);
            comp.SavedCoordinates = coords;
            _popup.PopupEntity(
                Loc.GetString("xeno-teleport-potion-saved"),
                uid, PopupType.MediumCaution);
        }
        else
        {
            // Второе использование — телепортируем обратно
            var dest = saved.SavedCoordinates;
            RemComp<XenoTeleportSavedComponent>(uid);
            _transform.SetMapCoordinates(uid, dest);
            _popup.PopupEntity(
                Loc.GetString("xeno-teleport-potion-returned"),
                uid, PopupType.Large);
        }
    }
}
