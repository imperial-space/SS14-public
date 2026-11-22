using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

/// <summary>
/// Типизированные параметры обработчиков Death Note.
/// Конкретный обработчик читает только относящиеся к нему поля.
/// </summary>
[DataDefinition]
public sealed partial class DeathNotePresetParameters
{
    /// <summary>
    /// Базовое соотношение типов урона для обработчиков, использующих случайный итог.
    /// Перед масштабированием обработчик обязан создать копию спецификатора.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// Приватное сообщение цели перед эффектом.
    /// </summary>
    [DataField]
    public LocId? TargetPopup;

    /// <summary>
    /// Приватный звук, который проигрывается цели перед точным временем исполнения.
    /// </summary>
    [DataField]
    public SoundSpecifier? PreludeSound;

    /// <summary>
    /// За какое время до точного исполнения начинается прелюдия.
    /// Обычно совпадает с длительностью настроенного звука.
    /// </summary>
    [DataField]
    public TimeSpan PreludeDuration;

    /// <summary>
    /// Сдвигает фактический эффект немного раньше указанного времени, если этого требует синхронизация звука и действия.
    /// </summary>
    [DataField]
    public TimeSpan ExecutionAdvance;

    /// <summary>
    /// Минимальное время от записи до исполнения автоматического сценария.
    /// При пустом времени задержка увеличивается до этого значения, а слишком близкое
    /// явно заданное время отклоняется. Пользовательские предписания с «!» не затрагиваются.
    /// </summary>
    [DataField]
    public TimeSpan MinimumExecutionDelay;

    /// <summary>
    /// Границы случайного итогового урона для обработчиков, использующих масштабирование.
    /// Обе границы должны быть положительными; нулевое значение считается ошибкой конфигурации.
    /// </summary>
    [DataField]
    public float RandomDamageMin;

    [DataField]
    public float RandomDamageMax;

    /// <summary>
    /// Прототип направляемой сущности или существа.
    /// </summary>
    [DataField]
    public EntProtoId? EntityPrototype;

    /// <summary>
    /// Дополнительный тип обломков для сценариев, создающих несколько групп предметов.
    /// </summary>
    [DataField]
    public EntProtoId? SecondaryEntityPrototype;

    /// <summary>
    /// Необязательное станционное событие, запускаемое вместе с направленным сценарием фауны.
    /// </summary>
    [DataField]
    public EntProtoId? GameRule;

    /// <summary>
    /// Необязательный визуальный источник, используемый вместо станционного события для направленной фауны.
    /// </summary>
    [DataField]
    public EntProtoId? RiftPrototype;

    /// <summary>
    /// Расстояние от цели до точки появления направляемой сущности.
    /// </summary>
    [DataField]
    public float SpawnDistance;

    /// <summary>
    /// Количество создаваемых сущностей.
    /// </summary>
    [DataField]
    public int SpawnCount = 1;

    [DataField]
    public int SecondarySpawnCount;

    /// <summary>
    /// Защитный предел количества сущностей, создаваемых одним обработчиком.
    /// </summary>
    [DataField]
    public int MaximumSpawnCount = 16;

    /// <summary>
    /// Радиус поиска свободных клеток вокруг цели.
    /// </summary>
    [DataField]
    public float SpawnRadius = 3f;

    /// <summary>
    /// Радиус появления существ вокруг отдельного визуального разлома.
    /// </summary>
    [DataField]
    public float RiftSpawnRadius = 1.5f;

    /// <summary>
    /// Начальная скорость направляемой сущности.
    /// </summary>
    [DataField]
    public float LaunchSpeed;

    /// <summary>
    /// Скорость плавного доведения направляемого объекта и радиус подтверждённого попадания.
    /// </summary>
    [DataField]
    public float TurnResponsiveness;

    [DataField]
    public float ImpactRadius;

    /// <summary>
    /// Интервал повторной фиксации особой приоритетной цели у HTN-существа.
    /// </summary>
    [DataField]
    public TimeSpan RetargetInterval = TimeSpan.FromSeconds(0.25);

