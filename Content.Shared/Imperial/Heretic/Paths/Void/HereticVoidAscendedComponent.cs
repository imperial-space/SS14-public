namespace Content.Shared.Imperial.Heretic.Paths.Void;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticVoidAscendedComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan NextWave;

    [ViewVariables]
    public EntityUid? AmbientSoundEntity;

    /// <summary>
    /// Maps on which WeatherVoidStorm was started (all station maps); used to stop it on ComponentRemove.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> StormMapUids = new();

    /// <summary>
    /// Eye color before void ascension; restored on ComponentRemove.
    /// </summary>
    [ViewVariables]
    public Color? OriginalEyeColor;

    /// <summary>
    /// AtmosTemperatureTransferEfficiency before void ascension; restored on ComponentRemove.
    /// </summary>
    [ViewVariables]
    public float? OriginalAtmosTransferEfficiency;
}
