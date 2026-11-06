using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>ordnance/explosive: взрыв радиусом не меньше заданного на карте сервера.</summary>
public sealed partial class TgExplosionRequirement : TgExperimentRequirement
{
    [DataField]
    public int MinRadius = 5;
}
