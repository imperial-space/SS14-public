using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Клон из слизи (chilling/cerulean): разлагается, разум возвращается в тело.</summary>
[RegisterComponent]
public sealed partial class SlimeCloneComponent : Component
{
    [ViewVariables]
    public EntityUid Original;

    [ViewVariables]
    public EntityUid? Mind;

    [ViewVariables]
    public TimeSpan NextDecay;
}
