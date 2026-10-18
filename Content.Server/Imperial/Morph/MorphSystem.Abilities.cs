using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Shared.Singularity.Components;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Morph;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Morph;

public sealed partial class MorphSystem
{
    /// <summary>Сколько секунд ИИ-морф ждёт, прежде чем снова притвориться предметом.</summary>
    private static readonly TimeSpan AiDisguiseInterval = TimeSpan.FromSeconds(5);

    /// <summary>oview(src): что видит морф, выбирая облик без игрока.</summary>
    private const float AiDisguiseRange = 7f;

    private readonly Dictionary<EntityUid, TimeSpan> _nextAiDisguise = new();

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<MorphComponent, MorphMimicActionEvent>(OnMimic);
        SubscribeLocalEvent<MorphComponent, MorphRememberFormDoAfterEvent>(OnRememberDoAfter);
        SubscribeLocalEvent<MorphComponent, MorphFormChosenMessage>(OnFormChosen);
        SubscribeLocalEvent<MorphComponent, MorphTakeFormDoAfterEvent>(OnTakeFormDoAfter);

        SubscribeLocalEvent<MorphComponent, MorphAmbushActionEvent>(OnAmbushAction);
        SubscribeLocalEvent<MorphComponent, MorphAmbushDoAfterEvent>(OnAmbushDoAfter);

        SubscribeLocalEvent<MorphComponent, MorphPassAirlockActionEvent>(OnPassAirlock);
        SubscribeLocalEvent<MorphComponent, MorphPassAirlockDoAfterEvent>(OnPassAirlockDoAfter);

        SubscribeLocalEvent<MorphComponent, MorphOpenVentActionEvent>(OnOpenVent);
        SubscribeLocalEvent<MorphComponent, MorphOpenVentDoAfterEvent>(OnOpenVentDoAfter);

