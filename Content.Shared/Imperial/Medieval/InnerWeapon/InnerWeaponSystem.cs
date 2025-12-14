using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Containers;

namespace Content.Shared.Imperial.Medieval.Weapons;

public sealed partial class InnerWeaponSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public void SetWeapon(EntityUid uid, string id, InnerWeaponComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        if (id != "" && (!_container.TryGetContainer(uid, id, out var container) || container is not ContainerSlot slot))
            return;

        comp.Current = id;
        Dirty(uid, comp);
    }

    public bool TryGetInnerWeapon(EntityUid uid, [NotNullWhen(true)] out EntityUid? weapon, out string cont, InnerWeaponComponent? comp = null)
    {
        weapon = null;
        cont = "";

        if (!Resolve(uid, ref comp, false))
            return false;

        cont = comp.Current;

        if (!_container.TryGetContainer(uid, comp.Current, out var container) || container is not ContainerSlot slot || !slot.ContainedEntity.HasValue)
            return false;

        weapon = slot.ContainedEntity.Value;
        return true;
    }
}
