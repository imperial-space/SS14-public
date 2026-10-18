using Content.Server.Chat.Systems;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Emag.Systems;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// clothing/mask/gas/sechailer: «Compli-o-Nator 3000» в масках СБ.
/// Ванильным маскам компонент добавляется при создании по ID прототипа.
/// </summary>
public sealed class SecHailerSystem : EntitySystem
{
    /// <summary>Какие маски — хейлеры и их стартовая агрессивность.</summary>
    private static readonly Dictionary<string, HailerAggression> VanillaHailers = new()
    {
        ["ClothingMaskGasSecurity"] = HailerAggression.BadCop,
        ["ClothingMaskGasSwat"] = HailerAggression.ShitCop,
    };

    /// <summary>GLOB.hailer_phrases: первая — для емага.</summary>
    private static readonly string[] Phrases =
    {
        "emag", "halt", "bobby", "compliance", "justice", "running", "dontmove", "floor", "robocop", "god",
        "freeze", "imperial", "bash", "harry", "asshole", "stfu", "shutup", "super", "dredd",
    };

    // Индексы в Phrases с единицы, как в SS13.
    private const int EmagPhrase = 1;
    private const int GoodCopPhrases = 6;
    private const int BadCopPhrases = 12;
    private const int BrokePhrases = 13;
    private const int AllPhrases = 19;

    private static readonly TimeSpan PhraseCooldown = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OveruseCooldown = TimeSpan.FromSeconds(18);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly Robust.Shared.Random.IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        EntityManager.EntityInitialized += OnEntityInitialized;

        SubscribeLocalEvent<SecHailerComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<SecHailerComponent, SecHailerHaltActionEvent>(OnHalt);
        SubscribeLocalEvent<SecHailerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SecHailerComponent, GotEmaggedEvent>(OnEmagged);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.EntityInitialized -= OnEntityInitialized;
    }

    private void OnEntityInitialized(Entity<MetaDataComponent> ent)
    {
        if (ent.Comp.EntityPrototype is not { } proto || !VanillaHailers.TryGetValue(proto.ID, out var aggression))
            return;

        if (HasComp<SecHailerComponent>(ent))
            return;

        AddComp(ent, new SecHailerComponent { Aggression = aggression });
    }

    private void OnGetActions(Entity<SecHailerComponent> ent, ref GetItemActionsEvent args)
    {
        if (_inventory.InSlotWithFlags(ent.Owner, SlotFlags.MASK))
            args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    /// <summary>halt().</summary>
    private void OnHalt(Entity<SecHailerComponent> ent, ref SecHailerHaltActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.Performer;
        var now = _timing.CurTime;
        if (now < ent.Comp.NextPhrase)
            return;

        if (ent.Comp.BrokenHailer)
        {
            _popup.PopupEntity(Loc.GetString("sec-hailer-broken", ("mask", ent.Owner)), user, user, PopupType.SmallCaution);
            return;
        }

        // Перегрев: 5 фраз за 18 секунд ломают модулятор.
        ent.Comp.RecentUses++;
        if (ent.Comp.OveruseResetAt == null || now >= ent.Comp.OveruseResetAt)
        {
            ent.Comp.RecentUses = 0;
            ent.Comp.OveruseResetAt = now + OveruseCooldown;
        }

        switch (ent.Comp.RecentUses)
        {
            case 3:
                _popup.PopupEntity(Loc.GetString("sec-hailer-heat-1", ("mask", ent.Owner)), user, user, PopupType.SmallCaution);
                break;
            case 4:
                _popup.PopupEntity(Loc.GetString("sec-hailer-heat-2", ("mask", ent.Owner)), user, user, PopupType.LargeCaution);
                break;
            case 5:
                ent.Comp.BrokenHailer = true;
                _popup.PopupEntity(Loc.GetString("sec-hailer-overload", ("mask", ent.Owner)), user, user, PopupType.LargeCaution);
                return;
        }

        PlayPhrase(ent, user, SelectPhrase(ent.Comp));
    }

    /// <summary>select_phrase().</summary>
    private int SelectPhrase(SecHailerComponent comp)
    {
        if (comp.Emagged)
            return EmagPhrase;

        var upper = comp.Aggression switch
        {
            HailerAggression.GoodCop => GoodCopPhrases,
            HailerAggression.BadCop => BadCopPhrases,
            _ => AllPhrases,
        };

        var lower = comp.Aggression == HailerAggression.Broken ? BrokePhrases : EmagPhrase + 1;
        return _random.Next(lower, upper + 1);
    }

    /// <summary>play_phrase().</summary>
    private void PlayPhrase(Entity<SecHailerComponent> ent, EntityUid user, int index)
    {
        ent.Comp.NextPhrase = _timing.CurTime + PhraseCooldown;
        var phrase = Phrases[index - 1];

        _chat.TrySendInGameICMessage(ent, Loc.GetString($"sec-hailer-phrase-{phrase}"), InGameICChatType.Speak,
            ChatTransmitRange.Normal, nameOverride: Loc.GetString("sec-hailer-name", ("user", user)),
            ignoreActionBlocker: true, color: Color.Red);
        _audio.PlayPvs($"/Audio/Imperial/Security/Hailer/{phrase}.ogg", ent,
            AudioParams.Default.WithMaxDistance(14f).WithVolume(2f));
    }

    /// <summary>screwdriver_act() крутит ограничитель, wirecutter_act() его ломает.</summary>
    private void OnInteractUsing(Entity<SecHailerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tool.HasQuality(args.Used, "Screwing"))
        {
            args.Handled = true;
            if (ent.Comp.Aggression == HailerAggression.Broken)
            {
                _popup.PopupEntity(Loc.GetString("sec-hailer-restrictor-broken"), args.User, args.User, PopupType.SmallCaution);
                return;
            }

            var position = ent.Comp.Aggression switch
            {
                HailerAggression.GoodCop => "middle",
                HailerAggression.BadCop => "last",
                _ => "first",
            };
            _popup.PopupEntity(Loc.GetString($"sec-hailer-restrictor-{position}"), args.User, args.User);
            ent.Comp.Aggression = (HailerAggression) ((int) ent.Comp.Aggression % 3 + 1);
            return;
        }

        if (_tool.HasQuality(args.Used, "Cutting"))
        {
            args.Handled = true;
            if (ent.Comp.Aggression == HailerAggression.Broken)
                return;

            ent.Comp.Aggression = HailerAggression.Broken;
            _popup.PopupEntity(Loc.GetString("sec-hailer-restrictor-cut"), args.User, args.User, PopupType.MediumCaution);
        }
    }

    private void OnEmagged(Entity<SecHailerComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || ent.Comp.Emagged)
            return;

        ent.Comp.Emagged = true;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("sec-hailer-emagged"), ent, args.UserUid);
    }
}
