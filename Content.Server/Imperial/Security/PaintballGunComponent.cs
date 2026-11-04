using Content.Shared.Imperial.Security;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Пейнтбол ──────────────────────────────────────────────────────────────

/// <summary>gun/projectile/automatic/paintball_gun.</summary>
[RegisterComponent]
public sealed partial class PaintballGunComponent : Component
{
    /// <summary>paintball_color: синий, красный, зелёный, жёлтый.</summary>
    [DataField]
    public int ColorIndex;

    /// <summary>emagged: перегруженный компрессор — шарики бьют на 8.</summary>
    [ViewVariables]
    public bool Emagged;
}
