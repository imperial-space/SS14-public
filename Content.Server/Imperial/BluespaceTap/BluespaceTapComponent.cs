using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.EntityTable;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.BluespaceTap;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.BluespaceTap;

/// <summary>
/// Блюспейс-сборщик (obj/machinery/power/bluespace_tap из Paradise): берёт мощность прямо из ВВ-сети,
/// копит очки и вытягивает из других измерений предметы.
/// </summary>
[RegisterComponent]
public sealed partial class BluespaceTapComponent : Component
{
    [DataField(required: true)]
    public List<BluespaceTapProduct> Products = new();

    /// <summary>Порог «материнской жилы» (motherlode_interval, цель станции).</summary>
    [DataField]
    public float MotherlodeInterval = 45000;

    [DataField]
    public TimeSpan ProcessInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public EntProtoId Portal = "ImperialNetherPortalTap";

    [DataField]
    public EntProtoId SpawnEffect = "ImperialBluespaceTapPortalEffect";

    [DataField]
    public EntProtoId FlashEffect = "ImperialBluespaceTapFlash";

    [DataField]
    public EntProtoId RadiationPulse = "ImperialBluespaceTapRadiation";

    [DataField]
    public SoundSpecifier BlinkSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Imperial/BluespaceTap/harvester.ogg");

    [DataField]
    public SoundSpecifier ZapSound = new SoundPathSpecifier("/Audio/Imperial/BluespaceTap/eleczap.ogg");

    [DataField]
    public string RadioChannel = "Engineering";

    [ViewVariables] public float MiningPower;
    [ViewVariables] public float DesiredMiningPower;
    [ViewVariables] public float MinedPoints;
    [ViewVariables] public float Points;
    [ViewVariables] public float TotalPoints;
    [ViewVariables] public float AvailablePower;
    [ViewVariables] public bool AutoShutdown = true;
    [ViewVariables] public bool Stabilizers = true;
    [ViewVariables] public float StabilizerPower;
    [ViewVariables] public bool StabilizerPriority = true;
    [ViewVariables] public HashSet<EntityUid> ActivePortals = new();
    [ViewVariables] public int Spawning;
    [ViewVariables] public bool IsDirty;
    [ViewVariables] public TimeSpan NextProcess;

    /// <summary>Следующие пороги автоматической выдачи по каждому товару.</summary>
    [ViewVariables] public List<float> NextIntervals = new();

    [ViewVariables] public float NextMotherlode;

    /// <summary>Отложенные порталы: время появления.</summary>
    [ViewVariables] public List<TimeSpan> PendingPortals = new();

    [ViewVariables] public float LastDraw;
}
