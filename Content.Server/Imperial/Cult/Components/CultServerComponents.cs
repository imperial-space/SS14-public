using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Действие подготовленного заклинания крови (datum/action/innate/cult/blood_spell).</summary>
[RegisterComponent]
public sealed partial class CultBloodSpellComponent : Component
{
    [DataField(required: true)]
    public CultSpell Spell;

    /// <summary>charges.</summary>
    [DataField]
    public int Charges = 1;

    /// <summary>health_cost: brute в руку за применение.</summary>
    [DataField]
    public float HealthCost;

    /// <summary>deletes_on_empty.</summary>
    [DataField]
    public bool DeletesOnEmpty = true;

    /// <summary>magic_path: прототип ауры в руке; null — мгновенное заклинание.</summary>
    [DataField]
    public EntProtoId? Aura;

    [DataField]
    public string? Invocation;

    [DataField]
    public string BaseDescription = string.Empty;

    [ViewVariables] public EntityUid? Owner;
    [ViewVariables] public EntityUid? Hand;

    /// <summary>Conceal Presence: следующее применение раскроет.</summary>
    [ViewVariables] public bool Revealing;
}

/// <summary>Аура в руке (obj/item/melee/blood_magic).</summary>
[RegisterComponent]
public sealed partial class CultAuraComponent : Component
{
    [DataField(required: true)]
    public CultSpell Spell;

    [DataField]
    public string? Invocation;

    [ViewVariables] public int Uses = 1;
    [ViewVariables] public float HealthCost;
    [ViewVariables] public EntityUid? Source;
    [ViewVariables] public bool Channeling;
}

/// <summary>Ритуальный предмет (datum/component/cult_ritual_item): черчение, стирание рун.</summary>
[RegisterComponent]
public sealed partial class CultRitualItemComponent : Component
{
    [ViewVariables] public bool Drawing;

    /// <summary>examine_message для культистов.</summary>
    [DataField]
    public LocId? ExamineMessage;

    /// <summary>Щиты вокруг черчения руны Нар'Си.</summary>
    [ViewVariables] public List<EntityUid> Shields = new();
}

/// <summary>Оружие культа: не-культиста отталкивает (obj/item/melee/cultblade/attack).</summary>
[RegisterComponent]
public sealed partial class CultWeaponComponent : Component
{
    /// <summary>free_use.</summary>
    [DataField]
    public bool FreeUse;

    /// <summary>Наказание за попытку удара не-культистом.</summary>
    [DataField]
    public bool PunishAttack = true;

    /// <summary>Сообщение «I wouldn't advise that.» при подборе.</summary>
    [DataField]
    public bool WarnPickup = true;

    [DataField]
    public float Force = 30f;
}

/// <summary>Ритуальный кинжал: бумеранг при броске не-культистом.</summary>
[RegisterComponent]
public sealed partial class CultDaggerComponent : Component;

/// <summary>Кровавая алебарда.</summary>
[RegisterComponent]
public sealed partial class CultHalberdComponent : Component
{
    [ViewVariables] public EntityUid? Owner;
    [ViewVariables] public EntityUid? BondAction;
}

/// <summary>Bola культа: проходит сквозь культистов.</summary>
[RegisterComponent]
public sealed partial class CultBolaComponent : Component;

/// <summary>Eldritch whetstone (sharpener/cult).</summary>
[RegisterComponent]
public sealed partial class CultWhetstoneComponent : Component
{
    [DataField] public int Uses = 1;
    [DataField("damageBonus")] public float Increment = 5f;
    [DataField] public float Max = 40f;
    [DataField] public string Prefix = "darkened";
}

/// <summary>Отметка заточенного оружия.</summary>
[RegisterComponent]
public sealed partial class CultSharpenedComponent : Component;

/// <summary>Veil shifter.</summary>
[RegisterComponent]
public sealed partial class CultVeilShifterComponent : Component
{
    [DataField] public int Uses = 4;
}

/// <summary>Cursed orb (shuttle_curse).</summary>
[RegisterComponent]
public sealed partial class CultCurseOrbComponent : Component;

/// <summary>Blood bolt barrage (arcane_barrage/blood).</summary>
[RegisterComponent]
public sealed partial class CultBloodBoltComponent : Component
{
    [ViewVariables] public HashSet<EntityUid> Healed = new();
}

