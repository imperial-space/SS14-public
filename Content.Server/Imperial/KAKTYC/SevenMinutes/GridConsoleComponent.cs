using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.KAKTYC.GridConsole;

[RegisterComponent]
[Access(typeof(GridConsoleSystem))]
public sealed partial class GridConsoleComponent : Component
{
    [ViewVariables]
    public string DiskContainer = "TeleportDiskSlot";
    [ViewVariables]
    public float TargetPosX;
    [ViewVariables]
    public float TargetPosY;
    [ViewVariables]
    public float TargetMap;
    [DataField]
    public bool IsBlock = true;
    [ViewVariables]
    public SoundSpecifier EmpSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");
}
