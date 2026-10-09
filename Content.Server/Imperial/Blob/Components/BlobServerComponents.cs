using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Blob.Components;

/// <summary>
/// Носитель блоба (datum/antagonist/blob/infection): действие «Pop» и авто-выпуск через 6 минут.
/// </summary>
[RegisterComponent]
public sealed partial class BlobCarrierComponent : Component
{
    [DataField]
    public EntProtoId PopAction = "ActionBlobPop";

    [DataField]
    public EntProtoId OvermindPrototype = "MobBlobOvermind";

    /// <summary>OVERMIND_STARTING_AUTO_PLACE_TIME.</summary>
    [DataField]
    public TimeSpan AutoPopDelay = TimeSpan.FromMinutes(6);

    /// <summary>starting_points_human_blob.</summary>
    [DataField]
    public float StartingPoints = 60f;

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Imperial/blob/blobalert.ogg");

    [ViewVariables] public EntityUid? PopActionEntity;
    [ViewVariables] public TimeSpan AutoPopTime;
    [ViewVariables] public EntityUid? Rule;
}

/// <summary>Опрос призраков на блоббернаута (pick_blobbernaut_candidate).</summary>
[RegisterComponent]
public sealed partial class BlobbernautSpawnerComponent : Component
{
    [ViewVariables] public EntityUid? Overmind;
    [ViewVariables] public EntityUid? Factory;
    [ViewVariables] public TimeSpan Expire;
}

/// <summary>Заражённый спорой: союзник блоба с панцирем на голове.</summary>
[RegisterComponent]
public sealed partial class BlobInfectedComponent : Component
{
    [ViewVariables] public EntityUid? Overmind;

    /// <summary>Снятые маски столкновений (BlobImpassable) по фикстурам.</summary>
    [ViewVariables] public Dictionary<string, int> DisabledFixtureMasks = new();
}

/// <summary>Спора, нацелившаяся на заражение.</summary>
[RegisterComponent]
public sealed partial class BlobSporeLatchComponent : Component
{
    /// <summary>Дистанция, с которой спора начинает забираться на голову.</summary>
    [DataField] public float InfectionRange = 1.2f;

    /// <summary>Насколько близко спора подлетает к жертве (меньше InfectionRange).</summary>
    [DataField] public float ApproachRange = 0.6f;

    /// <summary>Радиус поиска жертв в критическом состоянии.</summary>
    [DataField] public float TargetSearchRange = 20f;

    /// <summary>Время, за которое спора забирается на голову.</summary>
    [DataField] public float LatchDuration = 5f;

    [ViewVariables] public EntityUid? LatchTarget;
    [ViewVariables] public bool LatchInProgress;
    [ViewVariables] public TimeSpan NextThink;
}
