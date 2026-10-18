using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

[Serializable, NetSerializable]
public enum DnaVaultUiKey : byte
{
    Key,
}

/// <summary>Состояние ДНК-хранилища (tgui DnaVault из SS13).</summary>
[Serializable, NetSerializable]
public sealed class DnaVaultBoundUserInterfaceState : BoundUserInterfaceState
{
    public int Plants;
    public int PlantsMax;
    public int Animals;
    public int AnimalsMax;
    public int Dna;
    public int DnaMax;
    public bool Completed;

    /// <summary>Две мутации на выбор для каждого, кто открывал хранилище и ещё не получил улучшение.</summary>
    public Dictionary<NetEntity, List<ProtoId<GeneticMutationPrototype>>> Choices = new();
}

/// <summary>Выбрать улучшение из своей пары.</summary>
[Serializable, NetSerializable]
public sealed class DnaVaultChooseMessage(ProtoId<GeneticMutationPrototype> mutation) : BoundUserInterfaceMessage
{
    public readonly ProtoId<GeneticMutationPrototype> Mutation = mutation;
}
