namespace Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

/// <summary>
/// Тип управляемого сценария, который ожидает конкретного взаимодействия жертвы.
/// </summary>
public enum DeathNoteGuidedScenarioType : byte
{
    AirlockAccident,
    DisposalCatastrophe,
    VendingMachineCrush,
    PoisonedFood,
    PoisonedDrink,
}
