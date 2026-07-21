namespace Content.Shared.Imperial.Medieval;

[RegisterComponent]
public sealed partial class MedievalCartographerTableComponent : Component
{
    [DataField]
    public float UpdateInterval = 0.1f;

    [ViewVariables] public bool OpenSoundPlayed;
    [ViewVariables] public bool CloseSoundPlayed;
}
