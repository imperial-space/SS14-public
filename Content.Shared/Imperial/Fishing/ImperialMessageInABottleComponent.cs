using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Бутылка для послания (glass/bottle + message_in_a_bottle): в неё кладут записку или деньги
/// и бросают в море — послание сохраняется между раундами и может быть выловлено.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialMessageInABottleComponent : Component
{
    public const string SlotId = "message";

    /// <summary>Только что выловлена: подгрузить сохранённое послание.</summary>
    [DataField]
    public bool LoadOnSpawn = true;
}
