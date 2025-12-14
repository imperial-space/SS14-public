namespace Content.Server.Myrmex.Components
{
    [RegisterComponent]
    public sealed partial class MyrmexGrowerComponent : Component
    {
        [DataField]
        public string ResType = "";

        [DataField]
        public string ResCur = "";

    }
}