/// <summary>Blood beam: 9 с заряда + 9 с стрельбы.</summary>
[RegisterComponent]
public sealed partial class CultBloodBeamComponent : Component
{
    [ViewVariables] public bool Charging;
    [ViewVariables] public bool Firing;
    [ViewVariables] public Angle Angle;
    [ViewVariables] public int Pulses;
    [ViewVariables] public TimeSpan NextPulse;
    [ViewVariables] public float Spread = 40f;
    [ViewVariables] public bool Second;
    [ViewVariables] public EntityUid? Shield;
    [ViewVariables] public EntityUid? User;
    [ViewVariables] public List<EntityUid> ChargeEffects = new();
}

/// <summary>Mirror shield.</summary>
[RegisterComponent]
public sealed partial class CultMirrorShieldComponent : Component
{
    [DataField] public int Illusions = 2;
    [ViewVariables] public List<TimeSpan> Restore = new();
}

/// <summary>Zealot's blindfold: жжёт глаза не-культистам.</summary>
[RegisterComponent]
public sealed partial class CultBlindfoldComponent : Component
{
    [ViewVariables] public EntityUid? Wearer;
    [ViewVariables] public TimeSpan NextTick;
}

/// <summary>Nar'Sien hardened armor: раны не-культистам, экзорцизм библией.</summary>
[RegisterComponent]
public sealed partial class CultHardenedArmorComponent : Component
{
    [ViewVariables] public EntityUid? Wearer;
    [ViewVariables] public TimeSpan NextTick;
}

/// <summary>Барьер руны (emergency_shield/cult/barrier).</summary>
[RegisterComponent]
public sealed partial class CultBarrierComponent : Component
{
    [ViewVariables] public EntityUid? Rune;
    [ViewVariables] public bool Active;
}

/// <summary>Пилон: лечение r5 и превращение пола.</summary>
[RegisterComponent]
public sealed partial class CultPylonComponent : Component
{
    [DataField] public float Range = 5f;
    [DataField] public float BruteHeal = 0.4f;
    [DataField] public float BurnHeal = 0.4f;
    [DataField] public float BloodHeal = 0.4f;
    [DataField] public float SimpleHeal = 1.2f;
    [DataField] public float WoundClotting = 0.1f;
    [DataField] public TimeSpan CorruptionCooldown = TimeSpan.FromSeconds(5);

    [ViewVariables] public TimeSpan NextHeal;
    [ViewVariables] public TimeSpan NextCorruption;
}

/// <summary>Рунная дверь: оглушает не-культистов.</summary>
[RegisterComponent]
public sealed partial class CultRunedDoorComponent : Component
{
    [DataField] public bool Runed = true;

    /// <summary>friendly: открывается всем.</summary>
    [DataField] public bool Friendly;

    [ViewVariables] public bool Concealed;
}

/// <summary>Рунная балка: кинжал разрушает с одного удара.</summary>
[RegisterComponent]
public sealed partial class CultGirderComponent : Component;

/// <summary>Стена культа: из неё выходит рунный металл.</summary>
[RegisterComponent]
public sealed partial class CultWallComponent : Component;

/// <summary>Призрак-культист, поддерживаемый руной Царства духов.</summary>
[RegisterComponent]
public sealed partial class CultSpiritGhostComponent : Component
{
    [ViewVariables] public EntityUid? Rune;
    [ViewVariables] public EntityUid? Invoker;
    [ViewVariables] public EntityUid? Shield;
    [ViewVariables] public TimeSpan NextDrain;
    [ViewVariables] public EntityUid? OriginalMind;
    [ViewVariables] public bool Dissolving;
}

/// <summary>Вознёсшийся тёмный дух (Ascend as a Dark Spirit).</summary>
[RegisterComponent]
public sealed partial class CultDarkSpiritComponent : Component
{
    [ViewVariables] public EntityUid? Body;
    [ViewVariables] public EntityUid? Rune;
    [ViewVariables] public List<EntityUid> Actions = new();
    [ViewVariables] public TimeSpan NextMark;
    [ViewVariables] public TimeSpan NextCheck;
}

/// <summary>Тело культиста, ушедшего тёмным духом.</summary>
[RegisterComponent]
public sealed partial class CultSpiritBodyComponent : Component
{
    [ViewVariables] public EntityUid? Ghost;
    [ViewVariables] public EntityUid? Rune;
}

/// <summary>Конструкт культа.</summary>
[RegisterComponent]
public sealed partial class CultConstructComponent : Component
{
    [DataField]
    public CultConstructType ConstructType = CultConstructType.Juggernaut;

