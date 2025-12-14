namespace Content.Server.Imperial.Medieval.Plague;

[RegisterComponent]
public sealed partial class MedievalPlagueSpreadBlockingComponent : Component
{
    [DataField]
    public float Modifier = 0.8f;
}
