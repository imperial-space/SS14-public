using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// Блок управления МОДа: хранит модули, считает сложность и питает их от своей батареи.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModSuitComponent : Component
{
    public const string ContainerId = "imperial-mod-modules";

    /// <summary>complexity_max: предельная суммарная сложность модулей.</summary>
    [DataField, AutoNetworkedField]
    public int MaxComplexity = 15;

    /// <summary>open: открыта крышка — можно ставить и снимать модули, костюм не работает.</summary>
    [DataField, AutoNetworkedField]
    public bool Open;

    /// <summary>dna_lock: ДНК владельца. Костюм не работает на ком-то другом.</summary>
    [DataField, AutoNetworkedField]
    public string? LockedDna;

    /// <summary>Модули, установленные при создании костюма.</summary>
    [DataField]
    public List<EntProtoId> StartingModules = new();

    [ViewVariables]
    public Container Modules = default!;

    /// <summary>Носитель, которому сейчас выданы действия модулей.</summary>
    [ViewVariables]
    public EntityUid? Wearer;

    /// <summary>Накопитель времени до следующего тика расхода энергии.</summary>
    [ViewVariables]
    public float Accumulator;
}
