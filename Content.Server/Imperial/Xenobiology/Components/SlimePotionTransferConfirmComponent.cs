namespace Content.Server.Imperial.Xenobiology;

/// <summary>
/// Данные <see cref="SlimePotionSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class SlimePotionTransferConfirmComponent : Component
{
    [ViewVariables]
    public (EntityUid Target, TimeSpan Until) Value;
}
