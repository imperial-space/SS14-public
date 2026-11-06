using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Проклятый шар-восьмёрка.</summary>
[RegisterComponent]
public sealed partial class UplinkHauntedEightballComponent : Component
{
    [ViewVariables]
    public TimeSpan? AnswerAt;

    [ViewVariables]
    public EntityUid? Asker;

    [DataField]
    public TimeSpan AnswerDelay = TimeSpan.FromSeconds(5);
}
