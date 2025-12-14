using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Flash;
using Content.Server.Jittering;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly JitteringSystem _jittering = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly FlashSystem _flash = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeAttacks();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateBoss();
        UpdateExplodingBoss();
        UpdateSpiked();
        UpdateMark();
        UpdateSpikeMarker();
        UpdateRunes();
        UpdateBHell();
    }

    public void StartBossfight(List<EntityUid> players, EntityUid boss)
    {
        var bossComp = EnsureComp<BossComponent>(boss);

        var grid = Transform(boss).GridUid;
        if (grid == null)
            return;

        List<EntityUid> positions = new();
        var enumerator = Transform(grid.Value).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (_tag.HasTag(child, (ProtoId<TagPrototype>)"BossSpawnPosition"))
            {
                positions.Add(child);
            }
        }

        foreach (var player in players)
        {
            bossComp.Players.Add(player);

            _transform.SetCoordinates(player, Transform(_random.Pick(positions)).Coordinates);
            EnsureComp<FightingBossComponent>(player);
        }

        bossComp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(13);
        bossComp.Active = true;

        var songEnt = _audio.PlayGlobal(bossComp.Song, Filter.BroadcastGrid(grid.Value), false);
        bossComp.SongEntity = songEnt?.Entity;
        bossComp.NextSongPlay = _timing.CurTime + TimeSpan.FromSeconds(bossComp.SongDuration);
    }

    public void DamageBoss(EntityUid boss, float damage)
    {
        if (!TryComp<BossComponent>(boss, out var bossComp))
            return;

        bossComp.Health -= damage;

        var max = bossComp.Stages.Where(x => x.Value.Threshold >= bossComp.Health).Select(x => x.Key).Max();
        if (bossComp.Stage != max)
        {
            _appearance.SetData(boss, BossStageVisuals.Stage, max);

            if (bossComp.Stage < max)
            {
                _audio.PlayPvs(bossComp.Stages[max].Sound, boss);
                _jittering.DoJitter(boss, TimeSpan.FromSeconds(3), true);
            }

            bossComp.Stage = max;
        }

        if (bossComp.Health <= 0)
        {
            var beforeEv = new BeforeBossDefeatEvent();
            RaiseLocalEvent(boss, ref beforeEv);

            if (beforeEv.Cancelled)
                return;

            var ev = new BossDefeatedEvent(boss);
            RaiseLocalEvent(ref ev);

            BossDefeated(boss, bossComp);
        }
    }

    public void BossDefeated(EntityUid boss, BossComponent component)
    {
        if (!component.Active)
            return;

        _audio.PlayGlobal(_audio.ResolveSound(component.DefeatSound), Filter.Broadcast(), true);
        if (component.DefeatMessage != string.Empty)
            _chat.ChatMessageToAll(ChatChannel.Radio, Loc.GetString(component.DefeatMessage), Loc.GetString(component.DefeatMessage), EntityUid.Invalid, false, true, Color.FromHex("#92ec00"));

        _jittering.DoJitter(boss, TimeSpan.FromSeconds(10), true, 10, 7);
        _audio.Stop(component.SongEntity);
        component.Active = false;

        EntityManager.AddComponents(boss, component.ComponentsOnDefeat);
    }

    public void BossWon(EntityUid boss, BossComponent component)
    {
        if (!component.Active)
            return;

        _audio.PlayGlobal(_audio.ResolveSound(component.LoseSound), Filter.Broadcast(), true);
        if (component.LoseMessage != string.Empty)
            _chat.ChatMessageToAll(ChatChannel.Radio, Loc.GetString(component.LoseMessage), Loc.GetString(component.LoseMessage), EntityUid.Invalid, false, true, Color.FromHex("#cc3a00"));

        var ev = new BossWonEvent(boss);
        RaiseLocalEvent(ref ev);

        SendPlayersBack(component.Players);
        component.Active = false;
    }

    public void SendPlayersBack(List<EntityUid> players)
    {
        var query = AllEntityQuery<MagicBarrierComponent>();

        var list = new List<EntityCoordinates>();
        while (query.MoveNext(out var uid, out var comp))
        {
            list.Add(Transform(uid).Coordinates);
        }

        if (list.Count == 0)
        {
            foreach (var item in EntityManager.AllEntities<HumanoidAppearanceComponent>().Where(x => players.Contains(x.Owner)))
                list.Add(Transform(item.Owner).Coordinates);
        }

        foreach (var item in players)
        {
            _flash.Flash(item, null, null, TimeSpan.FromSeconds(5), 1, false);
            _transform.SetCoordinates(item, _random.Pick(list));
        }
    }

    private List<BossAttack> GetBossAttacks(BossComponent comp)
    {
        List<BossAttack> attacks = new();

        foreach (var stage in comp.Stages.Where(x => x.Key <= comp.Stage))
            attacks.AddRange(stage.Value.Attacks);

        return attacks;
    }

    private void UpdateBoss()
    {
        var query = EntityQueryEnumerator<BossComponent>();
        while (query.MoveNext(out var uid, out var bossComp))
        {
            if (bossComp.Players.Count == 0 || !bossComp.Active)
                continue;

            if (bossComp.NextSongPlay <= _timing.CurTime)
            {
                var songEnt = _audio.PlayGlobal(bossComp.Song, Filter.BroadcastGrid(Transform(uid).GridUid ?? EntityUid.Invalid), false);
                bossComp.SongEntity = songEnt?.Entity;
                bossComp.NextSongPlay = _timing.CurTime + TimeSpan.FromSeconds(bossComp.SongDuration);
            }

            if (bossComp.NextAttack > _timing.CurTime)
                continue;

            if (!bossComp.Players.Where(x => _mobState.IsAlive(x)).Any())
            {
                BossWon(uid, bossComp);
                QueueDel(uid);
                continue;
            }

            var list = GetBossAttacks(bossComp);
            var stage = bossComp.Stages[bossComp.Stage];
            if (!list.Any())
                continue;

            var count = _random.Pick(new List<int>() { stage.AttacksPerTime.Item1, stage.AttacksPerTime.Item2 });

            _random.Shuffle(list);
            for (var i = 0; i < count && i < list.Count; i++)
            {
                var attack = PickAttack(list);
                if (attack == null)
                    break;

                if (attack.NextAttack > _timing.CurTime)
                    continue;

                if (attack.Execute(uid, bossComp.Players, EntityManager))
                    attack.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(attack.Cooldown);
            }

            bossComp.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(stage.StageDelay);
        }
    }

    private void UpdateExplodingBoss()
    {
        var query = EntityQueryEnumerator<ExplosionDefeatedBossComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextExplosion)
                continue;

            _explosion.QueueExplosion(_transform.ToMapCoordinates(Transform(uid).Coordinates), ExplosionSystem.DefaultExplosionPrototypeId, 85, 10, 0, null, 0, 0, false);
            comp.NextExplosion = _timing.CurTime + TimeSpan.FromSeconds(comp.Delay);
            comp.Index++;

            if (comp.Index >= comp.Explosions)
            {
                RemComp<ExplosionDefeatedBossComponent>(uid);
                SendPlayersBack(Comp<BossComponent>(uid).Players);
            }
        }
    }

    private BossAttack? PickAttack(List<BossAttack> list)
    {
        var picks = list.Select(x => (x, x.Priority)).ToDictionary();
        var sum = picks.Values.Sum();
        var accumulated = 0f;

        var rand = _random.NextFloat() * sum;

        foreach (var (key, weight) in picks)
        {
            accumulated += weight;

            if (accumulated >= rand)
            {
                list.Remove(key);
                return key;
            }
        }

        return null;
    }
}
