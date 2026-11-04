using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>obj/item/slime_cookie.</summary>
[RegisterComponent]
public sealed partial class SlimeCookieComponent : Component
{
    /// <summary>Цвет-эффект печенья (grey, orange, ...).</summary>
    [DataField(required: true)]
    public string Effect = string.Empty;

    [DataField]
    public float Nutrition = 5f;

    [DataField]
    public LocId Taste = "slime-cookie-taste-grey";

    /// <summary>pyrite: краска.</summary>
    [ViewVariables]
    public Color? Dye;
}
