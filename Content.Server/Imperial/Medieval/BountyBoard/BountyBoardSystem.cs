using System.Linq;
using Content.Server.Imperial.Medieval.Achievements;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Server.SSDFree;
using Content.Server.SSDFree.Components;
using Content.Shared.SSDFree.Components;
using Content.Server.Stack;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Imperial.Medieval.Achievements;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.BountyBoard;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Robust.Server.Player;
using Robust.Shared.Collections;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.BountyBoard;

public sealed class BountyBoardSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SSDFreeSystem _sddFree = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly JobSystem _jobSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly StorageSystem _storageSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AchievementSystem _achievement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MercenaryTargetComponent, InteractUsingEvent>(OnTargetInteracted);

        SubscribeLocalEvent<MercenaryBountyBoardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MercenaryBountyTargetTraitComponent, MapInitEvent>(OnTargetInit);
    }

    private void OnTargetInit(Entity<MercenaryBountyTargetTraitComponent> ent, ref MapInitEvent args)
    {
        _inventorySystem.SpawnItemOnEntity(ent, ent.Comp.CurrencyProtoId);

        if (_playerManager.TryGetSessionByEntity(ent, out var session))
        {
            _chatManager.DispatchServerMessage(session, Loc.GetString("trait-medieval-bounty-desc"));
        }
    }

    private void OnMapInit(Entity<MercenaryBountyBoardComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTimeUse = _gameTiming.CurTime + TimeSpan.FromMinutes(1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MercenaryBountyBoardComponent, StorageComponent>();

        while (query.MoveNext(out var uid, out var boardComponent, out _))
        {
            if (boardComponent.NextTimeUse > _gameTiming.CurTime)
            {
                continue;
            }

            var targetMaybe = FindEligibleTarget();

            if (!targetMaybe.HasValue)
            {
                boardComponent.NextTimeUse = _gameTiming.CurTime + TimeSpan.FromSeconds(30);
                continue;
            }

            var contract = SpawnContract((uid, boardComponent), targetMaybe.Value);

            if (!_storageSystem.CanInsert(uid,contract, out _))
            {
                boardComponent.NextTimeUse = _gameTiming.CurTime + boardComponent.CooldownTime;

                Del(contract);
                continue;
            }

            boardComponent.NextTimeUse = _gameTiming.CurTime + boardComponent.CooldownTime;

            _storageSystem.Insert(uid, contract, out _);
            InitTarget(targetMaybe.Value.Target);
        }
    }

    private void InitTarget(EntityUid target)
    {
        EnsureComp<MercenaryTargetComponent>(target);
    }

    private Entity<MercenaryContractComponent> SpawnContract(Entity<MercenaryBountyBoardComponent> ent,  MercTargetData targetData)
    {
        var contractUid = Spawn(ent.Comp.ContractProtoId);
        var contractComp = Comp<MercenaryContractComponent>(contractUid);

        contractComp.TargetUid = targetData.Target;

        var payout = _random.Next(contractComp.PayoutRange.X, contractComp.PayoutRange.Y);
        contractComp.Payout = payout;

        var description = Loc.GetString("merc-contract-description",
            ("name", targetData.Name),
            ("job", targetData.JobName),
            ("payout", payout));

        _metaData.SetEntityDescription(contractUid, description);

        return (contractUid, contractComp);
    }

    private void OnTargetInteracted(Entity<MercenaryTargetComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<MercenaryContractComponent>(args.Used, out var contractComponent))
        {
            return;
        }

        if (contractComponent.TargetUid != args.Target)
        {
            _popupSystem.PopupEntity(Loc.GetString("contract-wrong-target"), args.Target, args.User, PopupType.SmallCaution);
            return;
        }

        if (!_mobStateSystem.IsDead(args.Target))
        {
            _popupSystem.PopupEntity(Loc.GetString("contract-target-still-alive"), args.Target, args.User, PopupType.SmallCaution);
            return;
        }

        if (TryComp<SSDFreeComponent>(args.Target, out var ssdFreeComponent))
        {
            var session = ssdFreeComponent.CommonSession;

            _sddFree.GoToSSD(args.Target, session?.UserId, false, ssdFreeComponent);
        }

        var xform = Transform(args.Target);


        _stackSystem.SpawnMultiple(contractComponent.CurrencyProtoId.Id, contractComponent.Payout, xform.Coordinates);

        if (HasComp<AchievementOwnerComponent>(args.User))
        {
            _achievement.TryUpdateProgressAndGrant(args.User, new MercenaryContractCompletedContext(),
                ach => ach.Conditions.Any(c => c is CompleteMercContractCondition));
        }

        Del(args.Used);
    }

    private MercTargetData? FindEligibleTarget()
    {
        var targets = new ValueList<EntityUid>();

        EntityUid target;

        var query = EntityQueryEnumerator<MercenaryBountyTargetTraitComponent, HumanoidAppearanceComponent, SSDFreeComponent>();

        while (query.MoveNext(out var uid, out _, out _, out _))
        {
            if (HasComp<MercenaryTargetComponent>(uid))
            {
                continue;
            }

            targets.Add(uid);
        }

        if (targets.Count == 0)
        {
            return null;
        }

        target = _random.Pick(targets);
        return CreateMercTargetData(target);
    }

    private MercTargetData CreateMercTargetData(EntityUid target)
    {
        var targetData = new MercTargetData();
        targetData.Target = target;

        var metaData = MetaData(target);

        targetData.Name = metaData.EntityName;

        if (_mindSystem.TryGetMind(target, out var mindUid, out _))
        {
            targetData.JobName = _jobSystem.MindTryGetJobName(mindUid);
        }

        return targetData;
    }

    private record struct MercTargetData
    {
        public EntityUid Target { get; set; }
        public string Name { get; set; }

        public string JobName { get; set; }
    }
}
