using Robust.Shared.Maths;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticVoidAscendedComponent : Component
{
    [DataField]
    public float WaveTimer;

    public EntityUid? AmbientSoundEntity;

    /// <summary>
    /// Maps on which WeatherVoidStorm was started (all station maps); used to stop it on ComponentRemove.
    /// </summary>
    public List<EntityUid> StormMapUids = new();

    /// <summary>
    /// Eye color before void ascension; restored on ComponentRemove.
    /// </summary>
    public Color? OriginalEyeColor;

    /// <summary>
    /// AtmosTemperatureTransferEfficiency before void ascension; restored on ComponentRemove.
    /// </summary>
    public float? OriginalAtmosTransferEfficiency;
}
