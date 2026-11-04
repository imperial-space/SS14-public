using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Лопатка: копает окоп на голой земле.</summary>
[RegisterComponent]
public sealed partial class CadiaEntrenchingToolComponent : Component
{
    [DataField]
    public TimeSpan DigTime = TimeSpan.FromSeconds(5);
}
