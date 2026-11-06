using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Падающий орбитальный удар: на земле горит круг-предупреждение, по истечении времени — взрыв или луч лэнса.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaOrbitalStrikeComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Impact;

    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public CadiaBatteryKind Kind;

    [DataField]
    public float Intensity;

    [DataField]
    public float Slope;

    [DataField]
    public float MaxTileIntensity;

    [DataField]
    public EntityUid? Source;

    [DataField]
    public float UpdateStrikesRadius = 10f;
}
