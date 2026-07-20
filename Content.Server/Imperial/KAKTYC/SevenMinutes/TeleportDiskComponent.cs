using Robust.Shared.Map;

namespace Content.Server.Imperial.KAKTYC.GridConsole;

[RegisterComponent]
[Access(typeof(TeleportDiskSystem))]
public sealed partial class TeleportDiskComponent : Component
{
    [DataField]
    public float PosX;
    [DataField]
    public float PosY;
    [ViewVariables]
    public EntityUid MapId;
    [ViewVariables]
    public float FloatId;
    [ViewVariables]
    public bool IsAlreadyUsed = false;
}
