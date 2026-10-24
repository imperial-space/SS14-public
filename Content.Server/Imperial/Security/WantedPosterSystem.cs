using System.Linq;
using System.Text;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.StationRecords;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// wanted_poster.dm и records/security/print_record(): консоль уголовных записей печатает
/// плакаты «Разыскивается» и «Пропал без вести» и досье; плакат вешается на стену.
/// </summary>
public sealed class WantedPosterSystem : EntitySystem
{
    private const string RolledProto = "WantedPosterRolled";
    private const string WallProto = "WantedPosterWall";
    private const string RippedProto = "PosterBroken";
    private const string BrigAccess = "Security";

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
    private static readonly SoundSpecifier RipSound = new SoundPathSpecifier("/Audio/Effects/poster_broken.ogg");

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CriminalRecordsConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnConsoleVerbs);
        SubscribeLocalEvent<WantedPrinterComponent, WantedPrintMessage>(OnPrint);

        SubscribeLocalEvent<WantedPosterComponent, MapInitEvent>(OnPosterMapInit);
        SubscribeLocalEvent<WantedPosterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<WantedPosterComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<WantedPosterComponent, WantedPosterPlaceDoAfterEvent>(OnPlaced);
        SubscribeLocalEvent<WantedPosterComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<WantedPosterComponent, InteractHandEvent>(OnInteractHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WantedPrinterComponent>();
        while (query.MoveNext(out var uid, out var printer))
        {
            if (printer.PrintAt is not { } at || now < at)
                continue;

            printer.PrintAt = null;
            if (printer.Pending is { } msg)
                FinishPrint(uid, msg);

            printer.Pending = null;
            UpdateUi(uid);
        }
    }

    #region Печать

    /// <summary>Кнопка «Печать» в консоли записей: отдельное окно поверх ванильной консоли.</summary>
    private void OnConsoleVerbs(Entity<CriminalRecordsConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("wanted-printer-verb"),
            Act = () => OpenPrinter(ent, user),
        });
    }

    private void OpenPrinter(EntityUid console, EntityUid user)
    {
        EnsureComp<WantedPrinterComponent>(console);
        _ui.SetUi(console, WantedPrinterUiKey.Key, new InterfaceData("WantedPrinterBoundUserInterface"));
        _ui.OpenUi(console, WantedPrinterUiKey.Key, user);
        UpdateUi(console);
    }

    private void UpdateUi(EntityUid console)
    {
        var state = new WantedPrinterState
        {
            Printing = CompOrNull<WantedPrinterComponent>(console)?.PrintAt != null,
        };

        if (_station.GetOwningStation(console) is { } station)
        {
            foreach (var (id, record) in _records.GetRecordsOfType<GeneralStationRecord>(station).OrderBy(r => r.Item2.Name))
            {
                state.Names.Add(record.Name);
                var key = new StationRecordKey(id, station);
                state.HasCrimes.Add(_records.TryGetRecord<CriminalRecord>(key, out var criminal) && criminal.History.Count > 0);
            }
        }

        _ui.SetUiState(console, WantedPrinterUiKey.Key, state);
    }

    /// <summary>print_record().</summary>
    private void OnPrint(Entity<WantedPrinterComponent> ent, ref WantedPrintMessage args)
    {
        var user = args.Actor;
        if (ent.Comp.PrintAt != null)
        {
            _popup.PopupEntity(Loc.GetString("wanted-printer-busy"), ent, user);
            _audio.PlayPvs(ErrorSound, ent);
            return;
        }

        if (args.Type != WantedPrintType.Missing && GetCrimes(ent, args.Name) is not { Count: > 0 })
        {
            _popup.PopupEntity(Loc.GetString("wanted-printer-no-crimes"), ent, user);
            _audio.PlayPvs(ErrorSound, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("wanted-printer-printing"), ent, user);
        _audio.PlayPvs(PrintSound, ent);
        ent.Comp.PrintAt = _timing.CurTime + TimeSpan.FromSeconds(2);
        ent.Comp.Pending = args;
        UpdateUi(ent);
    }

    private List<string>? GetCrimes(EntityUid console, string name)
    {
        if (_station.GetOwningStation(console) is not { } station || _records.GetRecordByName(station, name) is not { } id)
            return null;

        if (!_records.TryGetRecord<CriminalRecord>(new StationRecordKey(id, station), out var record))
            return null;

        return record.History.Select(h => h.Crime).ToList();
    }

    private void FinishPrint(EntityUid console, WantedPrintMessage msg)
    {
        var alias = Clip(msg.Alias, 32);
        if (string.IsNullOrWhiteSpace(alias))
            alias = msg.Name;

        var description = Clip(msg.Description, 512);
        if (string.IsNullOrWhiteSpace(description))
            description = Loc.GetString("wanted-printer-no-details");

        var header = Clip(msg.Header, 8);
        var coords = Transform(console).Coordinates;

        if (msg.Type == WantedPrintType.Rapsheet)
        {
            var paper = Spawn("Paper", coords);
            _meta.SetEntityName(paper, Loc.GetString("wanted-rapsheet-name", ("name", alias)));
            var text = new StringBuilder();
            text.AppendLine(Loc.GetString("wanted-rapsheet-header", ("header", string.IsNullOrWhiteSpace(header) ? Loc.GetString("wanted-rapsheet-default-header") : header)));
            text.AppendLine(Loc.GetString("wanted-rapsheet-person", ("name", alias)));
            text.AppendLine();
            text.AppendLine(description);
            text.AppendLine();
            foreach (var crime in GetCrimes(console, msg.Name) ?? new List<string>())
            {
                text.AppendLine(Loc.GetString("wanted-crime-line", ("crime", crime)));
            }

            _paper.SetContent(paper, text.ToString());
            return;
        }

        var missing = msg.Type == WantedPrintType.Missing;
        if (!missing)
        {
            var text = new StringBuilder(description);
            text.Append("\n\n").Append(Loc.GetString("wanted-crimes-header"));
            foreach (var crime in GetCrimes(console, msg.Name) ?? new List<string>())
            {
                text.Append('\n').Append(Loc.GetString("wanted-crime-line", ("crime", crime)));
            }

            description = text.ToString();
        }

        if (string.IsNullOrWhiteSpace(header))
            header = Loc.GetString(missing ? "wanted-header-missing" : "wanted-header-wanted");

        var poster = Spawn(RolledProto, coords);
        var comp = EnsureComp<WantedPosterComponent>(poster);
        comp.WantedName = alias;
        comp.Description = description;
        comp.Header = header;
        comp.Missing = missing;
        comp.Rolled = true;
        ApplyPoster((poster, comp));
    }

    private static string Clip(string text, int length)
    {
        text = text.Trim();
        return text.Length > length ? text[..length] : text;
    }

    #endregion

    #region Плакат

    private void OnPosterMapInit(Entity<WantedPosterComponent> ent, ref MapInitEvent args)
    {
        ApplyPoster(ent);
    }

    /// <summary>name = "[postName] ([wanted_name])".</summary>
    private void ApplyPoster(Entity<WantedPosterComponent> ent)
    {
        var kind = Loc.GetString(ent.Comp.Missing ? "wanted-poster-name-missing" : "wanted-poster-name-wanted");
        _meta.SetEntityName(ent, string.IsNullOrEmpty(ent.Comp.WantedName) ? kind : $"{kind} ({ent.Comp.WantedName})");
        _appearance.SetData(ent, WantedPosterVisuals.Missing, ent.Comp.Missing);
    }

    private void OnExamined(Entity<WantedPosterComponent> ent, ref ExaminedEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.WantedName))
            return;

        args.PushMarkup(Loc.GetString("wanted-poster-examine-header", ("header", ent.Comp.Header)));
        args.PushText(ent.Comp.Description);
    }

    /// <summary>poster/attack_atom: повесить на стену.</summary>
    private void OnAfterInteract(Entity<WantedPosterComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !ent.Comp.Rolled || !args.CanReach || args.Target is not { } wall || !_tag.HasTag(wall, new Robust.Shared.Prototypes.ProtoId<Content.Shared.Tag.TagPrototype>("Wall")))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("wanted-poster-placing"), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2), new WantedPosterPlaceDoAfterEvent(),
            ent, wall, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnPlaced(Entity<WantedPosterComponent> ent, ref WantedPosterPlaceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } wall || TerminatingOrDeleted(wall))
            return;

        args.Handled = true;
        var poster = Spawn(WallProto, Transform(wall).Coordinates);
        CopyTo(ent.Comp, poster, false);
        QueueDel(ent);
    }

    /// <summary>wirecutter_act(): снять плакат целым.</summary>
    private void OnInteractUsing(Entity<WantedPosterComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Rolled || !_tool.HasQuality(args.Used, new Robust.Shared.Prototypes.ProtoId<Content.Shared.Tools.ToolQualityPrototype>("Cutting")))
            return;

        args.Handled = true;
        var rolled = Spawn(RolledProto, Transform(ent).Coordinates);
        CopyTo(ent.Comp, rolled, true);
        _hands.TryPickupAnyHand(args.User, rolled);
        _popup.PopupEntity(Loc.GetString("wanted-poster-removed"), args.User, args.User);
        QueueDel(ent);
    }

    /// <summary>attack_hand(): сорвать плакат.</summary>
    private void OnInteractHand(Entity<WantedPosterComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Rolled)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("wanted-poster-ripped", ("user", args.User)), ent, PopupType.Small);
        _audio.PlayPvs(RipSound, ent);
        Spawn(RippedProto, Transform(ent).Coordinates);
        QueueDel(ent);
    }

    private void CopyTo(WantedPosterComponent from, EntityUid target, bool rolled)
    {
        var comp = EnsureComp<WantedPosterComponent>(target);
        comp.WantedName = from.WantedName;
        comp.Description = from.Description;
        comp.Header = from.Header;
        comp.Missing = from.Missing;
        comp.Rolled = rolled;
        ApplyPoster((target, comp));
    }

    #endregion
}
