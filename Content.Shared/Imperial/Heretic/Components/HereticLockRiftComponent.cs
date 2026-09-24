namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticLockRiftComponent : Component
{
    [DataField]
    public EntityUid? Caster;

    [DataField]
    public float SpawnInterval = 30f;

    [DataField]
    public string SpawnEntity = "HereticFamiliarStalker";

    public float Timer;
}
