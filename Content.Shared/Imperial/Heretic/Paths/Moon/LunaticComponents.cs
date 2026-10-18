using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Actions;
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
}

/// <summary>datum/antagonist/lunatic/master: вознёсшийся еретик Луны, Постановщик.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LunaticMasterComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon = "LunaticMasterFaction";
}

/// <summary>Предмет Хватки Мансуса лунатика (obj/item/melee/touch_attack/mansus_fist).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LunaticMansusGraspItemComponent : Component;

/// <summary>
/// delusion/preset/moon: пока эффект висит, носитель видит всех вокруг лунными массами.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMoonDelusionStatusEffectComponent : Component;

/// <summary>action/cooldown/lunatic_track: «Эхо Лунного света».</summary>
public sealed partial class LunaticTrackActionEvent : InstantActionEvent;

/// <summary>action/cooldown/spell/touch/mansus_grasp, выданная лунатику.</summary>
public sealed partial class LunaticMansusGraspActionEvent : InstantActionEvent;
