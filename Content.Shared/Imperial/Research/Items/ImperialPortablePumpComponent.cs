using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Портативная воздушная помпа (portable_atmospherics/pump).</summary>
[RegisterComponent]
public sealed partial class ImperialPortablePumpComponent : Component
{
    [DataField]
    public GasMixture Air = new(1000f) { Temperature = Atmospherics.T20C };

    /// <summary>true — качать из бака наружу, false — с тайла в бак.</summary>
    [DataField]
    public bool PumpOut;

    [DataField]
    public bool Enabled;

    [DataField]
    public float TargetPressure = Atmospherics.OneAtmosphere;

    [DataField]
    public float MaxPressure = Atmospherics.OneAtmosphere * 25;

    /// <summary>Скорость, литров за тик атмосферы.</summary>
    [DataField]
    public float TransferRate = 200f;
}
