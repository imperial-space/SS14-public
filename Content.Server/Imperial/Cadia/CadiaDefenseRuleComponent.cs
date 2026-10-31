using Content.Shared.Imperial.Cadia;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Правило «Оборона Кадии»: фазы войны, волны подкреплений обеих сторон, пилоны и цитадель.
/// </summary>
[RegisterComponent, Access(typeof(CadiaDefenseRuleSystem))]
public sealed partial class CadiaDefenseRuleComponent : Component
{
    [DataField]
    public TimeSpan PreparationTime = TimeSpan.FromMinutes(15);

    [DataField]
    public TimeSpan BombardmentTime = TimeSpan.FromMinutes(10);

    [DataField]
    public TimeSpan AssaultTime = TimeSpan.FromMinutes(30);

    [DataField]
    public TimeSpan FinaleTime = TimeSpan.FromMinutes(15);

    /// <summary>Сколько длится эвакуация после падения Кадии.</summary>
    [DataField]
    public TimeSpan EvacuationTime = TimeSpan.FromMinutes(8);

    /// <summary>Спасено столько или больше — организованное отступление, а не разгром.</summary>
    [DataField]
    public int EvacGoodTier = 25;

    /// <summary>Спасено столько или больше — горькое бегство; меньше — полный разгром.</summary>
    [DataField]
    public int EvacPoorTier = 10;

    /// <summary>Голос диктора для вступления; пока записи нет, сцена идёт только с текстом и музыкой.</summary>
    [DataField]
    public SoundSpecifier? IntroVoice;

    /// <summary>Подкрепления защитников: реже, но с укреплённых позиций.</summary>
    [DataField]
    public TimeSpan ImperiumWaveInterval = TimeSpan.FromMinutes(4);

    /// <summary>Орды Хаоса накатывают чаще.</summary>
    [DataField]
    public TimeSpan ChaosWaveInterval = TimeSpan.FromMinutes(3);

    /// <summary>Сила варпа за каждый павший пилон.</summary>
    [DataField]
    public int WarpPerPylon = 25;

    /// <summary>Сила варпа за каждого павшего защитника.</summary>
    [DataField]
    public int WarpPerKill = 1;

    /// <summary>С начала штурма варп сочится сам: сколько силы в минуту. При обстреле вдвое меньше.</summary>
    [DataField]
    public int WarpPerMinute = 6;

    /// <summary>С чем Хаос приходит в начало войны: хватит на пару демонов к первой высадке.</summary>
    [DataField]
    public int StartWarp = 20;

    /// <summary>Сторона не получает новых мест, если живых больше, чем у другой, в это число раз и ещё на FairSideSlack.</summary>
    [DataField]
    public float FairSideRatio = 1.3f;

    [DataField]
    public int FairSideSlack = 6;

    /// <summary>
    /// Места ролей в прототипах рассчитаны на столько игроков. При другом онлайне массовые роли (больше одного места)
    /// растут или сжимаются пропорционально, но не меньше одного места; одиночные роли не меняются.
    /// </summary>
    [DataField]
    public int SlotReferencePlayers = 100;

    /// <summary>Во сколько раз места могут вырасти сверх расчётных при большом онлайне.</summary>
    [DataField]
    public float MaxSlotScale = 1.5f;

    /// <summary>Онлайн берётся с шагом в столько игроков, чтобы места не пересчитывались от каждого входа.</summary>
    [DataField]
    public int SlotOnlineStep = 5;

    /// <summary>Онлайн (с шагом), по которому сейчас посчитаны места.</summary>
    [ViewVariables]
    public int SlotOnline;

    /// <summary>Стороны, которым сейчас закрыт набор из-за перекоса.</summary>
    [ViewVariables]
    public HashSet<CadiaFaction> ClosedSides = new();

    /// <summary>Стороны, которым набор закрыл ведущий.</summary>
    [ViewVariables]
    public HashSet<CadiaFaction> ForcedClosed = new();

    /// <summary>Игроки, которых ведущий убрал с поля до конца ивента.</summary>
    [ViewVariables]
    public HashSet<Robust.Shared.Network.NetUserId> BannedPlayers = new();

    /// <summary>Кто уже видел вступительную сцену.</summary>
    [ViewVariables]
    public HashSet<Robust.Shared.Network.NetUserId> IntroSeen = new();

    /// <summary>Чем Хаос сломил Кадию: попадёт в итог после эвакуации.</summary>
    [ViewVariables]
    public string? FallReason;

    [ViewVariables]
    public TimeSpan NextWarpSeep;

    [DataField]
    public SoundSpecifier PhaseSound = new SoundPathSpecifier("/Audio/Announcements/war.ogg");

    [DataField]
    public SoundSpecifier BombardmentSound = new SoundPathSpecifier("/Audio/Imperial/MeteorShield/airraid.ogg");

    [DataField]
    public SoundSpecifier FinaleSound = new SoundPathSpecifier("/Audio/Misc/narsie_rises.ogg");

    [DataField]
    public SoundSpecifier WaveImperiumSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    [DataField]
    public SoundSpecifier WaveChaosSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/scary_horn.ogg");

    [DataField]
    public SoundSpecifier PylonFallSound = new SoundPathSpecifier("/Audio/Imperial/DeadSector/Sound/Ambient/Blowout/blowoutambientrumble.ogg");

    [DataField]
    public SoundSpecifier CitadelAlarmSound = new SoundPathSpecifier("/Audio/Misc/siren.ogg");

    [DataField]
    public SoundSpecifier ImperiumVictorySound = new SoundPathSpecifier("/Audio/Misc/notice2.ogg");

    [DataField]
    public SoundSpecifier ChaosVictorySound = new SoundPathSpecifier("/Audio/Misc/ratvar_rises.ogg");

    // ---------- Состояние ----------

    [ViewVariables]
    public EntityUid? State;

    /// <summary>Спавнеры призрачных ролей по ролям.</summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> Spawners = new();

    /// <summary>Пороги захвата цитадели, о которых уже предупредили (25/50/75%).</summary>
    [ViewVariables]
    public HashSet<int> CitadelWarnings = new();

    [ViewVariables]
    public int ImperiumDeaths;

    [ViewVariables]
    public int ChaosDeaths;

    [ViewVariables]
    public int PylonsFallen;

    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>Дополнительные места ролей: геносемя возвращает братьев в строй.</summary>
    [ViewVariables]
    public Dictionary<string, int> BonusSlots = new();

    /// <summary>Когда сработает Экстерминатус.</summary>
    [ViewVariables]
    public TimeSpan? ExterminatusAt;

    [ViewVariables]
    public CadiaFaction? Winner;

    [ViewVariables]
    public string? WinReason;

    /// <summary>Когда завершить раунд после объявления победы (даём досмотреть финал).</summary>
    [ViewVariables]
    public TimeSpan? EndRoundAt;

    [DataField]
    public TimeSpan EndRoundDelay = TimeSpan.FromSeconds(26);
}

/// <summary>Спавнер призрачной роли войны: какую роль он выдаёт.</summary>
[RegisterComponent]
public sealed partial class CadiaRoleSpawnerComponent : Component
{
    [DataField]
    public ProtoId<CadiaRolePrototype> Role;
}

/// <summary>Боец ещё не получил вводную — отдадим, когда в тело войдёт игрок.</summary>
[RegisterComponent]
public sealed partial class CadiaBriefingPendingComponent : Component;
