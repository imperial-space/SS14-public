namespace Content.Server.Locale.Components;


[RegisterComponent]
public sealed partial class AdvancedLocaleComponent : Component
{

    [DataField]
    public string Name = "";

    [DataField]
    public string Desc = "";

    [DataField]
    public string Content = "";

    [DataField]
    public string WayStoneName = "";


}
