namespace Content.Server.Imperial.Medieval.WarningOnAttach;

[RegisterComponent]
public sealed partial class WarningOnAttachComponent : Component
{
    public string Message => Loc.GetString(_message);

    [DataField("message")]
    private string _message = "";

    public List<string> Players = new();
}
