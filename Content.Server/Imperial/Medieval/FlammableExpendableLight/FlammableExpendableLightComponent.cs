namespace Content.Server.Imperial.Medieval.FlammableExpendableLight;


/// <summary>
/// The component overrides the behavior of ExpendableLight by lighting it only if the OnFire field in FlammableComponent is true
/// </summary>
[RegisterComponent]
public sealed partial class FlammableExpendableLightComponent : Component;
