using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.SoundOnInteract;

[RegisterComponent]
public sealed partial class MedievalSoundOnInteractComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundPathSpecifier? OnPick = null;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundPathSpecifier? OnPut = null;
}
