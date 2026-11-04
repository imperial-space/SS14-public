using Content.Shared.Imperial.Security;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>Принтер плакатов на консоли уголовных записей.</summary>
[RegisterComponent]
public sealed partial class WantedPrinterComponent : Component
{
    [ViewVariables]
    public TimeSpan? PrintAt;

    [ViewVariables]
    public WantedPrintMessage? Pending;
}
