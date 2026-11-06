using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>active_sonar: показывает живых существ вокруг.</summary>
[RegisterComponent]
public sealed partial class ImperialModSonarComponent : Component
{
    [DataField]
    public float Range = 9f;

    [DataField]
    public int Slices = 8;

    [DataField]
    public TimeSpan ScanInterval = TimeSpan.FromSeconds(0.5);

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Imperial/ModModules/sonar_ping.ogg");

    [ViewVariables]
    public int Slice;

    [ViewVariables]
    public TimeSpan NextScan;
}
