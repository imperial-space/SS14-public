using Content.Shared.Imperial.Security;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Плакаты розыска ───────────────────────────────────────────────────────

/// <summary>Данные плаката: и у свёрнутого, и у висящего на стене.</summary>
[RegisterComponent]
public sealed partial class WantedPosterComponent : Component
{
    [DataField]
    public string WantedName = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public string Header = string.Empty;

    [DataField]
    public bool Missing;

    /// <summary>Свёрнутый плакат (obj/item/poster/wanted) или висящий (structure/sign/poster/wanted).</summary>
    [DataField]
    public bool Rolled = true;

    [DataField]
    public TimeSpan AfterInteractDelay = TimeSpan.FromSeconds(2);
}
