using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>TS_DAMAGE_*: как ИИ выбирает цели.</summary>
public enum TerrorSpiderTargetMethod : byte
{
    Simple,
    Poison,
    Brute,
}
