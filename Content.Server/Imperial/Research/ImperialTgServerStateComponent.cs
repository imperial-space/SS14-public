using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Research.Components;
using Content.Server.Research.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Imperial.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Research;

/// <summary>Состояние сервера исследований TG: research_disabled и EMPED.</summary>
[RegisterComponent]
public sealed partial class ImperialTgServerStateComponent : Component
{
    [ViewVariables]
    public bool Disabled;

    [ViewVariables]
    public TimeSpan EmpedUntil;

    /// <summary>deconstructed_items: типы, уже давшие очки в деструктивном анализаторе.</summary>
    [ViewVariables]
    public HashSet<string> DeconstructedItems = new();
}
