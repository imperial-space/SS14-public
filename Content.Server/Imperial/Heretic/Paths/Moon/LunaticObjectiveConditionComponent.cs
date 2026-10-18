using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
namespace Content.Server.Imperial.Heretic.Paths.Moon;

/// <summary>objective/lunatic: «Помогите вашему Постановщику» или, для самого еретика, «Ведите лунатиков».</summary>
[RegisterComponent]
public sealed partial class LunaticObjectiveConditionComponent : Component
{
    /// <summary>is_master: цель Постановщика.</summary>
    [DataField]
    public bool IsMaster;
}

/// <summary>Роль лунатика на сущности роли разума.</summary>
[RegisterComponent]
public sealed partial class LunaticRoleComponent : Component;

/// <summary>Роль Постановщика на сущности роли разума.</summary>
[RegisterComponent]
public sealed partial class LunaticMasterRoleComponent : Component;
