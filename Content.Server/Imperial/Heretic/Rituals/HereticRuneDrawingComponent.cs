namespace Content.Server.Imperial.Heretic.Rituals;

/// <summary>
/// Еретик сейчас чертит руну; хранит визуальный эффект черчения, чтобы убрать его по окончании.
/// </summary>
[RegisterComponent]
public sealed partial class HereticRuneDrawingComponent : Component
{
    [ViewVariables]
    public EntityUid Effect;
}
