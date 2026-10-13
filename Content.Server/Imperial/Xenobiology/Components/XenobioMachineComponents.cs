using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>machinery/computer/camera_advanced/xenobio.</summary>
[RegisterComponent]
public sealed partial class XenobioConsoleComponent : Component
{
    /// <summary>max_slimes.</summary>
    [DataField]
    public int MaxSlimes = 5;

    /// <summary>stored_monkeys.</summary>
    [DataField]
    public float StoredMonkeys;

    /// <summary>Радиус «своей зоны» консоли (allowed_area: в SS14 нет зон).</summary>
    [DataField]
    public float Range = 12f;

    /// <summary>Радиус поиска переработчика обезьян (связь в пределах той же зоны).</summary>
    [DataField]
    public float RecyclerRange = 15f;

    [DataField]
    public EntProtoId EyeProto = "XenobioCameraEye";

    [DataField]
    public EntProtoId MonkeyProto = "MobMonkey";

    [DataField]
    public string SlimeContainer = "xenobio_slimes";

    [DataField]
    public string PotionContainer = "xenobio_potion";

    [ViewVariables]
    public EntityUid? Eye;
}

/// <summary>mob/eye/camera/remote/xenobio.</summary>
[RegisterComponent]
public sealed partial class XenobioCameraEyeComponent : Component
{
    [ViewVariables]
    public EntityUid Console;

    [ViewVariables]
    public EntityUid Mind;

    [ViewVariables]
    public EntityUid Body;

    [ViewVariables]
    public EntityCoordinates LastValid;
}

/// <summary>machinery/monkey_recycler.</summary>
[RegisterComponent]
public sealed partial class SlimeMonkeyRecyclerComponent : Component
{
    /// <summary>stored_matter.</summary>
    [DataField]
    public float StoredMatter;

    /// <summary>cube_production: 0.1 за сервопривод и 0.1 за бункер первого уровня.</summary>
    [DataField]
    public float CubeProduction = 0.2f;

    [DataField]
    public EntProtoId Cube = "MonkeyCube";
}

/// <summary>machinery/processor/slime.</summary>
[RegisterComponent]
public sealed partial class SlimeProcessorComponent : Component
{
    /// <summary>rating_amount (бункер первого уровня).</summary>
    [DataField]
    public int RatingAmount = 1;

    /// <summary>rating_speed.</summary>
    [DataField]
    public int RatingSpeed = 1;

    /// <summary>food_processor_process.time = 40 децисекунд.</summary>
    [DataField]
    public float TimePerSlime = 4f;

    [DataField]
    public string Container = "slime_processor";

    [ViewVariables]
    public bool Processing;

    [ViewVariables]
    public TimeSpan ProcessingEnd;

    [ViewVariables]
    public TimeSpan NextPickup;
}

/// <summary>obj/item/slime_scanner.</summary>
[RegisterComponent]
public sealed partial class SlimeScannerComponent : Component;
