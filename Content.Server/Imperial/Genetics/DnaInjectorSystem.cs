using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Genetics;

/// <summary>
/// ДНК-инжектор (dnainjector из SS13). Активатор собирает спящий ген цели, если он у неё есть, и тогда
/// становится «исследованием» (в консоли из него можно добыть хромосому). Мутатор добавляет мутации сверху.
/// </summary>
public sealed class DnaInjectorSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly GeneticMakeupSystem _makeup = default!;
    [Dependency] private readonly GeneticsSystem _genetics = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DnaInjectorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DnaInjectorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DnaInjectorComponent, DnaInjectDoAfterEvent>(OnInjectDoAfter);
        SubscribeLocalEvent<DnaInjectorComponent, ExaminedEvent>(OnExamined);
    }

    private void OnAfterInteract(Entity<DnaInjectorComponent> injector, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !_genetics.CanHaveGenome(target))
            return;

        args.Handled = true;
        StartInject(injector, args.User, target);
    }

    private void OnUseInHand(Entity<DnaInjectorComponent> injector, ref UseInHandEvent args)
    {
        if (args.Handled || !_genetics.CanHaveGenome(args.User))
            return;

        args.Handled = true;
        StartInject(injector, args.User, args.User);
    }

    private void StartInject(Entity<DnaInjectorComponent> injector, EntityUid user, EntityUid target)
    {
        if (injector.Comp.Used)
        {
            _popup.PopupEntity(Loc.GetString("dna-injector-used"), injector, user);
            return;
        }

        var delay = user == target ? TimeSpan.FromSeconds(1) : injector.Comp.InjectTime;
        var args = new DoAfterArgs(EntityManager, user, delay, new DnaInjectDoAfterEvent(), injector, target, injector)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        if (user != target)
            _popup.PopupEntity(Loc.GetString("dna-injector-start", ("user", user), ("target", target)), target, target, PopupType.MediumCaution);
    }

    private void OnInjectDoAfter(Entity<DnaInjectorComponent> injector, ref DnaInjectDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target || injector.Comp.Used)
            return;

        args.Handled = true;
        Inject(injector, args.User, target);
    }

    public void Inject(Entity<DnaInjectorComponent> injector, EntityUid user, EntityUid target)
    {
        if (!_genetics.TryGetGenome(target, out var genome))
            return;

        var affected = false;
        if (injector.Comp.Mode == DnaInjectorMode.Makeup && injector.Comp.Makeup is { } makeup)
        {
            _makeup.Apply(target, makeup, injector.Comp.MakeupType);
            _genetics.AddGeneticDamage(target, 100);
            affected = true;
        }

        foreach (var stored in injector.Comp.Mode == DnaInjectorMode.Makeup ? new List<StoredMutation>() : injector.Comp.Mutations)
        {
            if (injector.Comp.Mode == DnaInjectorMode.Activator)
            {
                // Активатор работает, только если у цели есть этот спящий ген.
                if (!_genetics.CompleteBlock(target, stored.Mutation))
                    continue;

                injector.Comp.Research = true;
                affected = true;
                continue;
            }

            affected |= _genetics.TryActivate(target, stored.Mutation, MutationSource.Mutator, stored.Chromosome);
        }

        injector.Comp.Used = true;
        Dirty(injector);
        _appearance.SetData(injector, DnaInjectorVisuals.Used, true);
        _metaData.SetEntityName(injector, Loc.GetString("dna-injector-used-name", ("name", Name(injector))));
        _audio.PlayPvs(InjectSound, target);

        if (!affected)
            _popup.PopupEntity(Loc.GetString("dna-injector-no-effect"), target, user);

        _adminLog.Add(LogType.ForceFeed, LogImpact.Medium,
            $"{ToPrettyString(user):user} injected {ToPrettyString(target):target} with {ToPrettyString(injector):injector} ({injector.Comp.Mode})");
    }

    private void OnExamined(Entity<DnaInjectorComponent> injector, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(injector.Comp.Mode switch
        {
            DnaInjectorMode.Activator => "dna-injector-examine-activator",
            DnaInjectorMode.Makeup => "dna-injector-examine-makeup",
            _ => "dna-injector-examine-mutator",
        }));

        if (injector.Comp.Used)
            args.PushMarkup(Loc.GetString("dna-injector-examine-used"));
    }
}
