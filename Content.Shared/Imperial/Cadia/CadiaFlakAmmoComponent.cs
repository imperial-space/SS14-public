using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Снарядный ящик: пополняет ленту зенитки.</summary>
[RegisterComponent]
public sealed partial class CadiaFlakAmmoComponent : Component
{
    [DataField]
    public int Rounds = 100;
}
