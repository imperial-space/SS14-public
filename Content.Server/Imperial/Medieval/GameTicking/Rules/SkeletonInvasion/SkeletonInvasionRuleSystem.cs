using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.GameTicking.Rules;
using System.Linq;
using Content.Server.Chat.Systems;
using Robust.Shared.Random;
using Content.Server.MagicBarrier.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Content.Server.Imperial.Medieval.SkeletonInvasion;
using Content.Shared.Humanoid;
using Content.Server.Imperial.Medieval.Boss;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Content.Server.GameTicking;
using Content.Server.Flash;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Utility;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Server.Storage.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.Imperial.Medieval.SkeletonInvasion;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class SkeletonInvasionRuleSystem : GameRuleSystem<SkeletonInvasionRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IChatManager _chatMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BossSystem _boss = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly FlashSystem _flash = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StorageSystem _storage = default!;

    private EntityUid _bossUid = EntityUid.Invalid;
    private RoundResult _result = RoundResult.NoBoss;
    private TimeSpan? _endTime;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IgnoreBossStartComponent, GhostRoleSpawnerUsedEvent>(OnSkeletonSpawn);
        SubscribeLocalEvent<SkullBossStandCompletedEvent>(OnSkullStandCompleted);
        SubscribeLocalEvent<BossDefeatedEvent>(OnBossDefeated);
        SubscribeLocalEvent<BossWonEvent>(OnBossWin);
    }

    protected override void Added(EntityUid uid, SkeletonInvasionRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (!_mapLoader.TryLoadMap(component.Arena, out var map, out var grids, new DeserializationOptions() { InitializeMaps = true }))
            return;

        _bossUid = EntityManager.AllEntities<BossComponent>().Where(x => Transform(x).MapUid == map.Value.Owner).First();
    }

    protected override void Started(EntityUid uid, SkeletonInvasionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var cursespawners = EntityManager.AllEntities<MagicBarrierCurseSpawnComponent>().Select(x => x.Owner).ToList();
        _chat.DispatchGlobalAnnouncement("Посланники темного повелителя замечен на этих землях.", playSound: true, colorOverride: Color.DeepPink, sender: "Барьер");
        component.NextSpawn = _timing.CurTime;
    }

    protected override void ActiveTick(EntityUid uid, SkeletonInvasionRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (_endTime != null && _endTime <= _timing.CurTime)
        {
            _endTime = null;
            GameTicker.EndRound();
            GameTicker.RestartRound();
            return;
        }

        if (component.NextSpawn > _timing.CurTime)
            return;

        var cursespawners = EntityManager.AllEntities<MagicBarrierCurseSpawnComponent>();

        for (var i = 0; i < component.SpawnCount; i++)
        {
            var fighter = Spawn("MedievalSpawnNecroFighterPreset", Transform(_random.Pick(cursespawners).Owner).Coordinates);
            var comp = EnsureComp<SpawnSkullPartOnGhostRoleTakeComponent>(fighter);
            comp.Prototypes = component.SkullParts.Where(x => !EntityManager.AllEntities<SkullBossStandComponent>().First().Comp.AttachedProtos.Contains(x)).ToList();
        }

        component.SpawnCount++;
        component.NextSpawn = _timing.CurTime + TimeSpan.FromMinutes(_random.NextFloat(component.SpawnDelay.Item1, component.SpawnDelay.Item2));
    }

    protected override void AppendRoundEndText(EntityUid uid, SkeletonInvasionRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var resultText = _result switch
        {
            RoundResult.NoBoss => Loc.GetString("skeletron-roundend-no-boss"),
            RoundResult.BossDefeated => Loc.GetString("skeletron-roundend-defeated"),
            RoundResult.BossWon => Loc.GetString("skeletron-roundend-lost"),
            RoundResult.SkeletonsWon => Loc.GetString("skeletron-roundend-skeletons"),
            _ => ""
        };

        args.AddLine(resultText);
    }

    protected override void Ended(EntityUid uid, SkeletonInvasionRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        _endTime = null;
    }

    private void OnSkeletonSpawn(EntityUid uid, IgnoreBossStartComponent comp, GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<SpawnSkullPartOnGhostRoleTakeComponent>(args.Spawner, out var list))
            return;

        if (!list.Prototypes.Any())
            return;

        _inventory.TryGetSlotEntity(uid, "back", out var fighterBack);
        if (HasComp<StorageComponent>(fighterBack))
        {
            var item = Spawn(_random.Pick(list.Prototypes));
            _storage.Insert(fighterBack.Value, item, out _);
        }
    }

    private void OnSkullStandCompleted(SkullBossStandCompletedEvent args)
    {
        if (args.PurifiedParts < args.Parts / 2)
        {
            var msg = "Проклятый череп полностью собран. Трепещите смертные,  армия тьмы грядет и мир уже не будет прежним. Объединение - единственный шанс на спасение...";
            _chatMan.ChatMessageToAll(ChatChannel.Radio, msg, msg, EntityUid.Invalid, false, true, colorOverride: Color.OrangeRed);
            _audio.PlayGlobal(new SoundPathSpecifier(new ResPath("/Audio/Imperial/Medieval/Effects/skull-announce.ogg")), Filter.Broadcast(), true);
            _result = RoundResult.SkeletonsWon;

            var cursespawners = EntityManager.AllEntities<MagicBarrierCurseSpawnComponent>();

            Spawn("MedievalSpawnNecroSenderPreset", Transform(_random.Pick(cursespawners).Owner).Coordinates);
            for (var i = 0; i < 40; i++)
            {
                Spawn("MedievalSpawnNecroFighterPreset", Transform(_random.Pick(cursespawners).Owner).Coordinates);
            }

            _endTime = _timing.CurTime + TimeSpan.FromMinutes(25);

            return;
        }

        if (!_bossUid.IsValid())
            return;

        var xform = Transform(args.Stand);
        var players = EntityManager.AllEntities<HumanoidAppearanceComponent>().Where(x => !HasComp<IgnoreBossStartComponent>(x.Owner) && Transform(x).MapUid == Transform(args.Stand).MapUid);

        var bossfightPlayers = new List<EntityUid>();
        foreach (var item in players)
        {
            if (!_mobState.IsAlive(item.Owner))
                continue;

            bossfightPlayers.Add(item.Owner);
            _flash.Flash(item.Owner, null, null, TimeSpan.FromSeconds(5), 0.3f, false);
            _audio.PlayGlobal(new SoundPathSpecifier(new ResPath("/Audio/Imperial/Medieval/Effects/teleport.ogg")), item.Owner);
        }

        if (bossfightPlayers.Count == 0)
            return;

        var msgBoss = "Древний череп собран. Его проклятие не сломлено, но искажено святым пламенем. Равновесие пало, и теперь судьба этого острова решится в битве!";
        _chatMan.ChatMessageToAll(ChatChannel.Radio, msgBoss, msgBoss, EntityUid.Invalid, false, true, colorOverride: Color.OrangeRed);
        _audio.PlayGlobal(new SoundPathSpecifier(new ResPath("/Audio/Imperial/Medieval/Effects/skull-announce.ogg")), Filter.Broadcast(), true);

        _boss.StartBossfight(bossfightPlayers, _bossUid);
        _result = RoundResult.BossWon;
    }

    private void OnBossDefeated(ref BossDefeatedEvent args)
    {
        _result = RoundResult.BossDefeated;
        _endTime = _timing.CurTime + TimeSpan.FromMinutes(10);
    }

    private void OnBossWin(ref BossWonEvent args)
    {
        var cursespawners = EntityManager.AllEntities<MagicBarrierCurseSpawnComponent>();

        Spawn("MedievalSpawnNecroSenderPreset", Transform(_random.Pick(cursespawners).Owner).Coordinates);

        for (var i = 0; i < 40; i++)
        {
            Spawn("MedievalSpawnNecroFighterPreset", Transform(_random.Pick(cursespawners).Owner).Coordinates);
        }

        _endTime = _timing.CurTime + TimeSpan.FromMinutes(25);
    }

    private enum RoundResult
    {
        NoBoss,
        BossDefeated,
        BossWon,
        SkeletonsWon
    }
}
