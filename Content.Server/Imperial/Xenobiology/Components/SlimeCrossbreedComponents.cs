using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>burning/charged/chilling: эффект при сжатии в руке с 10u плазмы.</summary>
[RegisterComponent]
public sealed partial class SlimeCrossActivatedComponent : Component
{
    [DataField]
    public string Solution = "crossbreed";

    [DataField]
    public float PlasmaRequired = 10f;

    /// <summary>Эффект уже запущен (таймер взрыва, призыв и т.п.).</summary>
    [ViewVariables]
    public bool Active;
}

/// <summary>chilling/bluespace, chilling/sepia: список связанных существ.</summary>
[RegisterComponent]
public sealed partial class SlimeCrossLinkComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Linked = new();
}

/// <summary>charged/gold: медленно призывает 10 враждебных существ.</summary>
[RegisterComponent]
public sealed partial class SlimeCrossMobSpawnerComponent : Component
{
    [DataField]
    public int MaxSpawn = 10;

    [ViewVariables]
    public int Spawned;

    [ViewVariables]
    public TimeSpan NextSpawn;
}

/// <summary>slimecross/industrial: поглощает плазму и производит предметы.</summary>
[RegisterComponent]
public sealed partial class SlimeIndustrialComponent : Component
{
    [DataField]
    public string Solution = "crossbreed";

    [DataField]
    public int PlasmaRequired = 2;

    [DataField]
    public EntProtoId? Item;

    /// <summary>Случайный пул: food, drink, foodordrink, coin, extract.</summary>
    [DataField]
    public string? RandomPool;

    [DataField]
    public int Amount = 1;

    /// <summary>Сколько единиц стопки выдать (iron/ten).</summary>
    [DataField]
    public int StackCount;

    /// <summary>darkblue: зелья огнеупорности одноразовые; yellow: батареи частично заряжены.</summary>
    [DataField]
    public string? AfterSpawn;

    [ViewVariables]
    public int Absorbed;

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public bool Working;
}

/// <summary>slimecross/recurring: экстракт сам восстанавливает заряды.</summary>
[RegisterComponent]
public sealed partial class SlimeRecurringComponent : Component
{
    [DataField]
    public float MaxCooldown = 10f;

    [ViewVariables]
    public float Cooldown;
}

/// <summary>slimecross/selfsustaining: распадается на четыре автослайма.</summary>
[RegisterComponent]
public sealed partial class SlimeSelfSustainingComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Extract;

    [DataField]
    public EntProtoId Autoslime = "SlimeAutoslime";
}

/// <summary>obj/item/autoslime: самоподдерживающийся экстракт, реакция выбирается при использовании.</summary>
[RegisterComponent]
public sealed partial class SlimeAutoslimeComponent : Component
{
    [DataField]
    public EntProtoId? Extract;
}

/// <summary>slimecross/reproductive: кормление кубиками обезьян даёт экстракты.</summary>
[RegisterComponent]
public sealed partial class SlimeReproductiveComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Extract;

    [DataField]
    public int FeedAmount = 3;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public int Cubes;

    [ViewVariables]
    public TimeSpan LastProduce;
}

/// <summary>slimecross/prismatic: бесконечная кисть для пола.</summary>
[RegisterComponent]
public sealed partial class SlimePrismaticComponent : Component
{
    [DataField]
    public Color PaintColor = Color.White;

    /// <summary>prismatic/grey смывает краску.</summary>
    [DataField]
    public bool Remover;
}

/// <summary>slimecross/regenerative: лечит, как регенеративное ядро легиона.</summary>
[RegisterComponent]
public sealed partial class SlimeRegenerativeComponent : Component
{
    /// <summary>regenerative/bluespace: место создания.</summary>
    [ViewVariables]
    public EntityCoordinates? Origin;
}

/// <summary>slimecross/consuming: ест еду и делает печенье.</summary>
[RegisterComponent]
public sealed partial class SlimeConsumingComponent : Component
{
    [DataField]
    public float NutrimentRequired = 10f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    [DataField]
    public int Cookies = 5;

    [DataField]
    public EntProtoId? Cookie;

    [ViewVariables]
    public float NutrimentEaten;

    [ViewVariables]
    public TimeSpan LastProduced = TimeSpan.FromMinutes(-10);
}

/// <summary>obj/item/slime_cookie.</summary>
[RegisterComponent]
public sealed partial class SlimeCookieComponent : Component
{
    /// <summary>Цвет-эффект печенья (grey, orange, ...).</summary>
    [DataField(required: true)]
    public string Effect = string.Empty;

    [DataField]
    public float Nutrition = 5f;

    [DataField]
    public LocId Taste = "slime-cookie-taste-grey";

    /// <summary>pyrite: краска.</summary>
    [ViewVariables]
    public Color? Dye;
}

/// <summary>slimecross/stabilized: пассивный эффект для носителя.</summary>
[RegisterComponent]
public sealed partial class SlimeStabilizedComponent : Component
{
    [ViewVariables]
    public EntityUid? Holder;

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public int Cooldown;

    /// <summary>gold: тип фамильяра.</summary>
    [ViewVariables]
    public EntProtoId? FamiliarProto;

    [ViewVariables]
    public string FamiliarName = string.Empty;

