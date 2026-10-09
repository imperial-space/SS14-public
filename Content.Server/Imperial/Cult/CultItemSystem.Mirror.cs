using System.Linq;
using Content.Server.Imperial.Cult.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Cult;

/// <summary>shield/mirror: блок, иллюзии, разрушение, предательство неверного.</summary>
public sealed partial class CultItemSystem
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    private const string IllusionProto = "MobCultIllusion";

    private void InitializeMirror()
    {
        SubscribeLocalEvent<CultMirrorShieldComponent, GotEquippedHandEvent>(OnMirrorEquipped);
        SubscribeLocalEvent<CultMirrorShieldComponent, GotUnequippedHandEvent>(OnMirrorUnequipped);
        SubscribeLocalEvent<CultMirrorBearerComponent, BeforeDamageChangedEvent>(OnBearerDamage);
        SubscribeLocalEvent<CultIllusionComponent, MobStateChangedEvent>(OnIllusionState);
    }

    private void OnMirrorEquipped(Entity<CultMirrorShieldComponent> ent, ref GotEquippedHandEvent args)
    {
        if (_cult.IsCultist(args.User))
            return;
        EnsureComp<CultMirrorBearerComponent>(args.User).Shield = ent;
    }

    private void OnMirrorUnequipped(Entity<CultMirrorShieldComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (TryComp<CultMirrorBearerComponent>(args.User, out var bearer) && bearer.Shield == ent.Owner)
            RemComp<CultMirrorBearerComponent>(args.User);
    }

    /// <summary>Некультист: 50% — «предатель» нападает на владельца.</summary>
    private void OnBearerDamage(Entity<CultMirrorBearerComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || !args.Damage.AnyPositive() || _cult.IsCultist(ent))
            return;
        if (!_random.Prob(0.5f))
            return;
        SpawnIllusion(ent, ent, life: 10f);
        _cult.Message(ent, Loc.GetString("cult-mirror-betrayed"));
    }

    /// <summary>Культист с зеркальным щитом в руке. Возвращает true, если удар заблокирован.</summary>
    private bool TryMirrorBlock(EntityUid owner, EntityUid shield, CultMirrorShieldComponent mirror, bool projectile, ref BeforeDamageChangedEvent args)
    {
        if (projectile)
        {
            var brute = args.Damage.GetTotal().Float();
            if (brute >= 30)
            {
                var coords = Transform(owner).Coordinates;
                _popup.PopupCoordinates(Loc.GetString("cult-mirror-shatter", ("item", args.Origin!.Value)), coords, PopupType.MediumCaution);
                _cult.Effect("CultEffectSparks", coords, randomDir: true);
                _audio.PlayPvs(CultSounds.GlassBreak3, coords);
                _cult.Paralyze(owner, 2.5f);
                QueueDel(shield);
                return false;
            }
        }

        // block_chance 50.
        if (!_random.Prob(0.5f))
            return false;

        args.Cancelled = true;
        _audio.PlayPvs(CultSounds.Ric5, owner);
        if (mirror.Illusions <= 0)
            return true;

        mirror.Illusions--;
        mirror.Restore.Add(_timing.CurTime + TimeSpan.FromSeconds(45));
        if (_random.Prob(0.6f))
            SpawnIllusion(owner, null, life: 7f);
        else
            SpawnIllusion(owner, null, life: 7f, escape: true);
        return true;
    }

    /// <summary>illusion/full_setup(): копия внешности; из-за isnull() в SS13 здоровье 100 и урон 5 всегда.</summary>
    public EntityUid SpawnIllusion(EntityUid original, EntityUid? target, float life, bool escape = false)
    {
        var coords = Transform(original).Coordinates;
        var illusion = Spawn(escape ? IllusionProto + "Escape" : IllusionProto, coords);
        var comp = EnsureComp<CultIllusionComponent>(illusion);
        comp.Source = original;
        Dirty(illusion, comp);
        _meta.SetEntityName(illusion, Name(original));
        _transform.SetLocalRotation(illusion, Transform(original).LocalRotation);
        EnsureComp<CultIllusionLifeComponent>(illusion).End = _timing.CurTime + TimeSpan.FromSeconds(life);

        if (target != null)
        {
            // faction_override = FACTION_CULT: нападает на владельца.
            _faction.ClearFactions(illusion);
            _faction.AddFaction(illusion, CultSystem.CultFaction);
            if (TryComp<HTNComponent>(illusion, out var htn))
                htn.Blackboard.SetValue("Target", target.Value);
        }
        return illusion;
    }

    private void OnIllusionState(Entity<CultIllusionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;
        KillIllusion(ent);
    }

    private void KillIllusion(EntityUid illusion)
    {
        if (TerminatingOrDeleted(illusion))
            return;
        _popup.PopupEntity(Loc.GetString("cult-illusion-death", ("target", illusion)), illusion, PopupType.Medium);
        QueueDel(illusion);
    }

    private void UpdateMirror(TimeSpan now)
    {
        var illusions = EntityQueryEnumerator<CultIllusionLifeComponent>();
        var dead = new List<EntityUid>();
        while (illusions.MoveNext(out var uid, out var life))
        {
            if (now >= life.End)
                dead.Add(uid);
        }
        foreach (var uid in dead)
            KillIllusion(uid);

        var shields = EntityQueryEnumerator<CultMirrorShieldComponent>();
        while (shields.MoveNext(out var uid, out var mirror))
        {
            if (mirror.Restore.Count == 0 || now < mirror.Restore[0])
                continue;
            mirror.Restore.RemoveAt(0);
            mirror.Illusions++;
            // readd(): сообщение, когда иллюзии восстановлены полностью.
            if (mirror.Illusions == 2 && Transform(uid).ParentUid is { Valid: true } holder && HasComp<Content.Shared.Mobs.Components.MobStateComponent>(holder))
                _cult.Message(holder, Loc.GetString("cult-mirror-restored"));
        }
    }
}
