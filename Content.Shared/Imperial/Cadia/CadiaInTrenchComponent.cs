using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Боец в окопе: большая часть пуль издали проходит над головой.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaInTrenchComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Sections;
}
