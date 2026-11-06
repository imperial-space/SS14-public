using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

public enum CrusherTrophyKind : byte
{
    /// <summary>watcher_wing / ice_wing: задерживает дальние атаки цели.</summary>
    WatcherWing,
    /// <summary>blaster_tubes/magma_wing: следующий выстрел наносит урон.</summary>
    MagmaWing,
    /// <summary>legion_skull: ускоряет перезарядку.</summary>
    LegionSkull,
    /// <summary>goliath_tentacle: урон за недостающее здоровье.</summary>
    GoliathTentacle,
    /// <summary>miner_eye: blooddrunk на 1 секунду.</summary>
    MinerEye,
    /// <summary>tail_spike: огонь и отталкивание вокруг.</summary>
    TailSpike,
    /// <summary>demon_claws: урон и лечение.</summary>
    DemonClaws,
    /// <summary>blaster_tubes: следующий выстрел наносит урон, но медленнее.</summary>
    BlasterTubes,
    /// <summary>vortex_talisman: самонаводящийся преследователь иерофанта.</summary>
    VortexTalisman,
    /// <summary>wendigo_horn: удвоенный урон ударами.</summary>
    WendigoHorn,
    /// <summary>lobster_claw: rebuke цели.</summary>
    LobsterClaw,
    /// <summary>brimdemon_fang: звук и надпись при подрыве.</summary>
    BrimdemonFang,
    /// <summary>bileworm_spewlet: кислота в четыре стороны, бурение породы вокруг.</summary>
    BilewormSpewlet,
    /// <summary>ice_demon_cube: два демонических клона.</summary>
    IceDemonCube,
    /// <summary>wolf_ear: краткое ускорение.</summary>
    WolfEar,
    /// <summary>bear_paw: двойной удар при здоровье ниже половины.</summary>
    BearPaw,
    /// <summary>raptor_feather: выстрелы проходят сквозь союзников.</summary>
    RaptorFeather,
    /// <summary>ice_block_talisman: заморозка цели.</summary>
    IceBlockTalisman,
    /// <summary>broodmother_tongue: шанс щупалец под целью; сжать — иммунитет к лаве.</summary>
    BroodmotherTongue,
    /// <summary>legionnaire_spine: шанс черепа-союзника; потрясти — призвать череп.</summary>
    LegionnaireSpine,
    /// <summary>flesh_glob: лечение ударами и подрывом.</summary>
    FleshGlob,
    /// <summary>retool_kit: только внешний вид дробителя.</summary>
    Retool,
}
