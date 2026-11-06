using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

/// <summary>Метка для клиентского визуализатора камеры (анимации подъёма, створок и слой отрисовки).</summary>
[RegisterComponent]
public sealed partial class FissionChamberVisualsComponent : Robust.Shared.GameObjects.Component
{
    [ViewVariables]
    public FissionChamberState? LastState;
}
