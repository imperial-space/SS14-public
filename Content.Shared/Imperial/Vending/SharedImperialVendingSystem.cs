using Content.Shared.UserInterface;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>
/// Окно с ценами открывается по своему ключу: ванильный клиент ищет под VendingMachineUiKey
/// только VendingMachineBoundUserInterface. Ключ ActivatableUI не синхронизируется, поэтому меняется на обеих сторонах.
/// </summary>
public sealed class SharedImperialVendingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialVendingComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<ImperialVendingComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<ActivatableUIComponent>(ent, out var activatable))
            activatable.Key = ImperialVendingUiKey.Key;
    }
}
