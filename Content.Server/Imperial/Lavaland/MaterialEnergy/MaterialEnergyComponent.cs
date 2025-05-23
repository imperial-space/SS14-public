using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.MaterialEnergy;

[RegisterComponent]
public sealed partial class MaterialEnergyComponent : Component
{
    /// <summary>
    /// All materials. For example: "Gold", "Steel" and so on
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string>? MaterialWhiteList;

    /// <summary>
    /// The standard sound when replenishing energy.
    /// </summary>
    [DataField]
    public SoundSpecifier ReplenishmentOfFirearm { get; set; } = new SoundPathSpecifier("/Audio/Imperial/Lavaland/PlasmaCutter/replenishment-of-the-firearm.ogg");
}
