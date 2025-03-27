using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.TurnedToStone;


/// <summary>
/// Add phase space components on caster
/// </summary>
[RegisterComponent, NetworkedComponent, Serializable]
public sealed partial class TurnedToStoneComponent : Component
{
    [DataField]
    public TimeSpan LifeTime = TimeSpan.FromSeconds(3);

    [DataField]
    public ProtoId<DamageModifierSetPrototype> DamageModifierSetID = "MedievalStone";


    [ViewVariables]
    public bool Disposed = false;

    [ViewVariables]
    public TimeSpan DisposeTime = TimeSpan.FromSeconds(0);

    [ViewVariables]
    public DamageSpecifier Damage = new();

    [ViewVariables]
    public ProtoId<DamageModifierSetPrototype> CachedDamageModifierSetID = "";

    [ViewVariables]
    public bool HasOutline = false;
}