    /// <summary>
    /// Глобальный звук объявления о появлении направленной фауны.
    /// </summary>
    [DataField]
    public SoundSpecifier? AnnouncementSound;

    /// <summary>
    /// Минимальное и максимальное число попаданий мимика по назначенной цели.
    /// </summary>
    [DataField]
    public int MimicMinimumTargetHits = 2;

    [DataField]
    public int MimicMaximumTargetHits = 3;

    /// <summary>
    /// Тип штатного взрыва.
    /// </summary>
    [DataField]
    public ProtoId<ExplosionPrototype>? ExplosionType;

    [DataField]
    public float TotalIntensity;

    [DataField]
    public float IntensitySlope;

    [DataField]
    public float MaxTileIntensity;

    [DataField]
    public float TileBreakScale = 1f;

    [DataField]
    public int MaxTileBreak;

    [DataField]
    public bool CanCreateVacuum;

    /// <summary>
    /// Параметры штатного поражения электричеством.
    /// </summary>
    [DataField]
    public int ShockDamage;

    [DataField]
    public TimeSpan EffectDuration;

    [DataField]
    public bool IgnoreInsulation;

    /// <summary>
    /// Количество добавляемых зарядов огня.
    /// </summary>
    [DataField]
    public float FireStacks;

    /// <summary>
    /// Реагент и доза штатного отравления.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    [DataField]
    public FixedPoint2 ReagentQuantity;

    /// <summary>
    /// Приоритетный список физических ядов для управляемого отравления еды и напитков.
    /// </summary>
    [DataField]
    public List<DeathNoteConsumablePoisonOption> ConsumablePoisons = new();

    /// <summary>
    /// Максимальное количество настоящих блюд или напитков, которые может отравить управляемый сценарий.
    /// </summary>
    [DataField]
    public int PoisonedConsumableLimit = 2;

    /// <summary>
    /// Фракция, назначаемая сценариями наподобие огня турели.
    /// </summary>
    [DataField]
    public ProtoId<NpcFactionPrototype>? Faction;

    /// <summary>
    /// Number of enhanced station energy-turret hits used to distribute the
    /// configured random total damage.
    /// </summary>
    [DataField]
    public int TurretMinimumTargetHits = 2;

    [DataField]
    public int TurretMaximumTargetHits = 3;

    /// <summary>
    /// Maximum range at which the temporary turret controller may directly
    /// maintain its marked target, including while that target is critical.
    /// </summary>
    [DataField]
    public float TurretTargetRange = 10f;

    [DataField]
    public DeathNoteGuidedScenarioType? GuidedScenario;

    [DataField]
    public LocId? GuidanceText;

    [DataField]
    public float StructuralDamage;

    [DataField]
    public int ImpactCount = 4;

    [DataField]
    public TimeSpan ImpactInterval = TimeSpan.FromSeconds(0.9);

    [DataField]
    public TimeSpan AirlockForceCloseDelay = TimeSpan.FromSeconds(0.2);

    [DataField]
    public TimeSpan DoorCloseStageDuration = TimeSpan.FromSeconds(0.05);

    [DataField]
    public TimeSpan VendingFallDuration = TimeSpan.FromSeconds(0.55);

    [DataField]
    public TimeSpan GuidedCleanupGracePeriod = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Сколько времени после запуска эффекта последующая смерть считается вызванной этой записью.
    /// </summary>
    [DataField]
    public TimeSpan EffectTrackingDuration = TimeSpan.FromMinutes(5);

    [DataField]
    public float AirlockAutoCloseDelayModifier = 0.04f;

    [DataField]
    public float DoorwayImpactRadius = 1.1f;

    [DataField]
    public SoundSpecifier? VendingImpactSound;

    [DataField]
    public TimeSpan BluespaceTeleportDelay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan BluespaceCollapseDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan BluespaceReturnDelay = TimeSpan.FromSeconds(5);
}
