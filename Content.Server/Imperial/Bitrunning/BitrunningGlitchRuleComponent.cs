using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Правило «Глюк битраннинга» (event.dm): мутирует существо в активном домене.</summary>
[RegisterComponent]
public sealed partial class BitrunningGlitchRuleComponent : Component
{
    /// <summary>Роль по выбору админа (прототип спавнера); null — случайная по угрозе.</summary>
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId? ForcedSpawner;
}
