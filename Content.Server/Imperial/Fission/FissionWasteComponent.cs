using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Плутониевая жижа (nuclear_waste): лопатой убирается за 5 секунд.</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionWasteComponent : Component
{
    [DataField] public SoundSpecifier StepSound = new SoundCollectionSpecifier("FootstepBlood");
    [DataField] public TimeSpan ClearTime = TimeSpan.FromSeconds(5);
}
