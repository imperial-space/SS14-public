using Content.Shared.Imperial.Security;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>datum/bounty/patrol и datum/bounty/item/contraband.</summary>
public sealed class SecurityBounty
{
    public SecurityBountyKind Kind;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Reward;

    // Патруль.
    public EntityUid Grid;
    public EntityCoordinates Center;
    public string Area = string.Empty;
    public int NeededCoverage;
    public readonly Dictionary<Vector2i, int> WalkedTiles = new();
    public Vector2i? LastTile;
    public int Alerted;

    // Контрабанда.
    public int Required;
    public int Shipped;

    public int Progress
    {
        get
        {
            var total = 0;
            foreach (var count in WalkedTiles.Values)
            {
                total += count;
            }

            return total;
        }
    }

    public bool CanClaim => Kind == SecurityBountyKind.Patrol ? Progress >= NeededCoverage : Shipped >= Required;
}
