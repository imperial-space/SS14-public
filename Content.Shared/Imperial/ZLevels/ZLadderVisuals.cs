using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

[Serializable, NetSerializable]
public enum ZLadderVisuals : byte
{
    /// <summary>Состояние спрайта ladderXY: X — есть трап выше, Y — есть трап ниже.</summary>
    State,
}
