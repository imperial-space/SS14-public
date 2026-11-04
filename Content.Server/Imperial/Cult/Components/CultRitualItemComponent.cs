using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Ритуальный предмет (datum/component/cult_ritual_item): черчение, стирание рун.</summary>
[RegisterComponent]
public sealed partial class CultRitualItemComponent : Component
{
    [ViewVariables] public bool Drawing;

    /// <summary>examine_message для культистов.</summary>
    [DataField]
    public LocId? ExamineMessage;

    /// <summary>Щиты вокруг черчения руны Нар'Си.</summary>
    [ViewVariables] public List<EntityUid> Shields = new();
}