    [DataField]
    public List<EntProtoId> Actions = new();

    [DataField]
    public LocId PlaystyleString = "cult-construct-playstyle-generic";

    /// <summary>can_repair: лечит конструктов/тени/постройки (5 brute).</summary>
    [DataField]
    public bool CanRepair;

    [DataField]
    public bool CanRepairSelf;

    /// <summary>Отражение энергии (juggernaut/projectile_hit).</summary>
    [DataField]
    public bool ReflectEnergy;

    [ViewVariables] public EntityUid? Master;
    [ViewVariables] public List<EntityUid> GrantedActions = new();
    [ViewVariables] public List<EntityUid> RuneEffects = new();

    /// <summary>seek_master / seek_prey: чувство крови указывает на SeekTarget.</summary>
    [ViewVariables] public bool Seeking;
    [ViewVariables] public EntityUid? SeekTarget;
    [ViewVariables] public EntityUid? SeekAction;
}

/// <summary>Тень (mob/living/basic/shade).</summary>
[RegisterComponent]
public sealed partial class CultShadeComponent : Component
{
    [ViewVariables] public EntityUid? Master;

    /// <summary>release_time: когда тень вышла из камня душ (null — в камне).</summary>
    [ViewVariables] public TimeSpan? ReleaseTime;
}

/// <summary>Камень душ (obj/item/soulstone).</summary>
[RegisterComponent]
public sealed partial class CultSoulstoneComponent : Component
{
    [DataField] public bool Spent;
    [DataField] public bool Purified;
    [ViewVariables] public EntityUid? Shade;
    [DataField] public string ContainerId = "soulstone_shade";
}

/// <summary>Оболочка конструкта (obj/structure/constructshell).</summary>
[RegisterComponent]
public sealed partial class CultConstructShellComponent : Component;

/// <summary>Нар'Си (obj/narsie).</summary>
[RegisterComponent]
public sealed partial class CultNarSieComponent : Component
{
    [DataField] public float ConsumeRange = 12f;
    [DataField] public float Speed = 1f;
    [ViewVariables] public EntityUid? Target;
    [ViewVariables] public TimeSpan NextMove;
    [ViewVariables] public TimeSpan NextRetarget;
    [ViewVariables] public TimeSpan NextMesmerize;
    [ViewVariables] public System.Numerics.Vector2 MoveDir;
    [ViewVariables] public TimeSpan SpawnAnimEnd;
    [ViewVariables] public bool Started;

    /// <summary>souls_needed / soul_goal / souls: массовое обращение.</summary>
    [ViewVariables] public HashSet<EntityUid> SoulsNeeded = new();
    [ViewVariables] public int SoulGoal;
    [ViewVariables] public int Souls;
}

/// <summary>Отметка «кровавой метки» над целью.</summary>
[RegisterComponent]
public sealed partial class CultBloodMarkComponent : Component
{
    [ViewVariables] public EntityUid? Target;
}

/// <summary>Смещение импульса мастера: схваченная цель.</summary>
[RegisterComponent]
public sealed partial class CultPulseHolderComponent : Component
{
    [ViewVariables] public EntityUid? Seized;
}

/// <summary>Временный эффект, следующий за сущностью.</summary>
[RegisterComponent]
public sealed partial class CultFollowEffectComponent : Component;