    [ViewVariables]
    public EntityUid? Familiar;

    /// <summary>cerulean: клон владельца.</summary>
    [ViewVariables]
    public EntityUid? Clone;

    /// <summary>rainbow: вложенное регенеративное ядро.</summary>
    [ViewVariables]
    public EntityUid? RegenCore;

    /// <summary>bluespace: здоровье на прошлом тике.</summary>
    [ViewVariables]
    public float? LastHealth;

    /// <summary>bluespace: откат телепорта.</summary>
    [ViewVariables]
    public TimeSpan BluespaceCooldown;

    /// <summary>sepia: текущий шаг скорости.</summary>
    [ViewVariables]
    public int SepiaMod;

    /// <summary>green: исходное имя.</summary>
    [ViewVariables]
    public string? OriginalName;

    /// <summary>pink: мир нарушен до этого времени.</summary>
    [ViewVariables]
    public TimeSpan BrokenPeaceUntil;

    [ViewVariables]
    public Dictionary<EntityUid, float> PinkTracked = new();

    [ViewVariables]
    public bool PinkPeaceful;
}

/// <summary>Временные эффекты печенья, ядер и экстрактов на существе.</summary>
[RegisterComponent]
public sealed partial class SlimeCrossEffectsComponent : Component
{
    [ViewVariables]
    public Dictionary<string, TimeSpan> Effects = new();

    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>Пацифизм выдан эффектами (а не другим источником).</summary>
    [ViewVariables]
    public bool GavePacified;

    /// <summary>Неуязвимость выдана эффектами.</summary>
    [ViewVariables]
    public bool GaveGodmode;

    /// <summary>Нескользящие ботинки выданы эффектами.</summary>
    [ViewVariables]
    public bool GaveNoSlip;
}

/// <summary>Ледяной блок frozenstasis / ice_stasis.</summary>
[RegisterComponent]
public sealed partial class SlimeIceStasisComponent : Component
{
    [ViewVariables]
    public EntityUid? Frozen;
}

/// <summary>Сепиевая «дежавю»: через 10 секунд возвращает существо и его урон.</summary>
[RegisterComponent]
public sealed partial class SlimeDejavuComponent : Component
{
    [ViewVariables]
    public EntityCoordinates Origin;

    [ViewVariables]
    public Dictionary<string, Content.Shared.FixedPoint.FixedPoint2> Damage = new();

    [ViewVariables]
    public TimeSpan RewindAt;

    [ViewVariables]
    public int Rewinds = 1;
}

/// <summary>Клон из слизи (chilling/cerulean): разлагается, разум возвращается в тело.</summary>
[RegisterComponent]
public sealed partial class SlimeCloneComponent : Component
{
    [ViewVariables]
    public EntityUid Original;

    [ViewVariables]
    public EntityUid? Mind;

    [ViewVariables]
    public TimeSpan NextDecay;
}

/// <summary>Временная стена из слизи (forcefield/slimewall).</summary>
[RegisterComponent]
public sealed partial class SlimeTemporaryComponent : Component
{
    [DataField]
    public float Lifetime = 10f;
}

/// <summary>barriercube: превращается в баррикаду.</summary>
[RegisterComponent]
public sealed partial class SlimeBarrierCubeComponent : Component
{
    [DataField]
    public EntProtoId Barrier = "SlimeBarricade";
}

/// <summary>capturedevice: хранит одно простое существо.</summary>
[RegisterComponent]
public sealed partial class SlimeCaptureDeviceComponent : Component
{
    [DataField]
    public string Container = "capture";
}

/// <summary>Маска-ребризер: лечит удушье, пока надета.</summary>
[RegisterComponent]
public sealed partial class SlimeRebreatherComponent : Component
{
    [ViewVariables]
    public EntityUid? Wearer;
}

/// <summary>Цветок мира: пацифизм и нельзя снять самому.</summary>
[RegisterComponent]
public sealed partial class SlimePeaceFlowerComponent : Component;

/// <summary>Радужный нож: случайный тип урона.</summary>
[RegisterComponent]
public sealed partial class SlimeRainbowKnifeComponent : Component;

/// <summary>Кровавый охладитель: перезаряжается кровью владельца.</summary>
[RegisterComponent]
public sealed partial class SlimeBloodChillerComponent : Component
{
    [ViewVariables]
    public TimeSpan NextCharge;
}

/// <summary>Призма света из призматических очков.</summary>
[RegisterComponent]
public sealed partial class SlimePrismGlassesComponent : Component
{
    [ViewVariables]
    public Color Color = Color.White;

    [ViewVariables]
    public EntityUid? ColourAction;

    [ViewVariables]
    public EntityUid? PlaceAction;
}

/// <summary>Зелья кроссбридинга (_potions.dm).</summary>
[RegisterComponent]
public sealed partial class SlimeCrossPotionComponent : Component
{
    /// <summary>extractcloner, peace, love, spaceproof, lavaproof, reviver, omnistabilizer.</summary>
    [DataField(required: true)]
    public string Kind = string.Empty;

    [DataField]
    public int Uses = 1;
}

/// <summary>Регенеративный экстракт внутри стабилизированного радужного.</summary>
[RegisterComponent]
public sealed partial class SlimeStoredRegenComponent : Component;
