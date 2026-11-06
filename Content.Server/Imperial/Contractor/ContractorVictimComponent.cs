using Content.Shared.Imperial.Contractor;
using Robust.Shared.Map;
using Content.Shared.FixedPoint;

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

    [DataField]
    public TimeSpan ReturnVictimBoozePowerDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan UpdateVictimsBoozePowerDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan NextDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan UpdateVictimsBoozePowerDelay2 = TimeSpan.FromSeconds(3.5);

    [DataField]
    public TimeSpan NextDelay2 = TimeSpan.FromSeconds(6.5);

    [DataField]
    public TimeSpan UpdateVictimsBoozePowerDelay3 = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan NextDelay3 = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan UpdateVictimsDuration = TimeSpan.FromSeconds(20);

    [DataField]
    public int ReturnVictimRange = 6;

    [DataField]
    public FixedPoint2 UpdateVictimsQuantity = FixedPoint2.New(20);
}
