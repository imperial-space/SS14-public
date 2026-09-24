using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Events;
using Content.Server.Imperial.DeimonFly.DeathNote.Presets;
using Content.Shared.Eye;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Scheduling;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// Серверная точка входа тетради смерти: регистрирует события и хранит общие зависимости системы.
/// </summary>
public sealed partial class DeathNoteSystem : EntitySystem
{
    private const int MaxAuditFieldLength = 512;
    private const string UnrevivableReason = "death-note-unrevivable";

    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DeathNoteJournalSystem _journal = default!;
    [Dependency] private readonly DeathNoteSchedulerSystem _scheduler = default!;
    [Dependency] private readonly DeathNotePresetRegistrySystem _presetRegistry = default!;
    [Dependency] private readonly DeathNoteRoundUnrevivableRuleSystem _roundUnrevivableRule = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathNoteComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DeathNoteComponent, GotEquippedHandEvent>(OnNotebookEquipped);
        SubscribeLocalEvent<DeathNoteComponent, GotUnequippedHandEvent>(OnNotebookUnequipped);
        SubscribeLocalEvent<DeathNoteComponent, InteractUsingEvent>(OnNotebookInteractUsing);
        SubscribeLocalEvent<DeathNoteComponent, ComponentShutdown>(OnNotebookShutdown);
        SubscribeLocalEvent<DeathNoteOwnerComponent, ComponentShutdown>(OnOwnerShutdown);
        SubscribeLocalEvent<DeathNoteOwner2Component, ComponentShutdown>(OnOwner2Shutdown);
        SubscribeLocalEvent<DeathNoteOwner3Component, ComponentShutdown>(OnOwner3Shutdown);
        SubscribeLocalEvent<DeathNoteHolderComponent, ComponentShutdown>(OnHolderShutdown);
        SubscribeLocalEvent<DeathNoteHolder2Component, ComponentShutdown>(OnHolder2Shutdown);
        SubscribeLocalEvent<DeathNoteHolder3Component, ComponentShutdown>(OnHolder3Shutdown);
        SubscribeLocalEvent<DeathNoteComponent, BoundUserInterfaceMessageAttempt>(OnNotebookUiAttempt);
        SubscribeLocalEvent<DeathNoteScheduledEntryEvent>(OnScheduledEntry);
        SubscribeLocalEvent<DeathNoteGuidedScenarioComponent, DeathNoteGuidedEffectStartEvent>(
            OnGuidedEffectStart);
        SubscribeLocalEvent<DeathNoteTimersCancelledEvent>(OnTimersCancelled);
        SubscribeLocalEvent<DeathNoteTargetTrackingComponent, MobStateChangedEvent>(OnTrackedTargetStateChanged);
        SubscribeLocalEvent<DeathNoteTargetTrackingComponent, GibbedBeforeDeletionEvent>(OnTrackedTargetGibbed);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Subs.BuiEvents<DeathNoteComponent>(DeathNoteUiKey.Notebook, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnNotebookOpened);
            subs.Event<DeathNoteSpreadRequestMessage>(OnSpreadRequested);
            subs.Event<DeathNoteSubmitMessage>(OnSubmit);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        PruneExpiredTargetTracking();
    }

    private void OnMapInit(Entity<DeathNoteComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<DeathNoteRuntimeComponent>(ent.Owner);
    }
}
