namespace Content.Shared.Imperial.Heretic.Paths.Lock;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticLockRiftComponent : Component
{
    [DataField]
    public EntityUid? Caster;

    [DataField]
    public float SpawnInterval = 30f;

    [DataField]
    public string SpawnEntity = "HereticFamiliarStalker";

    [ViewVariables, AutoPausedField]
    public TimeSpan NextSpawn;
}
