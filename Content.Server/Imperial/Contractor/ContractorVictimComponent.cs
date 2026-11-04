using Content.Shared.Imperial.Contractor;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Contractor;

/// <summary>Похищенный: эффекты, затем возврат на станцию.</summary>
[RegisterComponent]
public sealed partial class ContractorVictimComponent : Component
{
    [ViewVariables]
    public int Level;

    [ViewVariables]
    public TimeSpan NextExperience;

    [ViewVariables]
    public TimeSpan ReturnAt;

    /// <summary>Хранилище вещей жертвы.</summary>
    [ViewVariables]
    public EntityUid? Belongings;

    /// <summary>Маяк зоны отправки, куда жертву вернут.</summary>
    [ViewVariables]
    public EntityUid? Dropoff;

    [ViewVariables]
    public EntityCoordinates? FallbackReturn;
}
