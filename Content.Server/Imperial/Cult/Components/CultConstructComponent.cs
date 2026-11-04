using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Конструкт культа.</summary>
[RegisterComponent]
public sealed partial class CultConstructComponent : Component
{
    [DataField]
    public CultConstructType ConstructType = CultConstructType.Juggernaut;

    [DataField]
    public List<EntProtoId> Actions = new();

    [DataField]
    public LocId PlaystyleString = "cult-construct-playstyle-generic";

    /// <summary>can_repair: лечит конструктов/тени/постройки (5 brute).</summary>
    [DataField]
    public bool CanRepair;

    [DataField]
    public bool CanRepairSelf;

    /// <summary>Отражение энергии (juggernaut/projectile_hit).</summary>
    [DataField]
    public bool ReflectEnergy;

    [ViewVariables] public EntityUid? Master;
    [ViewVariables] public List<EntityUid> GrantedActions = new();
    [ViewVariables] public List<EntityUid> RuneEffects = new();

    /// <summary>seek_master / seek_prey: чувство крови указывает на SeekTarget.</summary>
    [ViewVariables] public bool Seeking;
    [ViewVariables] public EntityUid? SeekTarget;
    [ViewVariables] public EntityUid? SeekAction;
}
