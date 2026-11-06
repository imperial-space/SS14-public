using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Гипноз (brain trauma hypnosis): навязчивая фраза.</summary>
[RegisterComponent]
public sealed partial class UplinkHypnotizedComponent : Component
{
    [ViewVariables]
    public string Phrase = string.Empty;

    [ViewVariables]
    public TimeSpan NextReminder;
}
