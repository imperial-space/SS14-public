using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticStalkerComponent : Component
{
    [DataField]
    public EntityUid? Master;

    [DataField]
    public List<string> AnimalPolymorphs = new()
    {
        "StalkerPolymorphCat",
        "StalkerPolymorphMouse",
        "StalkerPolymorphCockroach",
    };

    [DataField]
    public List<string> GolemPolymorphs = new()
    {
        "StalkerPolymorphBorg",
        "StalkerPolymorphDrone",
    };
}
