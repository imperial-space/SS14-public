using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>Покупка записи с особым эффектом (призывы, перки, испытания): true — куплено.</summary>
[ByRefEvent]
public record struct ImperialSpellbookSpecialEvent(EntityUid User, EntityUid Book, ImperialSpellbookEntryPrototype Entry, bool Handled = false, bool Success = false);

/// <summary>Возврат записи-предмета (контракт ученика, бутылки демонов): предмет возвращается в книгу.</summary>
[RegisterComponent]
public sealed partial class ImperialSpellbookRefundableItemComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ImperialSpellbookEntryPrototype> Entry;

    /// <summary>Сообщение при возврате.</summary>
    [DataField]
    public LocId Message = "imperial-spellbook-item-refunded";

    /// <summary>Предмет уже использован — вернуть нельзя (contract.used).</summary>
    [ViewVariables]
    public bool Used;
}

/// <summary>
/// /obj/item/spellbook: привязка к владельцу, покупка и возврат записей, наборы и случайный выбор.
/// </summary>
public sealed class ImperialSpellbookSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly ImperialSpellSystem _spells = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ImperialWizardRuleSystem _wizard = default!;

    /// <summary>Одобренные наборы (wizard_loadout): записи и сколько раз их брать.</summary>
    private static readonly Dictionary<string, (string Entry, int Times)[]> Loadouts = new()
    {
        ["loadout_classic"] = new[]
        {
            ("ImperialSpellbookFireball", 1), ("ImperialSpellbookMagicMissile", 1), ("ImperialSpellbookDisintegrate", 1),
            ("ImperialSpellbookJaunt", 2),
        },
        ["loadout_hammer"] = new[]
        {
            ("ImperialSpellbookMjolnir", 1), ("ImperialSpellbookSummonItem", 1), ("ImperialSpellbookMutate", 1),
            ("ImperialSpellbookForcewall", 1), ("ImperialSpellbookBlink", 1), ("ImperialSpellbookTeslaBlast", 1),
        },
        ["loadout_army"] = new[]
        {
            ("ImperialSpellbookSoulstones", 1), ("ImperialSpellbookStaffChange", 1), ("ImperialSpellbookNecrostone", 1),
            ("ImperialSpellbookTeleport", 1), ("ImperialSpellbookJaunt", 1),
        },
        ["loadout_tap"] = new[]
        {
            ("ImperialSpellbookSoulTap", 1), ("ImperialSpellbookDisintegrate", 1), ("ImperialSpellbookFleshToStone", 1),
            ("ImperialSpellbookMindswap", 1), ("ImperialSpellbookKnock", 1), ("ImperialSpellbookTeleport", 1),
        },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialSpellbookComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ImperialSpellbookComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialSpellbookComponent, Content.Shared.Interaction.InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialSpellbookComponent, ImperialSpellbookPurchaseMessage>(OnPurchase);
        SubscribeLocalEvent<ImperialSpellbookComponent, ImperialSpellbookRefundMessage>(OnRefund);
        SubscribeLocalEvent<ImperialSpellbookComponent, ImperialSpellbookRandomizeMessage>(OnRandomize);
        SubscribeLocalEvent<ImperialSpellbookComponent, ImperialSpellbookLoadoutMessage>(OnLoadout);
    }

    public IEnumerable<ImperialSpellbookEntryPrototype> Entries()
    {
        return _proto.EnumeratePrototypes<ImperialSpellbookEntryPrototype>().Where(e => !e.Hidden);
    }

    #region Владелец

    /// <summary>Привязать книгу к разуму (owner = user.mind).</summary>
    public void SetOwner(Entity<ImperialSpellbookComponent> book, EntityUid mindId, MindComponent mind)
    {
        book.Comp.Owner = mindId;
        book.Comp.OwnerName = mind.CharacterName ?? string.Empty;
    }

    private void OnUseInHand(Entity<ImperialSpellbookComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        if (!_mind.TryGetMind(user, out var mindId, out var mind))
            return;

        if (ent.Comp.Owner == null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spellbook-bind", ("book", ent.Owner)), user, user);
            SetOwner(ent, mindId, mind);
            return;
        }

        if (ent.Comp.Owner != mindId)
        {
            _popup.PopupEntity(Loc.GetString(_wizard.IsApprentice(mindId) ? "imperial-spellbook-apprentice" : "imperial-spellbook-not-owner",
                ("book", ent.Owner)), user, user, PopupType.SmallCaution);
            return;
        }

        _ui.OpenUi(ent.Owner, ImperialSpellbookUiKey.Key, user);
        UpdateUi(ent, user);
    }

    private void OnExamined(Entity<ImperialSpellbookComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Owner != null
            ? Loc.GetString("imperial-spellbook-examine-owner", ("owner", ent.Comp.OwnerName))
            : Loc.GetString("imperial-spellbook-examine-no-owner"));
    }

    #endregion

    #region Покупка

    private void OnPurchase(Entity<ImperialSpellbookComponent> ent, ref ImperialSpellbookPurchaseMessage args)
    {
        if (!IsOwner(ent, args.Actor) || !_proto.TryIndex<ImperialSpellbookEntryPrototype>(args.Entry, out var entry))
            return;

        Purchase(ent, args.Actor, entry);
        UpdateUi(ent, args.Actor);
    }

    private bool IsOwner(Entity<ImperialSpellbookComponent> book, EntityUid user)
    {
        if (!HasComp<HumanoidProfileComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spellbook-lesser-life"), user, user);
            return false;
        }

        return _mind.TryGetMind(user, out var mindId, out _) && book.Comp.Owner == mindId;
    }

    public int Times(ImperialSpellbookComponent book, string entry)
    {
        return book.Times.GetValueOrDefault(entry);
    }

    /// <summary>limit записи: у заклинаний — spell_max_level.</summary>
    public int? GetLimit(ImperialSpellbookEntryPrototype entry)
    {
        if (entry.Limit != null)
            return entry.Limit;

        if (entry.Spell is { } spell && _proto.TryIndex(spell, out var proto)
            && proto.TryGetComponent<ImperialSpellComponent>(out var comp, EntityManager.ComponentFactory))
            return comp.MaxLevel;

        return null;
    }

    /// <summary>can_buy().</summary>
    public bool CanBuy(Entity<ImperialSpellbookComponent> book, EntityUid user, ImperialSpellbookEntryPrototype entry)
    {
        if (book.Comp.Uses < entry.Cost)
            return false;

        if (GetLimit(entry) is { } limit && Times(book.Comp, entry.ID) >= limit)
            return false;

        foreach (var other in entry.NoCoexistence)
        {
            if (Times(book.Comp, other) > 0 || HasEntry(user, other))
                return false;
        }

        if (entry.Category == ImperialSpellbookCategory.Perks && _wizard.HasPerk(user, entry.ID))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spellbook-perk-known"), user, user);
            return false;
        }

        return true;
    }

    private bool HasEntry(EntityUid user, string entryId)
    {
        if (!_proto.TryIndex<ImperialSpellbookEntryPrototype>(entryId, out var other))
            return false;

        return other.Spell is { } spell && _spells.FindSpell(user, spell) != null || _wizard.HasPerk(user, entryId);
    }

    /// <summary>purchase_entry(): списать очки и выдать запись.</summary>
    public bool Purchase(Entity<ImperialSpellbookComponent> book, EntityUid user, ImperialSpellbookEntryPrototype entry)
    {
        if (!CanBuy(book, user, entry) || !BuyEntry(book, user, entry))
            return false;

        book.Comp.Times[entry.ID] = Times(book.Comp, entry.ID) + 1;
        _wizard.LogPurchase(user, entry);

        // TRAIT_SPELLS_LOTTERY: шанс 50/цена получить запись бесплатно.
        if (_wizard.HasPerk(user, "ImperialSpellbookPerkSpellLottery") && _random.Prob(Math.Min(1f, 0.5f / Math.Max(1, entry.Cost))))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spellbook-lottery-free"), user, user);
            return true;
        }

        book.Comp.Uses -= entry.Cost;
        return true;
    }

    /// <summary>buy_spell(): выучить или улучшить заклинание, выдать предметы, особые записи — через событие.</summary>
    private bool BuyEntry(Entity<ImperialSpellbookComponent> book, EntityUid user, ImperialSpellbookEntryPrototype entry)
    {
        if (entry.Special != null)
        {
            var ev = new ImperialSpellbookSpecialEvent(user, book, entry);
            RaiseLocalEvent(user, ref ev, true);
            if (!ev.Handled)
            {
                Log.Warning($"Запись книги {entry.ID} ({entry.Special}) никто не обработал");
                return false;
            }

            if (!ev.Success)
                return false;
        }

        if (entry.Spell is { } spellProto)
        {
            if (_spells.FindSpell(user, spellProto) is { } existing)
            {
                var before = Name(existing);
                if (!_spells.LevelSpell(existing))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-spellbook-max-level"), user, user);
                    return false;
                }

                _popup.PopupEntity(Loc.GetString("imperial-spellbook-improved", ("before", before), ("after", Name(existing))), user, user);
            }
            else
            {
                if (GrantSpell(user, spellProto) is not { } spell)
                    return false;

                _popup.PopupEntity(Loc.GetString("imperial-spellbook-learned", ("spell", Name(spell))), user, user);
            }
        }

        foreach (var item in entry.Items)
        {
            var spawned = Spawn(item, Transform(user).Coordinates);
            _hands.TryPickupAnyHand(user, spawned);
        }

        return true;
    }

    /// <summary>new spell_type(user.mind).Grant(user): заклинание живёт на разуме и переходит вместе с ним.</summary>
    public EntityUid? GrantSpell(EntityUid user, EntProtoId spell)
    {
        if (_mind.TryGetMind(user, out var mindId, out _))
        {
            var action = _actionContainer.AddAction(mindId, spell);
            if (action == null)
                return null;

            _actions.GrantContainedAction(user, mindId, action.Value);
            return action;
        }

        return _actions.AddAction(user, spell);
    }

    #endregion

    #region Возврат

    private void OnRefund(Entity<ImperialSpellbookComponent> ent, ref ImperialSpellbookRefundMessage args)
    {
        if (!IsOwner(ent, args.Actor) || !_proto.TryIndex<ImperialSpellbookEntryPrototype>(args.Entry, out var entry))
            return;

        var user = args.Actor;
        if (!CanRefund(ent, user, entry))
            return;

        var result = RefundSpell(user, entry);
        if (result <= 0)
            return;

        ent.Comp.Times[entry.ID] = 0;
        ent.Comp.Uses += result;
        UpdateUi(ent, user);
    }

    /// <summary>can_refund(): только заклинания, которые у мага есть, и только пока возвраты разрешены.</summary>
    private bool CanRefund(Entity<ImperialSpellbookComponent> book, EntityUid user, ImperialSpellbookEntryPrototype entry)
    {
        if (_wizard.HasPerk(user, "ImperialSpellbookPerkSpellLottery"))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spellbook-no-refund"), user, user);
            return false;
        }

        if (!entry.Refundable || !book.Comp.RefundsAllowed || entry.Spell is not { } spell)
            return false;

        return _spells.FindSpell(user, spell) != null;
    }

    /// <summary>refund_spell(): только в логове; вернуть уровень × цену.</summary>
    private int RefundSpell(EntityUid user, ImperialSpellbookEntryPrototype entry)
    {
        if (!_wizard.InDen(user))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spellbook-refund-den"), user, user);
            return -1;
        }

        if (entry.Spell is not { } spellProto || _spells.FindSpell(user, spellProto) is not { } spell)
            return -1;

        var amount = spell.Comp.Level * entry.Cost;
        if (amount <= 0)
            return -1;

        _actions.RemoveAction(spell.Owner);
        QueueDel(spell);
        return amount;
    }

    /// <summary>attackby(): контракт ученика и бутылки демонов возвращаются в книгу за очки.</summary>
    private void OnInteractUsing(Entity<ImperialSpellbookComponent> ent, ref Content.Shared.Interaction.InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<ImperialSpellbookRefundableItemComponent>(args.Used, out var refundable))
            return;

        args.Handled = true;
        var user = args.User;
        if (!_proto.TryIndex(refundable.Entry, out var entry) || !entry.Refundable || !ent.Comp.RefundsAllowed)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spellbook-item-no-refund", ("item", args.Used)), user, user);
            return;
        }

        if (refundable.Used)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spellbook-item-used"), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString(refundable.Message), user, user);
        ent.Comp.Uses += entry.Cost;
        ent.Comp.Times[entry.ID] = Math.Max(0, Times(ent.Comp, entry.ID) - 1);
        QueueDel(args.Used);
    }

    #endregion

    #region Наборы и случайный выбор

    private void OnLoadout(Entity<ImperialSpellbookComponent> ent, ref ImperialSpellbookLoadoutMessage args)
    {
        if (!IsOwner(ent, args.Actor) || !FullPoints(ent, args.Actor) || !Loadouts.TryGetValue(args.Loadout, out var wanted))
            return;

        foreach (var (entryId, times) in wanted)
        {
            if (!_proto.TryIndex<ImperialSpellbookEntryPrototype>(entryId, out var entry))
            {
                Log.Error($"Набор {args.Loadout}: нет записи {entryId}");
                continue;
            }

            for (var i = 0; i < times; i++)
            {
                if (!Purchase(ent, args.Actor, entry))
                {
                    Log.Error($"Набор {args.Loadout}: не удалось купить {entryId}");
                    break;
                }
            }
        }

        ent.Comp.RefundsAllowed = false;
        UpdateUi(ent, args.Actor);
    }

    private void OnRandomize(Entity<ImperialSpellbookComponent> ent, ref ImperialSpellbookRandomizeMessage args)
    {
        if (!IsOwner(ent, args.Actor) || !FullPoints(ent, args.Actor))
            return;

        if (args.Full)
            Randomize(ent, args.Actor, ent.Comp.FullRandomBonus);
        else
            SemiRandomize(ent, args.Actor, ent.Comp.SemiRandomBonus);
        UpdateUi(ent, args.Actor);
    }

    private bool FullPoints(Entity<ImperialSpellbookComponent> book, EntityUid user)
    {
        if (book.Comp.Uses >= book.Comp.MaxUses)
            return true;

        _popup.PopupEntity(Loc.GetString("imperial-spellbook-need-all-points"), user, user);
        return false;
    }

    /// <summary>semirandomize(): одно заклинание наступления, одно мобильности, остальное случайно.</summary>
    public void SemiRandomize(Entity<ImperialSpellbookComponent> book, EntityUid user, int bonus)
    {
        var needed = new List<ImperialSpellbookCategory> { ImperialSpellbookCategory.Offensive, ImperialSpellbookCategory.Mobility };
        var shuffled = Entries().ToList();
        _random.Shuffle(shuffled);
        for (var i = 0; i < 2; i++)
        {
            foreach (var entry in shuffled)
            {
                if (!needed.Contains(entry.Category) || !Purchase(book, user, entry))
                    continue;

                needed.Remove(entry.Category);
                break;
            }
        }

        book.Comp.RefundsAllowed = false;
        Randomize(book, user, bonus);
    }

    /// <summary>randomize(): покупать случайные записи, пока есть очки.</summary>
    public void Randomize(Entity<ImperialSpellbookComponent> book, EntityUid user, int bonus)
    {
        var entries = Entries().ToList();
        book.Comp.Uses += bonus;
        while (book.Comp.Uses > 0 && entries.Count > 0)
        {
            var entry = _random.Pick(entries);
            if (!Purchase(book, user, entry))
            {
                // В TG запись остаётся в списке и выбирается снова; чтобы не зациклиться, убираем недоступные.
                if (!CanBuy(book, user, entry))
                    entries.Remove(entry);
                continue;
            }

            entries.Remove(entry);
        }

        book.Comp.RefundsAllowed = false;
    }

    #endregion

    public void UpdateUi(Entity<ImperialSpellbookComponent> book, EntityUid user)
    {
        var states = new List<ImperialSpellbookEntryState>();
        foreach (var entry in Entries())
        {
            var name = Loc.GetString(entry.Name);
            float? cooldown = entry.Cooldown;
            var garb = entry.RequiresGarb ?? false;
            if (entry.Spell is { } spellProto && _proto.TryIndex(spellProto, out var proto)
                && proto.TryGetComponent<ImperialSpellComponent>(out var spellComp, EntityManager.ComponentFactory))
            {
                garb = entry.RequiresGarb ?? (spellComp.Requirements & ImperialSpellRequirements.WizardGarb) != 0;
                cooldown ??= (float) spellComp.Cooldown.TotalSeconds;
                if (_spells.FindSpell(user, spellProto) is { } owned)
                {
                    name = Name(owned);
                    cooldown = (float) _spells.GetCooldown(owned.Comp).TotalSeconds;
                }
            }

            states.Add(new ImperialSpellbookEntryState(entry.ID, name, Times(book.Comp, entry.ID), cooldown, garb));
        }

        _ui.SetUiState(book.Owner, ImperialSpellbookUiKey.Key, new ImperialSpellbookState(book.Comp.OwnerName, book.Comp.Uses,
            book.Comp.MaxUses, book.Comp.MaxUses + book.Comp.SemiRandomBonus, book.Comp.MaxUses + book.Comp.FullRandomBonus, states));
    }
}
