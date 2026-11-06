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
