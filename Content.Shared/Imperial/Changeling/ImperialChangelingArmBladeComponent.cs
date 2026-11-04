using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/obj/item/melee/arm_blade: скрежет airlock_alien_prying при вскрытии запитанного шлюза.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingArmBladeComponent : Component
{
    [DataField]
    public SoundSpecifier PrySound = new SoundPathSpecifier("/Audio/Imperial/Changeling/airlock_alien_prying.ogg");
}
