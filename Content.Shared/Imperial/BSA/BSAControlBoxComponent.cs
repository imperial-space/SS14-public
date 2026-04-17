using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Imperial.BSA;

/// <summary>
/// Компонент консоли управления Блюспейс-артиллерией.
/// Живёт на центральной части BSA (ControlBox).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BSAControlBoxComponent : Component
{
    /// <summary>
    /// Перезарядка между выстрелами.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Суммарная интенсивность взрыва.
    /// </summary>
    [DataField]
    public float ExplosionTotalIntensity = 8000f;

    /// <summary>
    /// Скорость затухания взрыва.
    /// </summary>
    [DataField]
    public float ExplosionSlope = 5f;

    /// <summary>
    /// Максимальная интенсивность на тайле.
    /// </summary>
    [DataField]
    public float ExplosionMaxTileIntensity = 500f;

    /// <summary>
    /// ID прототипа взрыва (например "Default").
    /// </summary>
    [DataField]
    public string ExplosionType = "Default";

    /// <summary>
    /// Момент следующего разрешённого выстрела.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? NextFire;

    /// <summary>
    /// Выбранный маяк-цель (EntityUid WarpPoint).
    /// </summary>
    [ViewVariables]
    public EntityUid? SelectedTarget;

    /// <summary>
    /// Есть ли питание от APC.
    /// </summary>
    [ViewVariables]
    public bool Powered;

    /// <summary>
    /// Цель, по которой будет произведён выстрел после задержки (предупреждение ЦК).
    /// </summary>
    [ViewVariables]
    public EntityUid? PendingFireTarget;

    /// <summary>
    /// Момент фактического выстрела (через 5 секунд после нажатия кнопки).
    /// </summary>
    [ViewVariables]
    public TimeSpan? PendingFireTime;

    /// <summary>
    /// Отражает IsAssembled MultipartMachineComponent — синхронизируется с клиентом.
    /// </summary>
    [AutoNetworkedField]
    public bool Assembled = false;

    /// <summary>
    /// Базовое потребление энергии в собранном состоянии (Вт).
    /// </summary>
    [DataField]
    public int IdlePowerDraw = 5000;

    /// <summary>
    /// Звук выстрела (на всю карту).
    /// </summary>
    [DataField]
    public SoundSpecifier? FireSound = new SoundPathSpecifier("/Audio/Effects/explosion1.ogg");

    /// <summary>
    /// Звук выстрела БСА — воспроизводится у пушки через 5 сек после приказа.
    /// На 4-й секунде звука — взрыв (total delay 9s).
    /// </summary>
    [DataField]
    public SoundSpecifier? ShotSound = new SoundPathSpecifier("/Audio/Imperial/bsa/shot.ogg");

    /// <summary>
    /// Короткий click при нажатии кнопок Выстрел / Сканировать.
    /// </summary>
    [DataField]
    public SoundSpecifier? ButtonSound = new SoundPathSpecifier("/Audio/Imperial/bsa/button.ogg");

    /// <summary>
    /// Циклический звук накопления заряда (-5 dB). Запускается при нажатии Выстрел,
    /// останавливается в момент взрыва.
    /// </summary>
    [DataField]
    public SoundSpecifier? AccumulationSound = new SoundPathSpecifier("/Audio/Imperial/bsa/accumulation.ogg");

    /// <summary>
    /// Звук последствий взрыва — слышен в радиусе 25 тайлов у цели (-5 dB).
    /// </summary>
    [DataField]
    public SoundSpecifier? ConsequencesSound = new SoundPathSpecifier("/Audio/Imperial/bsa/consequences.ogg");

    /// <summary>
    /// Звук выбора цели (маяка) и начала зарядки.
    /// </summary>
    [DataField]
    public SoundSpecifier? GoalSound = new SoundPathSpecifier("/Audio/Imperial/bsa/goal.ogg");

    /// <summary>
    /// Звук глобального предупреждения перед выстрелом.
    /// </summary>
    [DataField]
    public SoundSpecifier? AlertSound = new SoundPathSpecifier("/Audio/Corvax/Adminbuse/artillery.ogg");

    // ── Runtime-только поля (не сериализуются) ──

    /// <summary>EntityUid звуковой сущности накопления — для остановки петли.</summary>
    [ViewVariables]
    public EntityUid? AccumulationSoundEntity;

    /// <summary>Время воспроизведения shot.ogg (t+5s, после artillery.ogg).</summary>
    [ViewVariables]
    public TimeSpan? ShotAudioTime;

    /// <summary>Время появления вспышки у ствола (t+9.5s после нажатия).</summary>
    [ViewVariables]
    public TimeSpan? MuzzleFlashTime;

    /// <summary>Цель для вспышки выстрела (сохраняется до момента спавна флэша).</summary>
    [ViewVariables]
    public EntityUid? FlashTarget;

    /// <summary>
    /// Прототип эффекта взрыва у цели.
    /// </summary>
    [DataField]
    public EntProtoId ImpactEffect = "BSAImpactEffect";

    /// <summary>
    /// Смещение точки спавна снаряда от центра BSAFrontPart вперёд,
    /// чтобы снаряд появлялся на конце ствола, а не внутри орудия.
    /// </summary>
    [DataField]
    public float MuzzleOffset = 6.5f;

    /// <summary>
    /// Прототип снаряда-вспышки, который спавнится у дула BSA.
    /// </summary>
    [DataField]
    public EntProtoId BSAShotProjectile = "BSAShotProjectile";
}
