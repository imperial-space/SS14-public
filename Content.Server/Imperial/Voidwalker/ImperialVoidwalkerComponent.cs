using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Shared.Atmos.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Voidwalker;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Voidwalker;

/// <summary>/mob/living/basic/voidwalker (и /sunwalker).</summary>
[RegisterComponent]
public sealed partial class ImperialVoidwalkerComponent : Component
{
    /// <summary>can_do_abductions: у гелиарха нет.</summary>
    [DataField]
    public bool CanAbduct = true;

    /// <summary>Гелиарх: поджигает, оставляет огонь, жжёт глаза.</summary>
    [DataField]
    public bool Sunwalker;

    [DataField]
    public TimeSpan KidnapTime = TimeSpan.FromSeconds(6);

    /// <summary>conversions_remaining.</summary>
    [DataField]
    public int ConversionsRemaining = 2;

    [DataField]
    public TimeSpan ConversionCooldown = TimeSpan.FromSeconds(60);

    /// <summary>Урон по не-людям (rclick_damage_type = BRUTE).</summary>
    [DataField]
    public DamageSpecifier ObjectDamage = new() { DamageDict = { ["Blunt"] = 13 } };

    /// <summary>charge_damage.</summary>
    [DataField]
    public float DashDamage = 20f;

    [DataField]
    public EntProtoId Loot = "ImperialCosmicSkull";

    [ViewVariables]
    public bool Kidnapping;

    [ViewVariables]
    public TimeSpan NextConversion;

    /// <summary>space_camo: после боя маскировки нет 5 секунд.</summary>
    [ViewVariables]
    public TimeSpan NextCamo;

    [ViewVariables]
    public TimeSpan LastDamaged;

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public bool Grounded;

    [ViewVariables]
    public bool Dashing;

    [ViewVariables]
    public bool DiveAttempt;

    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan UpdateDelay2 = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan MeleeHitDelay = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan MeleeHitDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan KidnapDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan CollideDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan UnsettleDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan UnsettleDoAfterDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan UnsettleDoAfterDuration2 = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan DashHitTime = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan ConvertWallDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public float UpdateChance = 0.02f;

    [DataField]
    public float DamagedChance = 0.5f;

    [DataField]
    public float SurfaceDoAfterRange = 0.5f;

    [DataField]
    public float DashBaseThrowSpeed = 15f;

    [DataField]
    public float ConvertWallDoAfterRange = 1.5f;
}
