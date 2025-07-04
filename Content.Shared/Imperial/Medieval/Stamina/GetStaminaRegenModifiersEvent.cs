namespace Content.Shared.Imperial.Medieval.Stamina;

[ByRefEvent]
public record struct GetStaminaRegenModifiersEvent(float Modifier = 1f);
