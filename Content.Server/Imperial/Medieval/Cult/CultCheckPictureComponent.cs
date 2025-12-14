using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Server.Cult.Components;


[RegisterComponent]
public sealed partial class CultCheckPictureComponent : Component
{
    [DataField]
    public int BloodyCrystall = 0;

    [DataField]
    public int RedCrystall = 0;

    [DataField]
    public int NewSectorCost = 0;

    [DataField]
    public int UnlockedSectors = 0;

    [DataField]
    public bool CollegiumUnlocked = false;

    [DataField]
    public bool Sector1 = false;

    [DataField]
    public bool Sector2 = false;

    [DataField]
    public bool Sector3 = false;

    [DataField]
    public bool Sector6 = false;

    [DataField]
    public bool Sector7 = false;

    [DataField]
    public bool Sector8 = false;

    [DataField]
    public bool Sector9 = false;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SoundSpecifier SuccesSound { get; set; } = new SoundPathSpecifier("/Audio/Imperial/Medieval/Cult/ritual_success.ogg")
    {
        Params = AudioParams.Default.WithVolume(-2f).WithVariation(0.015f),
    };

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SoundSpecifier VictimSuccessSound { get; set; } = new SoundPathSpecifier("/Audio/Imperial/Medieval/Cult/victim_ritual_success.ogg");

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SoundSpecifier FailSound { get; set; } = new SoundPathSpecifier("/Audio/Imperial/Medieval/Cult/ritual_deny.ogg")
    {
        Params = AudioParams.Default.WithVolume(-2f).WithVariation(0.015f),
    };

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SoundSpecifier EatSound { get; set; } = new SoundPathSpecifier("/Audio/Imperial/Medieval/Cult/cristal_eat.ogg")
    {
        Params = AudioParams.Default.WithVolume(-2f).WithVariation(0.015f),
    };
}
