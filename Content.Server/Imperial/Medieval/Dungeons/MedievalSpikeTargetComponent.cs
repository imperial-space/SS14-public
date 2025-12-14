namespace Content.Server.SpikeTrap.Components
{
    [RegisterComponent]
    public sealed partial class MedievalSpikeTargetComponent : Component
    {
        [DataField]
        public bool Enabled = true;
    }
}
