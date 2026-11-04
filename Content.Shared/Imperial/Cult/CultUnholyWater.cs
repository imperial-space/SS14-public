using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cult;

/// <summary>
/// datum/reagent/fuel/unholywater: культиста лечит и бодрит, остальных травит.
/// Значения — за секунду метаболизма (scale = доля от 1 с).
/// </summary>
public sealed partial class CultUnholyWater : EntityEffectBase<CultUnholyWater>;
