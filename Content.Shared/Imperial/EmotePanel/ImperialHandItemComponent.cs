using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>/obj/item/hand_item: исчезает, как только покидает руку.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialHandItemComponent : Component
{
    [DataField(required: true)]
    public ImperialHandItemKind Kind;

    /// <summary>table_smacks_left шлепка.</summary>
    [DataField]
    public int TableSmacks = 3;
}
