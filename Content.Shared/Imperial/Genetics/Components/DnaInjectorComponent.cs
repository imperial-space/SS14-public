using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Components;

/// <summary>
/// ДНК-инжектор (dnainjector из SS13). Активатор включает спящий ген цели, мутатор добавляет мутацию сверху,
/// инжектор облика переносит имя, внешность или особенности.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DnaInjectorComponent : Component
{
    [DataField, AutoNetworkedField]
    public DnaInjectorMode Mode = DnaInjectorMode.Mutator;

    [DataField, AutoNetworkedField]
    public List<StoredMutation> Mutations = new();

    [DataField, AutoNetworkedField]
    public bool Used;

    /// <summary>Активатор, впрыснутый в носителя гена: в консоли даёт хромосому.</summary>
    [DataField]
    public bool Research;

    [DataField]
    public TimeSpan InjectTime = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public GeneticMakeupData? Makeup;

    [ViewVariables]
    public DnaMakeupType MakeupType;

    [DataField]
    public TimeSpan StartInjectDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float InjectAmount = 100f;
}
