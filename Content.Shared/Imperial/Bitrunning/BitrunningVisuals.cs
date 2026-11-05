using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

[Serializable, NetSerializable]
public enum BitrunningVisuals : byte
{
    /// <summary>Строка-состояние спрайта (qserver_on, netpod_closed_active…).</summary>
    State,
    /// <summary>Байтфорж: частицы «злые» (красные).</summary>
    Angry,
    /// <summary>Байтфорж: вспышка материализации.</summary>
    Flicker,
}
