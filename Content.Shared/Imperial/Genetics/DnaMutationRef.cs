using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Ссылка на мутацию в окне консоли.</summary>
[Serializable, NetSerializable]
public readonly record struct DnaMutationRef(DnaMutationSource Source, int Index, int Injector = -1);
