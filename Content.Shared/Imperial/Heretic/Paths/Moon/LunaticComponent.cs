using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Paths.Moon;

/// <summary>
/// datum/antagonist/lunatic: аколит вознёсшегося еретика Луны.
/// Висит на теле, чтобы его видели HUD лунатиков и «Эхо Лунного света».
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LunaticComponent : Component
{
    /// <summary>ascended_body: тело Постановщика.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Master;

    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon = "LunaticFaction";

    [ViewVariables]
    public EntityUid? TrackAction;

    [ViewVariables]
    public EntityUid? GraspAction;

    [DataField]
    public TimeSpan TrackCooldown = TimeSpan.FromSeconds(1);
}
