using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Ауспекс-радар: видит шаттлы врага на этажах выше, предупреждает свою сторону о новых целях
/// и увеличивает дальность обзора и точность зениток рядом.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaAuspexComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaFaction Faction = CadiaFaction.Imperium;

    [DataField]
    public float Range = 40f;

    /// <summary>Зенитки в этом радиусе получают данные наведения.</summary>
    [DataField]
    public float LinkRange = 14f;

    /// <summary>Во сколько раз уменьшается разброс связанной зенитки.</summary>
    [DataField]
    public float SpreadFactor = 0.6f;

    [DataField]
    public SoundSpecifier ContactSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/radar_ping.ogg");

    [ViewVariables]
    public HashSet<EntityUid> Contacts = new();

    [DataField, AutoNetworkedField]
    public int ContactCount;
}
