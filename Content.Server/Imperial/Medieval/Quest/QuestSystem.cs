using System.Linq;
using Content.Server.Quest.Components;
using Content.Shared.Speech;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Server.Storage.Components;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Medieval.Trading;
using Content.Shared.Storage.Components;

namespace Content.Server.Quest;
public partial class QuestSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly TradingSystem _trading = null!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<QuestContractComponent, ComponentStartup>(OnStart);
        SubscribeLocalEvent<PalletContractComponent, ComponentStartup>(OnStartPallete);
        SubscribeLocalEvent<PalletStorageComponent, ComponentStartup>(OnStartPallete2);
        SubscribeLocalEvent<QuestContractComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<PalletContractComponent, ExaminedEvent>(OnExaminePallete);
        SubscribeLocalEvent<QuestContractComponent, BeforeRangedInteractEvent>(OnUseInHand);
        SubscribeLocalEvent<PalletContractComponent, BeforeRangedInteractEvent>(OnUseInHandPallete);
    }

    public void OnUseInHand(EntityUid uid, QuestContractComponent comp, BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (EntityManager.Deleted(args.User) || EntityManager.Deleted(args.Used) || args.Target != null && EntityManager.Deleted(args.Target.Value))
            return;

        args.Handled = true;
        OnUse(args.Target, args.User, args.Used, comp);
    }

    public void OnUse(EntityUid? target, EntityUid user, EntityUid used, QuestContractComponent comp)
    {
        if (target == null)
            return;

        if (TryComp<EntityStorageComponent>(target, out var storage) && storage != null)
        {
            int lootCount = 0;
            foreach (var loot in storage.Contents.ContainedEntities)
            {
                if (TryComp<QuestItemComponent>(loot, out var questItem) && questItem.ContractName == comp.ContractName)
                    lootCount++;
            }
            if (lootCount >= comp.Amount)
            {
                GetReward(used, user, storage.Owner, comp.Reward, comp.ContractPartner, comp.ReputationReward, comp.ContractGuildId);
            }
        }
    }

    public void OnUseInHandPallete(EntityUid uid, PalletContractComponent comp, BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;

        args.Handled = true;
        OnUsePallete(args.Target, args.User, args.Used, comp);
    }

    public void OnUsePallete(EntityUid? target, EntityUid user, EntityUid used, PalletContractComponent comp)
    {
        if (target == null)
            return;

        if (TryComp<PalletStorageComponent>(target.Value, out var pallet) && pallet.ContractPartner == comp.ContractPartner)
            GetReward(used, user, target.Value, comp.Reward, comp.ContractPartner, comp.ReputationReward, comp.ContractGuildId);
    }

    public void GetReward(EntityUid contract,
        EntityUid user,
        EntityUid chest,
        int reward,
        string contractPartner,
        float reputationReward,
        Guid? contractGuildId)
    {
        int remainingAmount = reward;
        var xform = Transform(chest);
        var coords = xform.Coordinates;
        if (!CheckQuestArea(coords, contractPartner))
            return;
        while (remainingAmount >= 100)
        {
            Spawn("MedievalRevent100", coords);
            remainingAmount -= 100;
        }
        var lastStack = Spawn("MedievalRevent", coords);
        if (TryComp<StackComponent>(lastStack, out var stack) && stack != null)
            _stack.SetCount(lastStack, remainingAmount, stack);
        _audio.PlayEntity("/Audio/Imperial/Medieval/quest_reward.ogg", Filter.Entities(user), user, false, AudioParams.Default.WithVolume(20f));

        if (contractGuildId != null)
        {
            var guild = _trading.Guilds
                .FirstOrDefault(t => t.Id == contractGuildId);
            if (guild == null)
                return;

            var netUser = GetNetEntity(user);
            guild.AddReputation(netUser, reputationReward);
        }

        QueueDel(contract);
        QueueDel(chest);
    }

    public bool CheckQuestArea(EntityCoordinates coords, string partner)
    {
        foreach (var entity in _lookup.GetEntitiesInRange(coords, 4f, flags: LookupFlags.Uncontained))
        {
            if (TryComp<QuestAreaComponent>(entity, out var area) && area.ContractPartner == partner)
                return true;
        }
        return false;
    }
    private void OnStart(EntityUid uid, QuestContractComponent comp, ComponentStartup args)
    {
        comp.Reward = _random.Next(comp.MinReward, comp.MaxReward);
        comp.Amount = _random.Next(comp.MinAmount, comp.MaxAmount);
        comp.ContractName = _random.Pick(comp.ContractTypes);
    }

    private void OnStartPallete(EntityUid uid, PalletContractComponent comp, ComponentStartup args)
    {
        comp.Reward = _random.Next(comp.MinReward, comp.MaxReward);
        foreach (var spy in EntityManager.EntityQuery<PalletSpyComponent>())
        {
            EnsureComp<SpeechComponent>(spy.Owner);
            _chat.TrySendInGameICMessage(spy.Owner, "Замечено прибытие ценного груза... его собираются доставить в " + comp.ContractPartner +"!!!", InGameICChatType.Speak, false);
        }
    }

    private void OnStartPallete2(EntityUid uid, PalletStorageComponent comp, ComponentStartup args)
    {
        var xform = Transform(uid);
        var coords = xform.Coordinates;
        Spawn(comp.QuestLink, coords);
    }

    private void OnExamine(EntityUid uid, QuestContractComponent comp, ExaminedEvent args)
    {
        args.PushMarkup("[color=sandybrown]Тип контракта: [/color]добыча");
        args.PushMarkup("[color=lightgreen]Место сдачи: [/color]" + comp.ContractPartner);
        args.PushMarkup("[color=orange]Тип добычи: [/color]" + comp.ContractName);
        args.PushMarkup("[color=red]Необходимое количество: [/color]" + comp.Amount);
        args.PushMarkup("[color=yellow]Награда: [/color]" + comp.Reward);
    }

    private void OnExaminePallete(EntityUid uid, PalletContractComponent comp, ExaminedEvent args)
    {
        args.PushMarkup("[color=sandybrown]Тип контракта: [/color]доставка");
        args.PushMarkup("[color=lightgreen]Место сдачи: [/color]" + comp.ContractPartner);
        args.PushMarkup("[color=yellow]Награда: [/color]" + comp.Reward);
    }
}
