using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Личинка: растёт (amount_grown) и может эволюционировать, когда вырастет.</summary>
[RegisterComponent]
public sealed partial class XenoLarvaComponent : Component
{
    [DataField]
    public float Growth;

    [DataField]
    public float MaxGrowth = 100;

    [DataField]
    public List<EntProtoId> Castes = new() { "ImperialXenoHunter", "ImperialXenoSentinel", "ImperialXenoDrone" };

    [ViewVariables]
    public bool Hidden;
}

/// <summary>Охотник: прыжок (pounce).</summary>
[RegisterComponent]
public sealed partial class XenoLeaperComponent : Component
{
    [DataField]
    public float MaxDistance = 7;

    [DataField]
    public float Speed = 15;

    [DataField]
    public TimeSpan TargetParalyze = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan SelfParalyze = TimeSpan.FromSeconds(4);

    [ViewVariables]
    public bool Leaping;
}

/// <summary>Желудок ксеноморфа: пожранные (devour) перевариваются.</summary>
[RegisterComponent]
public sealed partial class XenoStomachComponent : Component
{
    public const string ContainerId = "xeno_stomach";

    [DataField]
    public TimeSpan DevourTime = TimeSpan.FromSeconds(13.5);

    /// <summary>stomach_acid_power = 75: acid_act(75, 10) по содержимому раз в три тика Life (6 с).</summary>
    [DataField]
    public DamageSpecifier Digestion = new() { DamageDict = new() { ["Caustic"] = 75 } };

    [DataField]
    public TimeSpan DigestInterval = TimeSpan.FromSeconds(6);

    [DataField]
    public SoundSpecifier RegurgitateSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/alien_york.ogg");

    [DataField]
    public SoundSpecifier DevourSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/alien_eat.ogg");

    [ViewVariables] public TimeSpan NextDigest;
}

/// <summary>Страж в режиме скрытности.</summary>
[RegisterComponent]
public sealed partial class XenoSneakingComponent : Component;

/// <summary>
/// Кислота (component/acid, acid_power 200, acid_volume 1000). Предмет получает
/// min(1 + sqrt(power × volume) × 0.3, 300) урона в секунду, объём убывает на 1 + sqrt(volume) в секунду.
/// Обычная стена плавится за 30 секунд, укреплённые не плавятся.
/// </summary>
[RegisterComponent]
public sealed partial class XenoAcidComponent : Component
{
    [DataField]
    public float Power = 200;

    [DataField]
    public float Volume = 300;

    [ViewVariables] public bool Wall;
    [ViewVariables] public float WallIntegrity = 30;
    [ViewVariables] public TimeSpan NextTick;
    [ViewVariables] public EntityUid? Overlay;
}

/// <summary>Сорняк улья (alien/weeds).</summary>
[RegisterComponent]
public sealed partial class XenoWeedsComponent : Component
{
    [ViewVariables] public EntityUid? Node;
    [ViewVariables] public TimeSpan? DieAt;
}

/// <summary>Узел сорняков (weeds/node): каждые 5–10 с даёт рост сорнякам в радиусе 3.</summary>
[RegisterComponent]
public sealed partial class XenoWeedNodeComponent : Component
{
    [DataField]
    public int Range = 3;

    [DataField]
    public TimeSpan MinGrowTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MaxGrowTime = TimeSpan.FromSeconds(10);

    [DataField]
    public List<EntProtoId> Weeds = new() { "ImperialXenoWeeds1", "ImperialXenoWeeds2", "ImperialXenoWeeds3" };

    [ViewVariables] public TimeSpan NextGrow;
}

/// <summary>Гнездо: пристёгивают только ксеноморфы, самому выбраться — 100 с.</summary>
[RegisterComponent]
public sealed partial class XenoNestComponent : Component
{
    [DataField]
    public TimeSpan EscapeTime = TimeSpan.FromSeconds(100);
}

/// <summary>Яйцо: растёт 90–150 с, лопается при приближении носителя.</summary>
[RegisterComponent]
public sealed partial class XenoEggComponent : Component
{
    [DataField]
    public TimeSpan MinGrowth = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan MaxGrowth = TimeSpan.FromSeconds(150);

    [DataField]
    public EntProtoId Facehugger = "ImperialXenoFacehugger";

    [DataField]
    public float TriggerRange = 1.5f;

    [DataField]
    public bool StartGrown;

    [ViewVariables] public Shared.Imperial.Xenomorph.XenoEggState State;
    [ViewVariables] public TimeSpan NextStateAt;

    /// <summary>Burst(kill): разбитое яйцо выпускает мёртвого лицехвата.</summary>
    [ViewVariables] public bool KillChild;
}

/// <summary>Лицехват (clothing/mask/facehugger).</summary>
[RegisterComponent]
public sealed partial class XenoFacehuggerComponent : Component
{
    [DataField]
    public bool Sterile;

    [DataField]
    public float LeapRange = 1.5f;

    [DataField]
    public TimeSpan MinActive = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan MaxActive = TimeSpan.FromSeconds(40);

    [DataField]
    public TimeSpan MinImpregnation = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan MaxImpregnation = TimeSpan.FromSeconds(15);

    [DataField]
    public DamageSpecifier AttachDamage = new() { DamageDict = new() { ["Blunt"] = 5 } };

    /// <summary>Attach: Paralyze 1 с и Knockdown 10 с.</summary>
    [DataField]
    public TimeSpan AttachKnockdown = TimeSpan.FromSeconds(10);

    [ViewVariables] public Shared.Imperial.Xenomorph.XenoFacehuggerState State;
    [ViewVariables] public TimeSpan NextStateAt;
    [ViewVariables] public EntityUid? Victim;
    [ViewVariables] public TimeSpan ImpregnateAt;
    [ViewVariables] public TimeSpan NextCheck;
}

/// <summary>Королевский паразит: делает ксеноморфа преторианцем.</summary>
[RegisterComponent]
public sealed partial class XenoRoyalParasiteComponent : Component
{
    [DataField]
    public EntProtoId Praetorian = "ImperialXenoPraetorian";

    /// <summary>promotion_plasma_cost: списывается при возвышении.</summary>
    [DataField]
    public float Cost = 500;
}

/// <summary>Спавнер личинки, вырывающейся из носителя (опрос призраков).</summary>
[RegisterComponent]
public sealed partial class XenoBurstSpawnerComponent : Component
{
    [ViewVariables] public EntityUid? Host;
}
