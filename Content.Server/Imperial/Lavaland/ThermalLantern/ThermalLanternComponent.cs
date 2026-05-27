using Content.Shared.Imperial.Lavaland.ColossusLoot;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Lavaland.ThermalLantern;

[RegisterComponent]
[Access(typeof(ThermalLanternSystem))]
public sealed partial class ThermalLanternComponent : Component
{
    [ViewVariables]
    public EntityUid? LastActivator;

    [ViewVariables]
    public EntityUid? VisionUser;
}
