using Content.Shared.Item;
using Content.Shared.Tag;
using Robust.Shared.GameStates;

namespace Content.Shared.Borgs.Imperial;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BorgHandsImperialComponent : Component // Компонент для вайтлиста боргов Империала
{
    [DataField("whitelistHandTag"), AutoNetworkedField]
    public string WhitelistHandTag { get; set; } = string.Empty; // Поле для ввода кастомного тега
}

public sealed partial class BorgHandsImperialSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgHandsImperialComponent, PickupAttemptEvent>(OnPickupAttempt);
    }


    public void OnPickupAttempt(EntityUid uid, BorgHandsImperialComponent component, PickupAttemptEvent args)
    {
        if (_tagSystem.HasAnyTag(args.Item, component.WhitelistHandTag))
        {
            return;
        }

        args.Cancel();
    }

}