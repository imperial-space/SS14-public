using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// obj/item/slime_extract: экстракт слайма. В него впрыскивают реагенты, и он реагирует (slime_extracts.dm).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimeExtractComponent : Component
{
    /// <summary>Цвет слайма, из которого получен экстракт.</summary>
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<SlimeTypePrototype> SlimeType;

    /// <summary>extract_uses.</summary>
    [DataField, AutoNetworkedField]
    public int Uses = 1;

    /// <summary>crossbreed_modification.</summary>
    [DataField]
    public string? CrossbreedModification;

    /// <summary>recurring: перезаряжающийся экстракт (кроссбридинг), его нельзя усилить.</summary>
    [DataField, AutoNetworkedField]
    public bool Recurring;

    [DataField]
    public string SolutionName = "extract";

    /// <summary>qdel_timer: отложенное удаление после реакции с задержкой.</summary>
    [ViewVariables]
    public TimeSpan? DeleteAt;

    /// <summary>Последний, кто держал экстракт (fingerprintslast).</summary>
    [ViewVariables]
    public EntityUid? LastHolder;

    [DataField]
    public TimeSpan DeleteDelay = TimeSpan.FromSeconds(5.5);

    [DataField]
    public TimeSpan EffectEmpDuration = TimeSpan.FromSeconds(60);

    [DataField]
    public float EffectRandomSlimeOrBombChance = 0.5f;

    [DataField]
    public float EffectEmpRange = 5f;

    [DataField]
    public float EffectEmpEnergyConsumption = 50000f;

    [DataField]
    public float EffectExplosionTotalIntensity = 140f;

    [DataField]
    public float EffectExplosionSlope = 10f;

    [DataField]
    public float EffectExplosionMaxTileIntensity = 30f;

    [DataField]
    public TimeSpan FoamDuration = TimeSpan.FromSeconds(20f);
}
