using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.Flash;
using Content.Server.Lightning;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Magic.TurnedToStone;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/datum/action/cooldown/spell/conjure: призыв существ или предметов вокруг.</summary>
[RegisterComponent]
public sealed partial class ImperialConjureSpellComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Summons = new();

    [DataField]
    public int Amount = 1;

    /// <summary>Прибавка к количеству за уровень (summon_amount++ у обезьян).</summary>
    [DataField]
    public int AmountPerLevel;

    [DataField]
    public float Radius = 7f;

    /// <summary>summon_lifespan (0 — навсегда).</summary>
    [DataField]
    public TimeSpan Lifespan = TimeSpan.Zero;

    [DataField]
    public bool RespectsDensity;

    /// <summary>Фракция призванных; заклинатель на время становится её союзником.</summary>
    [DataField]
    public string? Faction;

    [DataField]
    public TimeSpan AllyTime = TimeSpan.FromMinutes(1);
}
