using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>dyespray TG: перекрашивает волосы или бороду.</summary>
[RegisterComponent]
public sealed partial class ImperialHairDyeComponent : Component
{
    /// <summary>true — борода, false — волосы.</summary>
    [DataField]
    public bool Facial;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public Color? PendingColor;
}