        SubscribeLocalEvent<MorphComponent, MorphReproduceActionEvent>(OnReproduce);
    }

    /// <summary>spell_handler/morph/can_cast(): хватает ли еды на способность.</summary>
    private bool TryPayHunger(Entity<MorphComponent> morph, EntityUid action)
    {
        var cost = CompOrNull<MorphActionCostComponent>(action)?.HungerCost ?? 0;
        if (cost <= 0)
            return true;

        if (!UseFood(morph, cost))
        {
            _popup.PopupEntity(Loc.GetString("morph-not-enough-food", ("cost", cost)), morph, morph, PopupType.SmallCaution);
            return false;
        }

        ShowFoodLeft(morph);
        return true;
    }

    /// <summary>revert_cast(): вернуть еду за сорвавшуюся способность.</summary>
    private void RefundHunger(Entity<MorphComponent> morph, int cost)
    {
        if (cost <= 0)
            return;

        AddFood(morph, cost);
        ShowFoodLeft(morph);
    }

    private int GetCost(EntProtoId actionProto)
    {
        return _prototype.Index(actionProto).TryGetComponent<MorphActionCostComponent>(out var cost, EntityManager.ComponentFactory)
            ? cost.HungerCost
            : 0;
    }

    #region Мимикрия

    private void OnMimic(Entity<MorphComponent> ent, ref MorphMimicActionEvent args)
    {
        if (args.Handled)
            return;

        // По себе — выбрать облик.
        if (args.Target == ent.Owner)
        {
            if (ent.Comp.Forms.Count == 0 && !ent.Comp.Morphed)
            {
                _popup.PopupEntity(Loc.GetString("morph-mimic-no-forms"), ent, ent, PopupType.SmallCaution);
                return;
            }

            args.Handled = true;
            var forms = ent.Comp.Forms.Select(f => f.Name).ToList();
            _ui.SetUiState(ent.Owner, MorphFormUiKey.Key, new MorphFormBoundUserInterfaceState(forms, ent.Comp.Morphed));
            _ui.OpenUi(ent.Owner, MorphFormUiKey.Key, ent.Owner);
            return;
        }

        if (!IsValidForm(ent, args.Target))
        {
            _popup.PopupEntity(Loc.GetString("morph-mimic-invalid"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var name = MetaData(args.Target).EntityName;
        if (ent.Comp.Forms.Any(f => f.Name == name))
        {
            _popup.PopupEntity(Loc.GetString("morph-mimic-known", ("target", args.Target)), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        if (ent.Comp.Forms.Count >= ent.Comp.MaxForms)
            _popup.PopupEntity(Loc.GetString("morph-mimic-forgetting", ("form", ent.Comp.Forms[ent.Comp.NextOverrideIndex].Name)), ent, ent);

        _popup.PopupEntity(Loc.GetString("morph-mimic-remember-start", ("target", args.Target)), ent, ent);

        var doAfter = new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(2), new MorphRememberFormDoAfterEvent(), ent, target: args.Target)
        {
            BreakOnMove = true,
            NeedHand = false,
            DistanceThreshold = null,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    /// <summary>valid_target(): ни эффекты, ни другие морфы, ни призраки, ни сингулярность.</summary>
    private bool IsValidForm(Entity<MorphComponent> morph, EntityUid target)
    {
        if (target == morph.Owner || TerminatingOrDeleted(target))
            return false;

        if (HasComp<MorphComponent>(target) || HasComp<GhostComponent>(target) || HasComp<SingularityComponent>(target))
            return false;

        // Облик возможен только у того, что видно на карте.
        if (!HasComp<Robust.Shared.GameObjects.MetaDataComponent>(target) || MetaData(target).EntityPrototype == null)
            return false;

        return !_container.IsEntityInContainer(target);
    }

    private void OnRememberDoAfter(Entity<MorphComponent> ent, ref MorphRememberFormDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled || args.Target is not { } target || !Exists(target))
        {
            _popup.PopupEntity(Loc.GetString("morph-lose-focus"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var meta = MetaData(target);
        var form = new MorphForm
        {
            Name = meta.EntityName,
            Description = meta.EntityDescription,
            Source = target,
            Proto = meta.EntityPrototype?.ID,
        };

        if (ent.Comp.Forms.Count >= ent.Comp.MaxForms)
        {
            ent.Comp.Forms[ent.Comp.NextOverrideIndex] = form;
            ent.Comp.NextOverrideIndex = (ent.Comp.NextOverrideIndex + 1) % ent.Comp.MaxForms;
        }
        else
        {
            ent.Comp.Forms.Add(form);
        }

        _popup.PopupEntity(Loc.GetString("morph-mimic-remembered", ("target", target)), ent, ent);
    }

    private void OnFormChosen(Entity<MorphComponent> ent, ref MorphFormChosenMessage args)
    {
        if (args.Actor != ent.Owner)
            return;

        _ui.CloseUi(ent.Owner, MorphFormUiKey.Key, ent.Owner);

        if (args.Index == MorphFormChosenMessage.OriginalForm)
        {
            RestoreForm(ent);
            return;
        }

        if (args.Index < 0 || args.Index >= ent.Comp.Forms.Count)
            return;

        _popup.PopupEntity(Loc.GetString("morph-mimic-take-start", ("form", ent.Comp.Forms[args.Index].Name)), ent, ent);

        var doAfter = new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(2), new MorphTakeFormDoAfterEvent(args.Index), ent)
        {
            BreakOnMove = true,
            NeedHand = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnTakeFormDoAfter(Entity<MorphComponent> ent, ref MorphTakeFormDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled || args.Index < 0 || args.Index >= ent.Comp.Forms.Count)
        {
            _popup.PopupEntity(Loc.GetString("morph-lose-focus"), ent, ent, PopupType.SmallCaution);
            return;
        }

        TakeForm(ent, ent.Comp.Forms[args.Index]);
    }

    /// <summary>take_form() + assume(): облик, медленнее и слабее.</summary>
    public void TakeForm(Entity<MorphComponent> morph, MorphForm form)
    {
        var oldName = Name(morph);

        morph.Comp.Morphed = true;
        morph.Comp.FormSource = form.Source is { } source && Exists(source) ? source : null;
        morph.Comp.FormProto = form.Proto;
        morph.Comp.FormVersion++;
        Dirty(morph);

        _meta.SetEntityName(morph, form.Name);
        _meta.SetEntityDescription(morph, form.Description);
        _speed.RefreshMovementSpeedModifiers(morph);

        _audio.PlayPvs(BoneBreak, morph);
        _popup.PopupEntity(Loc.GetString("morph-take-form-others", ("old", oldName), ("new", form.Name)), morph, Filter.PvsExcept(morph), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("morph-take-form-self", ("new", form.Name)), morph, morph);
    }

    /// <summary>restore_form() + restore(): обратно в кучу зелёной плоти.</summary>
    public void RestoreForm(Entity<MorphComponent> morph, bool showMessage = true)
    {
        if (!morph.Comp.Morphed)
            return;

        var oldName = Name(morph);

        morph.Comp.Morphed = false;
        morph.Comp.FormSource = null;
        morph.Comp.FormProto = null;
        morph.Comp.FormVersion++;
        Dirty(morph);

        _meta.SetEntityName(morph, morph.Comp.OriginalName ?? Loc.GetString("morph-name"));
        _meta.SetEntityDescription(morph, morph.Comp.OriginalDescription ?? string.Empty);
        _speed.RefreshMovementSpeedModifiers(morph);

        if (morph.Comp.AmbushPrepared)
            _popup.PopupEntity(Loc.GetString("morph-ambush-faded"), morph, morph, PopupType.SmallCaution);
        FailedAmbush(morph);

        _audio.PlayPvs(BoneBreak, morph);
        if (showMessage)
        {
            _popup.PopupEntity(Loc.GetString("morph-restore-others", ("old", oldName)), morph, Filter.PvsExcept(morph), true, PopupType.MediumCaution);
            _popup.PopupEntity(Loc.GetString("morph-restore-self"), morph, morph);
        }
    }

    #endregion

    #region Засада

    /// <summary>spell/morph_spell/ambush: 6 секунд неподвижности в облике.</summary>
    private void OnAmbushAction(Entity<MorphComponent> ent, ref MorphAmbushActionEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.Morphed)
        {
            _popup.PopupEntity(Loc.GetString("morph-ambush-need-disguise"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.AmbushPrepared)
        {
            _popup.PopupEntity(Loc.GetString("morph-ambush-already"), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("morph-ambush-start"), ent, ent);

        var doAfter = new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(6), new MorphAmbushDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            NeedHand = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnAmbushDoAfter(Entity<MorphComponent> ent, ref MorphAmbushDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!ent.Comp.Morphed)
        {
            _popup.PopupEntity(Loc.GetString("morph-ambush-need-stay-morphed"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("morph-ambush-need-stay-still"), ent, ent, PopupType.SmallCaution);
            return;
        }

        PrepareAmbush(ent);
    }

    #endregion

    #region Шлюзы

    /// <summary>spell/morph_spell/pass_airlock: только в истинном облике и не через болты.</summary>
    private void OnPassAirlock(Entity<MorphComponent> ent, ref MorphPassAirlockActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Morphed)
        {
            _popup.PopupEntity(Loc.GetString("morph-airlock-true-form"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!HasComp<AirlockComponent>(args.Target))
            return;

        if (IsBolted(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("morph-airlock-bolted", ("airlock", args.Target)), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("morph-airlock-start-others", ("morph", ent.Owner), ("airlock", args.Target)), ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("morph-airlock-start-self", ("airlock", args.Target)), ent, ent);

        var doAfter = new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(6), new MorphPassAirlockDoAfterEvent(), ent, target: args.Target)
        {
            BreakOnMove = true,
            NeedHand = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private bool IsBolted(EntityUid airlock)
    {
        return TryComp<DoorBoltComponent>(airlock, out var bolts) && bolts.BoltsDown;
    }

    private void OnPassAirlockDoAfter(Entity<MorphComponent> ent, ref MorphPassAirlockDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } airlock)
            return;

        args.Handled = true;

        if (ent.Comp.Morphed)
        {
            _popup.PopupEntity(Loc.GetString("morph-airlock-stay-true-form", ("airlock", airlock)), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (IsBolted(airlock))
        {
            _popup.PopupEntity(Loc.GetString("morph-airlock-bolted", ("airlock", airlock)), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (args.Cancelled || TerminatingOrDeleted(airlock))
        {
            _popup.PopupEntity(Loc.GetString("morph-airlock-stay-still", ("airlock", airlock)), ent, ent, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("morph-airlock-pass-others", ("morph", ent.Owner), ("airlock", airlock)), ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("morph-airlock-pass-self", ("airlock", airlock)), ent, ent);

        // forceMove(A.loc): закрытый шлюз в SS14 твёрдый, поэтому морф выходит на клетку за ним.
        var morphPos = _xform.GetWorldPosition(ent);
        var airlockXform = Transform(airlock);
        var airlockPos = _xform.GetWorldPosition(airlockXform);
        var dir = airlockPos - morphPos;
        var step = Math.Abs(dir.X) >= Math.Abs(dir.Y)
            ? new Vector2(MathF.Sign(dir.X), 0)
            : new Vector2(0, MathF.Sign(dir.Y));

        _xform.SetWorldPosition(ent, airlockPos + step);
    }

    #endregion

    #region Вентиляции

    /// <summary>spell/morph_spell/open_vent: кислота на заваренные вентиляции и скрубберы рядом.</summary>
    private void OnOpenVent(Entity<MorphComponent> ent, ref MorphOpenVentActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<VentCrawlingComponent>(ent) || _container.IsEntityInContainer(ent))
            return;

        if (FindWeldedVents(ent).Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("morph-vent-none"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!TryPayHunger(ent, args.Action))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("morph-vent-start"), ent, ent);

        var doAfter = new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(2), new MorphOpenVentDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            NeedHand = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private List<EntityUid> FindWeldedVents(EntityUid morph)
    {
        var result = new List<EntityUid>();
        foreach (var ent in _lookup.GetEntitiesInRange<WeldableComponent>(Transform(morph).Coordinates, 1.5f))
        {
            if (!HasComp<GasVentPumpComponent>(ent) && !HasComp<GasVentScrubberComponent>(ent))
                continue;

            if (_weldable.IsWelded(ent, ent.Comp))
                result.Add(ent);
        }

        return result;
    }

    private void OnOpenVentDoAfter(Entity<MorphComponent> ent, ref MorphOpenVentDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("morph-vent-cancel"), ent, ent, PopupType.SmallCaution);
            RefundHunger(ent, GetCost(ent.Comp.OpenVentAction));
            return;
        }

        // Кислоте нужно 2 секунды, чтобы проесть сварной шов.
        foreach (var vent in FindWeldedVents(ent))
        {
            _audio.PlayPvs(WelderSound, vent);
            var captured = vent;
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(2), () =>
            {
                if (!TerminatingOrDeleted(captured))
                    _weldable.SetWeldedState(captured, false);
            });
        }
    }

    #endregion

    #region Размножение

    /// <summary>spell/morph_spell/reproduce: 150 еды, только на полу.</summary>
    private void OnReproduce(Entity<MorphComponent> ent, ref MorphReproduceActionEvent args)
    {
        if (args.Handled)
            return;

        if (_container.IsEntityInContainer(ent) || HasComp<VentCrawlingComponent>(ent) || Transform(ent).GridUid == null)
        {
            _popup.PopupEntity(Loc.GetString("morph-reproduce-floor"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!TryPayHunger(ent, args.Action))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("morph-reproduce-start"), ent, ent, PopupType.Medium);

        // Новый морф ждёт призрака через гост-роль, а пока никто не занял — им правит ИИ.
        var child = Spawn(ent.Comp.ReproduceProto, Transform(ent).Coordinates);
        _adminLog.Add(Content.Shared.Database.LogType.Action, Content.Shared.Database.LogImpact.Medium,
            $"{ToPrettyString(ent):morph} made a new morph {ToPrettyString(child)}");
    }

    #endregion

    #region ИИ

    /// <summary>
    /// AIShouldSleep(): морф без игрока, которому некого бить, принимает облик ближайшего предмета
    /// и сразу садится в засаду («они жульничают, да»).
    /// </summary>
    private void UpdateAi(Entity<MorphComponent> morph, TimeSpan now)
    {
        if (morph.Comp.Morphed || HasComp<ActorComponent>(morph) || _mobs.IsDead(morph))
            return;

        if (_nextAiDisguise.TryGetValue(morph, out var next) && now < next)
            return;

        _nextAiDisguise[morph] = now + AiDisguiseInterval;

        var candidates = new List<EntityUid>();
        foreach (var item in _lookup.GetEntitiesInRange<ItemComponent>(Transform(morph).Coordinates, AiDisguiseRange))
        {
            if (IsValidForm(morph, item))
                candidates.Add(item);
        }

        if (candidates.Count == 0)
            return;

        var target = _random.Pick(candidates);
        var meta = MetaData(target);
        TakeForm(morph, new MorphForm
        {
            Name = meta.EntityName,
            Description = meta.EntityDescription,
            Source = target,
            Proto = meta.EntityPrototype?.ID,
        });
        PrepareAmbush(morph);
    }

    #endregion
}
