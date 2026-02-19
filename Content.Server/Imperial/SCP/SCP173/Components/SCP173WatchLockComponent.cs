namespace Content.Server.Imperial.SCP.SCP173.Components;

[RegisterComponent]
public sealed partial class SCP173WatchLockComponent : Component
{
    [DataField("observeRadius")]
    public float ObserveRadius = 8f;

    [DataField("minLookDot")]
    public float MinLookDot = 0.35f;

    [DataField("requireUnobstructed")]
    public bool RequireUnobstructed = true;

    [DataField("frozenWalkModifier")]
    public float FrozenWalkModifier = 0f;

    [DataField("frozenSprintModifier")]
    public float FrozenSprintModifier = 0f;

    [DataField("requireLightToObserve")]
    public bool RequireLightToObserve = true;

    [DataField("lightLookupRadius")]
    public float LightLookupRadius = 12f;

    [DataField("lightRadiusPadding")]
    public float LightRadiusPadding = 0.15f;

    [ViewVariables]
    public bool IsLocked;

    [ViewVariables]
    public bool IsContainedByCell;
}
