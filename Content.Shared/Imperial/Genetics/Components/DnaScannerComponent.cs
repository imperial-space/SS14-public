using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Components;

/// <summary>ДНК-сканер: замок не даёт извлечь пациента.</summary>
[RegisterComponent]
public sealed partial class DnaScannerComponent : Component
{
    [ViewVariables]
    public bool Locked;
}
