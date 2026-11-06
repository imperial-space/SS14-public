namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Библия религии станции: при осмотре видно, каким богом она одобрена.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialHolyBibleComponent : Component
{
    [DataField]
    public string Deity = string.Empty;
}
