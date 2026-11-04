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
}
