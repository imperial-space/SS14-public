using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Зашифрованный ящик домена: доставить на клетку-цель.</summary>
[RegisterComponent]
public sealed partial class BitrunningEncryptedCacheComponent : Component
{
    /// <summary>true — основной ящик (crate), false — побочный (lockbox curiosity).</summary>
    [DataField]
    public bool Main = true;
}
