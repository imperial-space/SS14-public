using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Shared.Cargo.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Советы рыбаку и послания в бутылках (SSpersistence message_bottles): записку или деньги кладут в бутылку
/// и бросают в море, а в следующих раундах их можно выловить.
/// </summary>
public sealed class ImperialFishingMiscSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ImperialFishingSpotSystem _spots = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    /// <summary>Количество советов в локали (strings/fishing_tips.txt).</summary>
    private const int TipCount = 63;

    private static readonly ResPath BottlesPath = new("/imperial_message_bottles.json");

    /// <summary>Источники, где можно бросить послание (TRAIT_MESSAGE_IN_A_BOTTLE_LOCATION): море, пляж, реки.</summary>
    private static readonly HashSet<string> BottleSources = new()
    {
        "ImperialFishSourceOcean", "ImperialFishSourceOceanBeach", "ImperialFishSourceRiver", "ImperialFishSourcePortalBeach",
        "ImperialFishSourcePortalOcean",
    };

    private sealed class SavedBottle
    {
        public string? Paper { get; set; }
        public string? CashProto { get; set; }
        public int CashAmount { get; set; }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialFishingTipComponent, MapInitEvent>(OnTipMapInit);
        SubscribeLocalEvent<ImperialMessageInABottleComponent, MapInitEvent>(OnBottleMapInit);
        SubscribeLocalEvent<ImperialMessageInABottleComponent, ExaminedEvent>(OnBottleExamined);
        SubscribeLocalEvent<ImperialMessageInABottleComponent, AfterInteractEvent>(OnBottleAfterInteract);
        SubscribeLocalEvent<ImperialMessageInABottleComponent, UseInHandEvent>(OnBottleUse);
    }

    private void OnTipMapInit(Entity<ImperialFishingTipComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<PaperComponent>(ent, out var paper) && string.IsNullOrEmpty(paper.Content))
            _paper.SetContent((ent, paper), Loc.GetString($"imperial-fishing-tip-{_random.Next(1, TipCount + 1)}"));
    }

    private void OnBottleMapInit(Entity<ImperialMessageInABottleComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.LoadOnSpawn)
            return;

        // load_message_bottle(): случайное послание прошлых раундов.
        var bottles = LoadBottles();
        if (bottles.Count == 0)
            return;

        var index = _random.Next(bottles.Count);
        var saved = bottles[index];
        bottles.RemoveAt(index);
        SaveBottles(bottles);

        EntityUid? message = null;
        if (saved.Paper != null)
        {
            message = Spawn("Paper", Transform(ent).Coordinates);
            _paper.SetContent(message.Value, saved.Paper);
        }
        else if (saved.CashProto != null)
        {
            message = Spawn(saved.CashProto, Transform(ent).Coordinates);
            if (TryComp<StackComponent>(message, out var stack))
                _stack.SetCount(message.Value, saved.CashAmount, stack);
        }

        if (message != null && !_slots.TryInsert(ent, ImperialMessageInABottleComponent.SlotId, message.Value, null))
            QueueDel(message.Value);
    }

    private void OnBottleExamined(Entity<ImperialMessageInABottleComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(_slots.GetItemOrNull(ent, ImperialMessageInABottleComponent.SlotId) is { } message
            ? Loc.GetString("imperial-fishing-bottle-has-message", ("message", message))
            : Loc.GetString("imperial-fishing-bottle-empty"));
    }

    /// <summary>Бросить послание в море (interact_with_atom_secondary на месте послания).</summary>
    private void OnBottleAfterInteract(Entity<ImperialMessageInABottleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (_spots.GetSource(target) is not { } source || !BottleSources.Contains(source.ID))
            return;

        if (_slots.GetItemOrNull(ent, ImperialMessageInABottleComponent.SlotId) is not { } message)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-bottle-no-message"), args.User, args.User);
            return;
        }

        args.Handled = true;
        var saved = new SavedBottle();
        if (TryComp<PaperComponent>(message, out var paper) && !string.IsNullOrWhiteSpace(paper.Content))
            saved.Paper = paper.Content;
        else if (HasComp<CashComponent>(message) && MetaData(message).EntityPrototype is { } cashProto)
        {
            saved.CashProto = cashProto.ID;
            saved.CashAmount = CompOrNull<StackComponent>(message)?.Count ?? 1;
        }

        if (saved.Paper != null || saved.CashProto != null)
        {
            var bottles = LoadBottles();
            bottles.Add(saved);
            SaveBottles(bottles);
        }

        _popup.PopupEntity(Loc.GetString("imperial-fishing-bottle-tossed", ("target", target)), args.User, args.User);
        _audio.PlayPvs("/Audio/Imperial/Fishing/bigsplash.ogg", target);
        QueueDel(ent);
    }

    /// <summary>Разбить бутылку, чтобы достать послание.</summary>
    private void OnBottleUse(Entity<ImperialMessageInABottleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || _slots.GetItemOrNull(ent, ImperialMessageInABottleComponent.SlotId) == null)
            return;

        args.Handled = true;
        var message = _slots.GetItemOrNull(ent, ImperialMessageInABottleComponent.SlotId)!.Value;
        var coords = Transform(args.User).Coordinates;
        _slots.TryEject(ent, ImperialMessageInABottleComponent.SlotId, args.User, out _, excludeUserAudio: true);
        _hands.PickupOrDrop(args.User, message);
        Spawn("ShardGlass", coords);
        _audio.PlayPvs("/Audio/Effects/glass_break1.ogg", coords);
        _popup.PopupEntity(Loc.GetString("imperial-fishing-bottle-broken"), args.User, args.User);
        QueueDel(ent);
    }

    private List<SavedBottle> LoadBottles()
    {
        try
        {
            if (!_resources.UserData.Exists(BottlesPath))
                return new List<SavedBottle>();

            using var reader = _resources.UserData.OpenText(BottlesPath);
            return JsonSerializer.Deserialize<List<SavedBottle>>(reader.ReadToEnd()) ?? new List<SavedBottle>();
        }
        catch (Exception e)
        {
            Log.Error($"Не удалось прочитать послания в бутылках: {e.Message}");
            return new List<SavedBottle>();
        }
    }

    private void SaveBottles(List<SavedBottle> bottles)
    {
        try
        {
            // Хранить не больше сотни посланий, самые старые уходят.
            var trimmed = bottles.Skip(Math.Max(0, bottles.Count - 100)).ToList();
            using var writer = _resources.UserData.OpenWriteText(BottlesPath);
            writer.Write(JsonSerializer.Serialize(trimmed));
        }
        catch (Exception e)
        {
            Log.Error($"Не удалось сохранить послания в бутылках: {e.Message}");
        }
    }
}
