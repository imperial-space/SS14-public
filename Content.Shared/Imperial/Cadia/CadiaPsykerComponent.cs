using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Псайкер: шкала варп-заряда восстанавливается сама, а напряжение растёт с каждым касто и спадает медленно.
/// Чем выше напряжение, тем вероятнее Опасности варпа. Капюшон и молитва священника снижают риск.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaPsykerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Charge = 100f;

    [DataField, AutoNetworkedField]
    public float MaxCharge = 100f;

    /// <summary>Восстановление заряда в секунду.</summary>
    [DataField]
    public float Regen = 2.5f;

    /// <summary>Напряжение 0..100: чем выше, тем опаснее следующий каст.</summary>
    [DataField, AutoNetworkedField]
    public float Strain;

    [DataField]
    public float StrainDecay = 1f;

    /// <summary>Подавлен другим псайкером до этого момента.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SilencedUntil;

    /// <summary>Какие способности выдать при появлении.</summary>
    [DataField]
    public List<EntProtoId> Powers = new();

    [ViewVariables]
    public List<EntityUid> Actions = new();

    [DataField]
    public TimeSpan PerilsDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan PerilsDuration2 = TimeSpan.FromSeconds(4);

    [DataField]
    public float EntitySmiteAmount = 28f;

    [DataField]
    public float EntityBloodboilAmount = 18f;

    [DataField]
    public float EntityBloodboilAmount2 = 12f;

    [DataField]
    public FixedPoint2 EntityBloodboilAmount3 = -10;

    [DataField]
    public float PerilsAmount = 20f;

    [DataField]
    public float PerilsTotalIntensity = 12f;

    [DataField]
    public float PerilsSlope = 4f;

    [DataField]
    public float PerilsMaxTileIntensity = 3f;

    [DataField]
    public float PerilsAmount2 = 35f;
}
