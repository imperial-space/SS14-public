using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Исцеляющее касание (lay_on_hands): лечит цель, часть урона забирает себе.</summary>
public sealed partial class GeneticMendingTouchActionEvent : EntityTargetActionEvent;
