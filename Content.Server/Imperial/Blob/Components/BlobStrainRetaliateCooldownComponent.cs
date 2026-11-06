namespace Content.Server.Imperial.Blob;

/// <summary>
/// Данные <see cref="BlobStrainSystem"/> для этой сущности.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BlobStrainRetaliateCooldownComponent : Component
{
    [ViewVariables, AutoPausedField]
    public TimeSpan Value;
}
