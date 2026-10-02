using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Genetics;

/// <summary>Ручной сканер генетических последовательностей (sequence_scanner из SS13).</summary>
[RegisterComponent]
public sealed partial class SequenceScannerComponent : Component
{
    /// <summary>Мутации последнего просканированного существа и активны ли они.</summary>
    [ViewVariables]
    public List<(ProtoId<GeneticMutationPrototype> Mutation, bool Active)> Buffer = new();

    [ViewVariables]
    public GeneticMakeupData? Makeup;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan MakeupScanTime = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public TimeSpan ReadyAt;

    [DataField]
    public SoundSpecifier ScanSound = new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg");
}

/// <summary>
/// ЛКМ по существу — сохранить его последовательности в буфер, ПКМ-действие «Сканировать облик» — снять
/// генетический облик, по консоли ДНК — выгрузить облик в свободный буфер консоли.
/// Из меню действий сканера можно посмотреть полную последовательность гена (перезарядка 20 секунд).
/// </summary>
public sealed class SequenceScannerSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DnaConsoleSystem _console = default!;
    [Dependency] private readonly GeneticMakeupSystem _makeup = default!;
    [Dependency] private readonly GeneticsSystem _genetics = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>DNA_MUTATION_BLOCKS: последовательность выводится группами по 8 оснований.</summary>
    private const int DisplayBlock = 8;

    private static readonly VerbCategory AnalyzeCategory = new("sequence-scanner-verb-category", null);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SequenceScannerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SequenceScannerComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<SequenceScannerComponent, GetVerbsEvent<UtilityVerb>>(OnGetUtilityVerbs);
        SubscribeLocalEvent<SequenceScannerComponent, SequenceScannerMakeupDoAfterEvent>(OnMakeupDoAfter);
        SubscribeLocalEvent<SequenceScannerComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SequenceScannerComponent>();
        while (query.MoveNext(out var uid, out var scanner))
        {
            if (scanner.ReadyAt == TimeSpan.Zero || _timing.CurTime < scanner.ReadyAt)
                continue;

            scanner.ReadyAt = TimeSpan.Zero;
            _appearance.SetData(uid, SequenceScannerVisuals.Recharging, false);
        }
    }

    private void OnExamined(Entity<SequenceScannerComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("sequence-scanner-examine-hint"));
        if (ent.Comp.Makeup != null)
            args.PushMarkup(Loc.GetString("sequence-scanner-examine-makeup", ("name", ent.Comp.Makeup.Name)));
    }

    private void OnAfterInteract(Entity<SequenceScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryComp<DnaConsoleComponent>(target, out var console))
        {
            args.Handled = true;
            ExportMakeup(ent, (target, console), args.User);
            return;
        }

        if (!_genetics.CanHaveGenome(target))
            return;

        args.Handled = true;
        GeneScan(ent, target, args.User);
    }

    private void GeneScan(Entity<SequenceScannerComponent> ent, EntityUid target, EntityUid user)
    {
        if (!_genetics.TryGetGenome(target, out var genome))
        {
            _popup.PopupEntity(Loc.GetString("sequence-scanner-no-sequence", ("target", target)), target, user);
            return;
        }

        ent.Comp.Buffer.Clear();
        foreach (var block in genome.Comp.Blocks)
        {
            ent.Comp.Buffer.Add((block.Mutation, genome.Comp.Active.ContainsKey(block.Mutation)));
        }

        foreach (var mutation in genome.Comp.Active.Keys)
        {
            if (ent.Comp.Buffer.All(b => b.Mutation != mutation))
                ent.Comp.Buffer.Add((mutation, true));
        }

        _audio.PlayPvs(ent.Comp.ScanSound, ent);
        _popup.PopupEntity(Loc.GetString("sequence-scanner-scanned", ("user", user), ("target", target)), target, PopupType.Small);

        var text = new StringBuilder(Loc.GetString("sequence-scanner-saved", ("target", target)));
        foreach (var (mutation, active) in ent.Comp.Buffer)
        {
            text.Append('\n');
            text.Append(active ? $"[bold]{DisplayName(mutation)}[/bold]" : DisplayName(mutation));
        }

        SendChat(user, text.ToString());
    }

    private string DisplayName(ProtoId<GeneticMutationPrototype> mutation)
    {
        var alias = Loc.GetString("genetics-mutation-alias", ("number", _genetics.GetAlias(mutation)));
        if (!_genetics.IsDiscovered(mutation) || !_proto.TryIndex(mutation, out var proto))
            return alias;

        return $"{Loc.GetString(proto.Name)} ({alias})";
    }

    private void SendChat(EntityUid user, string text)
    {
        if (TryComp<ActorComponent>(user, out var actor))
            _chat.DispatchServerMessage(actor.PlayerSession, text);
    }

    private void OnGetVerbs(Entity<SequenceScannerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Buffer.Count == 0)
            return;

        var user = args.User;
        foreach (var (mutation, _) in ent.Comp.Buffer)
        {
            var id = mutation;
            args.Verbs.Add(new Verb
            {
                Text = DisplayName(id),
                Category = AnalyzeCategory,
                Disabled = ent.Comp.ReadyAt != TimeSpan.Zero,
                Act = () => DisplaySequence(ent, user, id),
            });
        }
    }

    private void DisplaySequence(Entity<SequenceScannerComponent> ent, EntityUid user, ProtoId<GeneticMutationPrototype> mutation)
    {
        if (ent.Comp.ReadyAt != TimeSpan.Zero)
        {
            _popup.PopupEntity(Loc.GetString("sequence-scanner-recharging"), ent, user);
            return;
        }

        var sequence = _genetics.GetFullSequence(mutation);
        var display = new StringBuilder();
        for (var i = 0; i < sequence.Length; i += DisplayBlock)
        {
            if (i > 0)
                display.Append('-');

            display.Append(sequence.AsSpan(i, Math.Min(DisplayBlock, sequence.Length - i)));
        }

        SendChat(user, $"{DisplayName(mutation)}:\n[bold]{display}[/bold]");
        ent.Comp.ReadyAt = _timing.CurTime + ent.Comp.Cooldown;
        _appearance.SetData(ent, SequenceScannerVisuals.Recharging, true);
    }

    private void OnGetUtilityVerbs(Entity<SequenceScannerComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_genetics.CanHaveGenome(args.Target))
            return;

        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("sequence-scanner-verb-makeup"),
            Act = () => StartMakeupScan(ent, user, target),
        });
    }

    private void StartMakeupScan(Entity<SequenceScannerComponent> ent, EntityUid user, EntityUid target)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.MakeupScanTime, new SequenceScannerMakeupDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("sequence-scanner-makeup-start", ("user", user), ("target", target)), target, PopupType.SmallCaution);
    }

    private void OnMakeupDoAfter(Entity<SequenceScannerComponent> ent, ref SequenceScannerMakeupDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("sequence-scanner-makeup-failed"), ent, args.User);
            return;
        }

        ent.Comp.Makeup = _makeup.Capture(target);
        _audio.PlayPvs(ent.Comp.ScanSound, ent);
        _popup.PopupEntity(Loc.GetString("sequence-scanner-makeup-scanned"), ent, args.User);
    }

    private void ExportMakeup(Entity<SequenceScannerComponent> ent, Entity<DnaConsoleComponent> console, EntityUid user)
    {
        if (ent.Comp.Makeup is not { } makeup)
        {
            _popup.PopupEntity(Loc.GetString("sequence-scanner-no-makeup"), console, user);
            return;
        }

        var slot = Array.IndexOf(console.Comp.Makeups, null);
        if (slot < 0)
            slot = 0;

        console.Comp.Makeups[slot] = makeup.Clone();
        _console.UpdateUi(console);
        _popup.PopupEntity(Loc.GetString("sequence-scanner-exported", ("slot", slot + 1)), console, user);
    }
}
