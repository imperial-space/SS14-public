using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Flesh;

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

    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public float EmpRange = 5f;

    [DataField]
    public float EmpEnergyConsumption = 300f;
}
