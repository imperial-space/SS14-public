using System.Linq;
using Content.Server.Imperial.Quirks;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Mood;
using Content.Shared.Imperial.Quirks;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Mood;

/// <summary>Причуды TG про настроение и antag_moodlet ролей.</summary>
public sealed partial class ImperialMoodSystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    /// <summary>0.416% в секунду: 15 раз за час, по 2 минуты — половина времени.</summary>
    private const float MoodQuirkChance = 0.00416f;

    /// <summary>SLEEP_BANK_MULTIPLIER.</summary>
    private const float SleepBankMultiplier = 10f;

    /// <summary>antag_moodlet: компонент роли разума → мудлет.</summary>
    private static readonly (string Role, string Mood)[] AntagMoodlets =
    {
        ("TraitorRole", "focused"),
        ("NukeopsRole", "focused"),
        ("NinjaRole", "focused"),
        ("BloodBrotherRole", "focused"),
        ("ContractorRole", "focused"),
        ("WizardRole", "focused"),
        ("ImperialWizardRole", "focused"),
        ("ImperialWizardApprenticeRole", "focused"),
        ("ChangelingRole", "ling"),
        ("ImperialChangelingRole", "ling"),
        ("CultistRole", "cult"),
        ("HereticRole", "heretics"),
        ("GhoulRole", "heretics"),
        ("LunaticRole", "heretics_lunatic"),
        ("RevolutionaryRole", "revolution"),
    };

    private void InitializeQuirks()
    {
        SubscribeLocalEvent<ImperialHeirloomComponent, ExaminedEvent>(OnHeirloomExamined);
    }

    private void PollQuirks(Entity<ImperialMoodComponent> ent)
    {
        var uid = ent.Owner;
        var mood = ent.AsNullable();

        // depression / jolly: случайные приступы.
        if (HasComp<ImperialQuirkDepressionComponent>(uid) && _random.Prob(MoodQuirkChance))
            AddMood(mood, "depression", "depression");
        if (HasComp<ImperialQuirkJollyComponent>(uid) && _random.Prob(MoodQuirkChance))
            AddMood(mood, "jolly", "jolly");

        if (TryComp<ImperialQuirkFamilyHeirloomComponent>(uid, out var heirloom))
            UpdateHeirloom(ent, heirloom);

        if (TryComp<ImperialQuirkAllNighterComponent>(uid, out var nighter))
            UpdateAllNighter(ent, nighter);

        // badback: рюкзак или любая сумка на спине.
        if (HasComp<ImperialQuirkBadBackComponent>(uid))
            Toggle(ent, "back_pain", "back_pain", _inventory.TryGetSlotEntity(uid, "back", out var back) && HasComp<StorageComponent>(back));

        if (TryComp<ImperialQuirkErraticComponent>(uid, out var erratic))
            UpdateErratic(ent, erratic);

        // bald: без шапки стыдно, в парике уверенно.
        if (TryComp<ImperialQuirksComponent>(uid, out var quirks) && quirks.Quirks.Contains("ImperialQuirkBald"))
        {
            if (!_inventory.TryGetSlotEntity(uid, "head", out var head))
                AddMood(mood, "bad_hair_day", "bald");
            else if (MetaData(head.Value).EntityPrototype?.ID.Contains("Wig") == true)
                AddMood(mood, "bad_hair_day", "confident_mane");
            else
                ClearMood(mood, "bad_hair_day");
        }

        // spacer: тоска по космосу на планете.
        if (TryComp<ImperialQuirkSpacerComponent>(uid, out var spacer))
        {
            ProtoId<ImperialMoodEventPrototype>? id = spacer.State switch
            {
                ImperialSpacerState.Planet => "spacer_on_planet",
                ImperialSpacerState.PlanetTooLong => "spacer_on_planet_too_long",
                ImperialSpacerState.Space or ImperialSpacerState.SpaceComfortable or ImperialSpacerState.NoGravity => "spacer_in_space",
                _ => null,
            };

            if (id == null)
                ClearMood(mood, "spacer");
            else if (GetMood(mood, "spacer")?.Event != id)
                AddMood(mood, "spacer", id.Value);
        }
    }

    #region Семейная реликвия

    private void UpdateHeirloom(Entity<ImperialMoodComponent> ent, ImperialQuirkFamilyHeirloomComponent comp)
    {
        if (!comp.Given)
        {
            comp.Given = true;
            GiveHeirloom(ent, comp);
        }

        var safe = comp.Item is { } item && Exists(item) && IsCarriedBy(item, ent);
        if (safe)
        {
            ClearMood(ent.AsNullable(), "family_heirloom_missing");
            AddMood(ent.AsNullable(), "family_heirloom", "family_heirloom");
        }
        else
        {
            ClearMood(ent.AsNullable(), "family_heirloom");
            AddMood(ent.AsNullable(), "family_heirloom_missing", "family_heirloom_missing");
        }
    }

    /// <summary>add_unique: реликвия в карман, рюкзак или руки.</summary>
    private void GiveHeirloom(Entity<ImperialMoodComponent> ent, ImperialQuirkFamilyHeirloomComponent comp)
    {
        if (comp.Heirlooms.Count == 0)
            return;

        var item = Spawn(_random.Pick(comp.Heirlooms), Transform(ent).Coordinates);
        comp.Item = item;

        var name = Name(ent);
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var heirloom = EnsureComp<ImperialHeirloomComponent>(item);
        heirloom.Owner = ent;
        heirloom.Family = parts.Length > 0 ? parts[^1] : name;

        var placed = false;
        foreach (var slot in new[] { "pocket1", "pocket2" })
        {
            if (_inventory.TryEquip(ent, item, slot, true, true))
            {
                placed = true;
                break;
            }
        }

        if (!placed && _inventory.TryGetSlotEntity(ent, "back", out var bag) &&
            _containers.TryGetContainer(bag.Value, "storagebase", out var container) && _containers.Insert(item, container))
            placed = true;

        if (!placed)
            _hands.TryPickupAnyHand(ent, item, checkActionBlocker: false);

        if (TryComp<ActorComponent>(ent, out var actor))
            Notify(actor.PlayerSession, Loc.GetString("imperial-mood-heirloom-given", ("item", item)));
    }

    private bool IsCarriedBy(EntityUid item, EntityUid owner)
    {
        var parent = Transform(item).ParentUid;
        while (parent.IsValid())
        {
            if (parent == owner)
                return true;
            parent = Transform(parent).ParentUid;
        }

        return false;
    }

    /// <summary>component/heirloom: хозяин узнаёт свою реликвию, остальные — чья она.</summary>
    private void OnHeirloomExamined(Entity<ImperialHeirloomComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(args.Examiner == ent.Comp.Owner
            ? Loc.GetString("imperial-mood-heirloom-examine-owner", ("family", ent.Comp.Family))
            : Loc.GetString("imperial-mood-heirloom-examine", ("family", ent.Comp.Family)));
    }

    #endregion

    /// <summary>all_nighter: сон копит запас, бодрствование тратит; без запаса и стимуляторов — усталость.</summary>
    private void UpdateAllNighter(Entity<ImperialMoodComponent> ent, ImperialQuirkAllNighterComponent comp)
    {
        var tired = false;
        if (HasComp<SleepingComponent>(ent))
            comp.SleepBank += SleepBankMultiplier;
        else if (comp.SleepBank > 0)
            comp.SleepBank -= 1;
        else
            tired = !Stimulated(ent, comp);

        var has = HasMood(ent.AsNullable(), "all_nighter");
        if (has && !tired)
            ClearMood(ent.AsNullable(), "all_nighter");
        else if (!has && tired)
        {
            AddMood(ent.AsNullable(), "all_nighter", "all_nighter");
            if (TryComp<ActorComponent>(ent, out var actor))
                Notify(actor.PlayerSession, Loc.GetString("imperial-mood-all-nighter-tired"));
        }
    }

    /// <summary>TRAIT_STIMULATED: в крови кофе, никотин или стимуляторы.</summary>
    private bool Stimulated(EntityUid uid, ImperialQuirkAllNighterComponent comp)
    {
        foreach (var reagent in comp.Stimulants)
        {
            if (_solutions.GetTotalPrototypeQuantity(uid, reagent).Float() > 0.01f)
                return true;
        }

        return false;
    }

    /// <summary>erratic: каждые 6–12 минут личность меняется, через раз возвращается прежняя.</summary>
    private void UpdateErratic(Entity<ImperialMoodComponent> ent, ImperialQuirkErraticComponent comp)
    {
        var now = _timing.CurTime;
        if (!comp.Initialized)
        {
            comp.Initialized = true;
            comp.BasePersonalities = ent.Comp.Personalities.ToList();
            comp.NextChange = now + TimeSpan.FromMinutes(_random.Next(5, 11));
            return;
        }

        if (now < comp.NextChange || _mobState.IsIncapacitated(ent))
            return;

        comp.NextChange = now + TimeSpan.FromMinutes(_random.Next(6, 13));
        comp.Index++;

        string key;
        if (comp.Index % 2 == 0)
        {
            comp.Index = 0;
            SetPersonalities(ent, comp.BasePersonalities);
            key = "imperial-mood-erratic-normal";
        }
        else
        {
            SetPersonalities(ent, _random.Prob(0.01f) ? new List<string>() : RandomPersonalities(MaxPersonalities - 2, MaxPersonalities + 1));
            key = "imperial-mood-erratic-different";
        }

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        Notify(actor.PlayerSession, Loc.GetString(key));
        Notify(actor.PlayerSession, Loc.GetString("imperial-mood-erratic-now", ("personalities", PersonalityNames(ent.Comp))));
    }

    /// <summary>antag_moodlet: цель антагониста держит настроение.</summary>
    private void PollAntag(Entity<ImperialMoodComponent> ent)
    {
        var wanted = new HashSet<string>();
        if (_minds.TryGetMind(ent, out _, out var mind))
        {
            foreach (var role in mind.MindRoleContainer.ContainedEntities)
            {
                foreach (var (name, mood) in AntagMoodlets)
                {
                    if (Factory.TryGetRegistration(name, out var reg) && HasComp(role, reg.Type))
                        wanted.Add(mood);
                }
            }
        }

        foreach (var (_, mood) in AntagMoodlets)
        {
            var category = $"antag_moodlet_{mood}";
            if (wanted.Contains(mood))
            {
                if (!ent.Comp.Events.ContainsKey(category))
                    AddMood(ent.AsNullable(), category, mood);
            }
            else if (ent.Comp.Events.ContainsKey(category))
            {
                ClearMood(ent.AsNullable(), category);
            }
        }
    }

    private void Notify(ICommonSession session, string message)
    {
        _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Emotes, message, message, EntityUid.Invalid, false, session.Channel);
    }
}
