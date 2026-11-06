using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Components;

/// <summary>Хромосома (chromosome из SS13): вставляется в консоль и затем в мутацию.</summary>
[RegisterComponent]
public sealed partial class ChromosomeComponent : Component
{
    [DataField(required: true)]
    public ChromosomeKind Kind;
}
