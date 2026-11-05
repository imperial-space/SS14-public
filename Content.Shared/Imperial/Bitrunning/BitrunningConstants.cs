using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

public static class BitrunningConstants
{
    public const int CostNone = 0;
    public const int CostLow = 1;
    public const int CostMedium = 2;
    public const int CostHigh = 3;
    public const int CostExtreme = 20;

    /// <summary>Нетподы, байтфоржи и ящики ищут сервер в этом радиусе (oview(4)).</summary>
    public const float LinkRange = 4.5f;
}
