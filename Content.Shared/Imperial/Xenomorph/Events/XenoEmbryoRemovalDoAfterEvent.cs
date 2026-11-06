using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>Извлечение эмбриона режущим инструментом (замена хирургии SS13).</summary>
[Serializable, NetSerializable]
public sealed partial class XenoEmbryoRemovalDoAfterEvent : SimpleDoAfterEvent;
