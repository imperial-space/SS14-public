namespace Content.Server.Myrmex.Components
{
    [RegisterComponent]
    public sealed partial class MyrmexEggComponent : Component
    {
        [DataField]
        public float TimeTillSpawn = 900f;
        [DataField]
        public string LarvaID = "MedievalMyrmexLarva";

        [DataField]
        public string SporeType = "";

        [DataField]
        public string LightColor = "";

    }
}
