using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;

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

    [DataField]
    public TimeSpan CookieInteractDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan CookieEffectSlimeOrangeDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CookieEffectSlimeBlueDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CookieEffectSlimeMetalDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CookieEffectSlimeYellowDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan CookieEffectSlimeDarkPurpleDuration = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan CookieEffectSlimeSepiaDuration = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan CookieEffectSlimePinkDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan CookieEffectSlimeOilDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CookieEffectSlimeBlackDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan CookieEffectSlimeLightPinkDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CookieEffectSlimeAdamantineDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public float CookieEffectSlimeSilverAmount = 10f;

    [DataField]
    public int CookieEffectSlimeBluespaceRange = 10;

    [DataField]
    public float CookieEffectSlimeCeruleanChance = 0.5f;

    [DataField]
    public FixedPoint2 CookieEffectSlimeRedQuantity = 5;

    [DataField]
    public FixedPoint2 CookieEffectSlimeRedAmount = 25;
}
