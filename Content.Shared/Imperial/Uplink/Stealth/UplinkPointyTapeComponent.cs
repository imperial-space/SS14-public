using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Суперострая лента: делает предмет застревающим при броске.</summary>
[RegisterComponent]
public sealed partial class UplinkPointyTapeComponent : Component
{
    /// <summary>Колющий урон при попадании обмотанным предметом (у липкой ленты TG — 0).</summary>
    [DataField]
    public float Damage = 10f;

    /// <summary>Время, за которое вонзившийся предмет можно вытащить.</summary>
    [DataField]
    public float RemovalTime = 3f;
}