/// <summary>Звуки культа в одном месте (для yml-перекрытия при желании).</summary>
public static class CultSounds
{
    public const string Root = "/Audio/Imperial/BloodCult/";
    public static readonly SoundSpecifier Magic = new SoundPathSpecifier(Root + "magic.ogg");
    public static readonly SoundSpecifier TeleportDiss = new SoundPathSpecifier(Root + "teleport_diss.ogg");
    public static readonly SoundSpecifier Disintegrate = new SoundPathSpecifier(Root + "disintegrate.ogg");
    public static readonly SoundSpecifier PortalEnter = new SoundPathSpecifier(Root + "portal_travel.ogg");
    public static readonly SoundSpecifier PortalCreated = new SoundCollectionSpecifier("ImperialCultPortalOpen");
    public static readonly SoundSpecifier BloodcultGain = new SoundPathSpecifier(Root + "bloodcult_gain.ogg");
    public static readonly SoundSpecifier GhostWhisper = new SoundPathSpecifier(Root + "ghost_whisper.ogg");
    public static readonly SoundSpecifier GhostyWind = new SoundPathSpecifier(Root + "ghosty_wind.ogg");
    public static readonly SoundSpecifier EnterBlood = new SoundPathSpecifier(Root + "enter_blood.ogg");
    public static readonly SoundSpecifier ExitBlood = new SoundPathSpecifier(Root + "exit_blood.ogg");
    public static readonly SoundSpecifier PopeEntry = new SoundPathSpecifier(Root + "pope_entry.ogg");
    public static readonly SoundSpecifier Ghost2 = new SoundPathSpecifier(Root + "ghost2.ogg");
    public static readonly SoundSpecifier Ghost = new SoundPathSpecifier(Root + "ghost.ogg");
    public static readonly SoundSpecifier Slice = new SoundPathSpecifier(Root + "slice.ogg");
    public static readonly SoundSpecifier CableCuff = new SoundPathSpecifier(Root + "cablecuff.ogg");
    public static readonly SoundSpecifier AlienPrying = new SoundPathSpecifier(Root + "airlock_alien_prying.ogg");
    public static readonly SoundSpecifier AirlockForced = new SoundPathSpecifier(Root + "airlockforced.ogg");
    public static readonly SoundSpecifier StaffHealing = new SoundPathSpecifier(Root + "staff_healing.ogg");
    public static readonly SoundSpecifier Smoke = new SoundPathSpecifier(Root + "smoke.ogg");
    public static readonly SoundSpecifier NarsieAttack = new SoundPathSpecifier(Root + "narsie_attack.ogg");
    public static readonly SoundSpecifier ThudSwoosh = new SoundPathSpecifier(Root + "thudswoosh.ogg");
    public static readonly SoundSpecifier ISeeYou = new SoundCollectionSpecifier("ImperialCultISeeYou");
    public static readonly SoundSpecifier ImHere = new SoundCollectionSpecifier("ImperialCultImHere");
    public static readonly SoundSpecifier OverHere = new SoundCollectionSpecifier("ImperialCultOverHere");
    public static readonly SoundSpecifier TurnAround = new SoundCollectionSpecifier("ImperialCultTurnAround");
    public static readonly SoundSpecifier VeryFarNoise = new SoundPathSpecifier(Root + "veryfar_noise.ogg");
    public static readonly SoundSpecifier Sheath = new SoundPathSpecifier(Root + "sheath.ogg");
    public static readonly SoundSpecifier ResonatorBlast = new SoundPathSpecifier(Root + "resonator_blast.ogg");
    public static readonly SoundSpecifier Deconstruct = new SoundPathSpecifier(Root + "deconstruct.ogg");
    public static readonly SoundSpecifier GlassBreak1 = new SoundPathSpecifier(Root + "glassbr1.ogg");
    public static readonly SoundSpecifier GlassBreak2 = new SoundPathSpecifier(Root + "glassbr2.ogg");
    public static readonly SoundSpecifier GlassBreak3 = new SoundPathSpecifier(Root + "glassbr3.ogg");
    public static readonly SoundSpecifier WandTeleport = new SoundPathSpecifier(Root + "wand_teleport.ogg");
    public static readonly SoundSpecifier Splat = new SoundPathSpecifier(Root + "splat.ogg");
    public static readonly SoundSpecifier LightningChargeup = new SoundPathSpecifier(Root + "lightning_chargeup.ogg");
    public static readonly SoundSpecifier Wail = new SoundPathSpecifier(Root + "wail.ogg");
    public static readonly SoundSpecifier Parry = new SoundPathSpecifier(Root + "parry.ogg");
    public static readonly SoundSpecifier Ric5 = new SoundPathSpecifier(Root + "ric5.ogg");
    public static readonly SoundSpecifier ThrowTap = new SoundPathSpecifier(Root + "throwtap.ogg");
    public static readonly SoundSpecifier PhaseIn = new SoundPathSpecifier(Root + "phasein.ogg");
    public static readonly SoundSpecifier GhostItemAttack = new SoundPathSpecifier(Root + "ghostitemattack.ogg");
    public static readonly SoundSpecifier ConstructForm = new SoundPathSpecifier(Root + "constructform.ogg");
    public static readonly SoundSpecifier DemonDies = new SoundPathSpecifier(Root + "demon_dies.ogg");
    public static readonly SoundSpecifier PrayChaplain = new SoundPathSpecifier(Root + "pray_chaplain.ogg");
    public static readonly SoundSpecifier Notice1 = new SoundPathSpecifier(Root + "notice1.ogg");
    public static readonly SoundSpecifier Notice3 = new SoundPathSpecifier(Root + "notice3.ogg");
    public static readonly SoundSpecifier NukeAlarm = new SoundPathSpecifier(Root + "nuke_alarm.ogg");
    public static readonly SoundSpecifier AnnounceSyndi = new SoundPathSpecifier(Root + "announce_syndi.ogg");
    public static readonly SoundSpecifier TerminalOff = new SoundPathSpecifier(Root + "terminal_off.ogg");
    public static readonly SoundSpecifier NarsieRises = new SoundPathSpecifier(Root + "narsie_rises.ogg");
    public static readonly SoundSpecifier ExplosionDistant = new SoundPathSpecifier(Root + "explosion_distant.ogg");
    public static readonly SoundSpecifier NarsieSummon = new SoundPathSpecifier(Root + "narsie_summon.ogg");
    public static readonly SoundSpecifier NarsieRisen = new SoundPathSpecifier(Root + "narsie_risen.ogg");
    public static readonly SoundSpecifier CultSummonAnnounce = new SoundPathSpecifier(Root + "cult_summon.ogg");
    public static readonly SoundSpecifier CultRiseAnnounce = new SoundPathSpecifier(Root + "cult_rise_announcement.ogg");
    public static readonly SoundSpecifier MagicBlockMind = new SoundPathSpecifier(Root + "magic_block_mind.ogg");
    public static readonly SoundSpecifier Sparks = new SoundCollectionSpecifier("sparks");
    public static readonly SoundSpecifier Forcewall = new SoundPathSpecifier(Root + "forcewall.ogg");
    public static readonly SoundSpecifier EtherealEnter = new SoundPathSpecifier(Root + "ethereal_enter.ogg");
    public static readonly SoundSpecifier EtherealExit = new SoundPathSpecifier(Root + "ethereal_exit.ogg");
    public static readonly SoundSpecifier MagicMissile = new SoundPathSpecifier(Root + "magic_missile.ogg");
    public static readonly SoundSpecifier MissileHit = new SoundPathSpecifier(Root + "mm_hit.ogg");
    public static readonly SoundSpecifier SummonItems = new SoundPathSpecifier(Root + "summonitems_generic.ogg");
}

