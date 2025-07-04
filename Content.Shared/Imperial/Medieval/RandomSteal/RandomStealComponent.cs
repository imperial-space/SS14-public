using Robust.Shared.Audio;

namespace Content.Shared.Imperial.RandomSteal.Components;

[RegisterComponent]
public sealed partial class RandomStealComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Chance = 40;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan TimeNeed = TimeSpan.FromSeconds(1.8f);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<string> Slots = new() { "pocket1", "pocket2", "back" };

    [DataField, ViewVariables(VVAccess.ReadWrite)]

    public SoundPathSpecifier FailedSound { get; set; } = new("/Audio/Effects/chime.ogg")
    {
        Params = AudioParams.Default.WithVolume(5f)
    };

    public List<string> Sizes = new() { "Small", "Tiny" };
}
