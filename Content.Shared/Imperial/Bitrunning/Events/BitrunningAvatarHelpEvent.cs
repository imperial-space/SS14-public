using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>avatar_domain_info: окно AvatarHelp с подсказкой домена.</summary>
[Serializable, NetSerializable]
public sealed class BitrunningAvatarHelpEvent(string help) : EntityEventArgs
{
    public string Help = help;
}