/// <summary>Лист рунного металла: радиальное меню построек.</summary>
[RegisterComponent]
public sealed partial class CultRunedMetalComponent : Component;

/// <summary>Предмет призрака культа (DROPDEL): исчезает вместе с телом.</summary>
[RegisterComponent]
public sealed partial class CultGhostItemComponent : Component;

/// <summary>Фазовый сдвиг призрака (ethereal_jaunt/shift).</summary>
[RegisterComponent]
public sealed partial class CultPhasedComponent : Component
{
    [ViewVariables] public TimeSpan End;
    [ViewVariables] public Robust.Shared.Map.EntityCoordinates? LastSafe;
    [ViewVariables] public Dictionary<string, (int Mask, int Layer)> Masks = new();
}

/// <summary>Снаряд «Эхо перчатки» джаггернаута.</summary>
[RegisterComponent]
public sealed partial class CultGauntletProjectileComponent : Component;

/// <summary>Малая магическая ракета ремесленника.</summary>
[RegisterComponent]
public sealed partial class CultMissileProjectileComponent : Component
{
    [ViewVariables] public EntityUid? Target;
    [DataField] public float Speed = 4f;
}

/// <summary>block_chance оружия культа (парирует только культист).</summary>
[RegisterComponent]
public sealed partial class CultParryComponent : Component
{
    [DataField] public float Chance = 0.25f;
    [DataField] public float WieldedMultiplier = 1f;
}

/// <summary>TRAIT_IMMOBILIZED во время стрельбы кровавым лучом.</summary>
[RegisterComponent]
public sealed partial class CultImmobilizedComponent : Component;

/// <summary>Время жизни иллюзии (addtimer death).</summary>
[RegisterComponent]
public sealed partial class CultIllusionLifeComponent : Component
{
    [ViewVariables] public TimeSpan End;
}

/// <summary>Некультист держит зеркальный щит: щит предаёт его (иллюзии-двойники).</summary>
[RegisterComponent]
public sealed partial class CultMirrorBearerComponent : Component
{
    [ViewVariables] public EntityUid? Shield;
}
