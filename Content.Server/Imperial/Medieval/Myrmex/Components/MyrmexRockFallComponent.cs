using Content.Shared.Damage;

namespace Content.Server.Myrmex.Components
{
    [RegisterComponent]
    public sealed partial class MyrmexRockFallComponent : Component
    {

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public DamageSpecifier Damage = new()
        {
            DamageDict = new()
            {
                { "Blunt", 600}
            }
        };

        [DataField]
        public float Range = 2.1f;

        [DataField]
        public int BadCount = 0;

        [DataField]
        public float Chanse = 0.1f;

        [DataField]
        public int MaxBadCount = 3;

        [DataField]
        public string FallID = "MedievalWallSoilSolid";

        [DataField]
        public string WarningID = "MedievalRockfallWarning";

        [DataField]
        public string EndID = "MedievalRockfall";
    }
}
