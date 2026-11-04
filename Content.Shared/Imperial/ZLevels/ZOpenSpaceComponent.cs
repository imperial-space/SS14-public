using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Невидимая метка клетки открытого пространства: через неё срабатывает падение (StepTrigger).
/// Расставляется системой сама по тайлам открытого пространства и не сохраняется в карту.
/// </summary>
[RegisterComponent]
public sealed partial class ZOpenSpaceComponent : Component;
