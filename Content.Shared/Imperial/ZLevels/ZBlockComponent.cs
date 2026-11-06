using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>
/// Флаги obj_flags SS13, влияющие на движение между этажами: BLOCK_Z_OUT_DOWN, BLOCK_Z_IN_UP и т. д.
/// Решётка (lattice) и трап — <see cref="OutDown"/>; мостик (catwalk) — <see cref="OutDown"/> и <see cref="InUp"/>.
/// </summary>
[RegisterComponent]
public sealed partial class ZBlockComponent : Component
{
    /// <summary>Не даёт упасть с этой клетки вниз.</summary>
    [DataField]
    public bool OutDown;

    /// <summary>Не даёт подняться с этой клетки вверх.</summary>
    [DataField]
    public bool OutUp;

    /// <summary>Не даёт упасть сверху на эту клетку.</summary>
    [DataField]
    public bool InDown;

    /// <summary>Не даёт подняться снизу на эту клетку.</summary>
    [DataField]
    public bool InUp;
}
