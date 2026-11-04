using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>teleporter: телепорт на видимую клетку.</summary>
[RegisterComponent]
public sealed partial class ImperialModTeleporterComponent : Component
{
    [DataField]
    public float Range = 9f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
}
