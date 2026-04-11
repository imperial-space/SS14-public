namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderPrincessBroodComponent : Component
{
    [DataField]
    public EntityUid? Princess;

    [DataField]
    public bool Elite;
}
