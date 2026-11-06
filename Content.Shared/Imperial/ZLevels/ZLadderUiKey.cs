using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>Радиальное меню лестницы, когда можно и вверх, и вниз (как в SS13).</summary>
[Serializable, NetSerializable]
public enum ZLadderUiKey : byte
{
    Key,
}
