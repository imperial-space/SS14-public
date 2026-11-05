using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>Evolve: у личинки — выбор касты, у трутня и преторианца — следующая ступень.</summary>
public sealed partial class XenoEvolveActionEvent : XenoInstantActionEvent;
