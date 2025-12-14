using Robust.Shared.Audio;

namespace Content.Shared.Imperial.BellMap.Components;

[RegisterComponent]
public sealed partial class BellMapComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/church_bell1.ogg")
    {
        Params = AudioParams.Default.WithVolume(5f)
    };
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public LocId Locale = "bell-map-popup";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan? LastRingTime;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Cooldown = 5f; //Default cooldown of 5 seconds.
}
