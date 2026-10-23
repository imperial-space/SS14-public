using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Abilities.Mime;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Chat;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Inventory;
using Content.Shared.Magic.Components;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>
/// /datum/action/cooldown/spell: проверки can_cast_spell(), произнесение (invocation), звук, искры и дым,
/// уровни заклинания (level_spell / delevel_spell).
/// </summary>
public sealed class ImperialSpellSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly EntProtoId SparksProto = "EffectSparks";
    private static readonly EntProtoId SmokeProto = "WizardSmoke";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialSpellComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialSpellComponent, ActionAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<ImperialSpellComponent, ActionPerformedEvent>(OnPerformed);
        SubscribeLocalEvent<ImperialSpellComponent, ImperialSpellInstantEvent>(OnInstant);
        SubscribeLocalEvent<ImperialSpellComponent, ImperialSpellEntityEvent>(OnEntity);
        SubscribeLocalEvent<ImperialSpellComponent, ImperialSpellWorldEvent>(OnWorld);
    }

    private void OnMapInit(Entity<ImperialSpellComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.BaseName ??= Name(ent);
        UpdateCooldown(ent);
        UpdateName(ent);
        Dirty(ent);
    }

    #region can_cast_spell

    private void OnAttempt(Entity<ImperialSpellComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!CanCast(ent, args.User, true))
            args.Cancelled = true;
    }

    /// <summary>can_cast_spell(): может ли <paramref name="caster"/> сейчас прочитать заклинание.</summary>
    public bool CanCast(Entity<ImperialSpellComponent> spell, EntityUid caster, bool feedback)
    {
        var attempt = new ImperialSpellCastAttemptEvent(caster, spell);
        RaiseLocalEvent(caster, ref attempt);
        if (attempt.Cancelled)
            return false;

        var req = spell.Comp.Requirements;

        if ((req & ImperialSpellRequirements.Station) != 0 && _station.GetOwningStation(caster) == null)
        {
            Feedback(caster, feedback, "imperial-spell-not-here", spell);
            return false;
        }

        if ((req & ImperialSpellRequirements.Mind) != 0 && !_mind.TryGetMind(caster, out _, out _))
            return false;

        if ((req & ImperialSpellRequirements.MimeVow) != 0 && !(TryComp<MimePowersComponent>(caster, out var mime) && !mime.VowBroken))
        {
            Feedback(caster, feedback, "imperial-spell-need-vow", spell);
            return false;
        }

        if (!TryInvoke(spell, caster, feedback))
            return false;

        var human = HasComp<HumanoidProfileComponent>(caster);
        if (human)
        {
            if ((req & ImperialSpellRequirements.WizardGarb) != 0)
            {
                if (!SlotHasGarb(caster, "outerClothing"))
                {
                    Feedback(caster, feedback, "imperial-spell-need-robe", spell);
                    return false;
                }

                if (!SlotHasGarb(caster, "head") && !SlotHasGarb(caster, "eyes"))
                {
                    Feedback(caster, feedback, "imperial-spell-need-hat", spell);
                    return false;
                }
            }
        }
        else
        {
            if ((req & ImperialSpellRequirements.Human) != 0)
            {
                Feedback(caster, feedback, "imperial-spell-humans-only", spell);
                return false;
            }

            // Не человек: хватит любой волшебной одежды при себе (горилла в шляпе мага).
            if ((req & ImperialSpellRequirements.WizardGarb) != 0 && !CarriesGarb(caster))
            {
                Feedback(caster, feedback, "imperial-spell-need-hat", spell);
                return false;
            }
        }

        return true;
    }

    /// <summary>Есть ли на теле одежда мага (CASTING_CLOTHES).</summary>
    public bool HasWizardGarb(EntityUid caster)
    {
        return HasComp<HumanoidProfileComponent>(caster)
            ? SlotHasGarb(caster, "outerClothing") && (SlotHasGarb(caster, "head") || SlotHasGarb(caster, "eyes"))
            : CarriesGarb(caster);
    }

    private bool SlotHasGarb(EntityUid uid, string slot)
    {
        return _inventory.TryGetSlotEntity(uid, slot, out var item) && HasComp<WizardClothesComponent>(item);
    }

    private bool CarriesGarb(EntityUid uid)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
        {
            if (HasComp<WizardClothesComponent>(item))
                return true;
        }

        return false;
    }

    /// <summary>try_invoke(): может ли произнести заклинание.</summary>
    private bool TryInvoke(Entity<ImperialSpellComponent> spell, EntityUid caster, bool feedback)
    {
        if ((spell.Comp.Requirements & ImperialSpellRequirements.CastableWithoutInvocation) != 0)
            return true;

        switch (spell.Comp.InvocationType)
        {
            case ImperialInvocationType.Emote when !_blocker.CanEmote(caster):
                Feedback(caster, feedback, "imperial-spell-cant-gesture", spell);
                return false;
            case ImperialInvocationType.Whisper or ImperialInvocationType.Shout when !_blocker.CanSpeak(caster):
                Feedback(caster, feedback, "imperial-spell-cant-speak", spell);
                return false;
        }

        return true;
    }

    private void Feedback(EntityUid caster, bool feedback, string message, EntityUid spell)
    {
        if (feedback)
            _popup.PopupEntity(Loc.GetString(message, ("spell", Name(spell))), caster, caster, PopupType.SmallCaution);
    }

    #endregion

    #region Эффект

    private void OnInstant(Entity<ImperialSpellComponent> ent, ref ImperialSpellInstantEvent args)
    {
        args.Handled = Dispatch(ent, args.Handled, args.Performer, args.Performer, null);
    }

    private void OnEntity(Entity<ImperialSpellComponent> ent, ref ImperialSpellEntityEvent args)
    {
        args.Handled = Dispatch(ent, args.Handled, args.Performer, args.Target, Transform(args.Target).Coordinates);
    }

    private void OnWorld(Entity<ImperialSpellComponent> ent, ref ImperialSpellWorldEvent args)
    {
        args.Handled = Dispatch(ent, args.Handled, args.Performer, args.Entity, args.Target);
    }

    /// <summary>cast(): эффект — на компонентах действия.</summary>
    private bool Dispatch(Entity<ImperialSpellComponent> ent, bool handled, EntityUid caster, EntityUid? target, Robust.Shared.Map.EntityCoordinates? coords)
    {
        if (handled)
            return true;

        var ev = new Spells.ImperialSpellEffectEvent(caster, target, coords);
        RaiseLocalEvent(ent, ref ev);
        return ev.Handled;
    }

    #endregion

    #region spell_feedback / after_cast

    private void OnPerformed(Entity<ImperialSpellComponent> ent, ref ActionPerformedEvent args)
    {
        var caster = args.Performer;
        if (ent.Comp.SkipCooldown)
        {
            ent.Comp.SkipCooldown = false;
            _actions.ClearCooldown(ent.Owner);
        }

        if (ent.Comp.SkipFeedback)
        {
            ent.Comp.SkipFeedback = false;
            return;
        }

        Invoke(ent, caster);
        if (ent.Comp.CastSound != null)
            _audio.PlayPvs(ent.Comp.CastSound, caster, AudioParams.Default.WithVariation(0.125f));

        var coords = Transform(caster).Coordinates;
        for (var i = 0; i < ent.Comp.Sparks; i++)
        {
            Spawn(SparksProto, coords);
        }

        if (ent.Comp.Smoke > 0)
            Spawn(SmokeProto, coords);

        var cast = new ImperialSpellCastEvent(caster, ent);
        RaiseLocalEvent(caster, ref cast);
        Log.Info($"{ToPrettyString(caster)} прочитал заклинание {Name(ent)}");
    }

    /// <summary>invocation(): произнести фразу, прошептать или изобразить жест.</summary>
    public void Invoke(Entity<ImperialSpellComponent> spell, EntityUid caster)
    {
        var message = spell.Comp.Invocation is { } loc ? Loc.GetString(loc) : null;
        var ev = new ImperialSpellInvocationEvent(spell, message, spell.Comp.InvocationType);
        RaiseLocalEvent(caster, ref ev);
        if (string.IsNullOrEmpty(ev.Message))
            return;

        InvokeText(spell, caster, ev.Message, ev.Type);
    }

    /// <summary>Произнести готовую фразу заклинания (с искажением пробелов).</summary>
    public void InvokeText(Entity<ImperialSpellComponent> spell, EntityUid caster, string text, ImperialInvocationType? type = null)
    {
        switch (type ?? spell.Comp.InvocationType)
        {
            case ImperialInvocationType.Shout:
            case ImperialInvocationType.Whisper:
                if (_random.Prob(spell.Comp.GarbledProb))
                    text = text.Replace(" ", "`");
                _chat.TrySendInGameICMessage(caster, text,
                    (type ?? spell.Comp.InvocationType) == ImperialInvocationType.Shout ? InGameICChatType.Speak : InGameICChatType.Whisper,
                    ChatTransmitRange.Normal, checkRadioPrefix: false, ignoreActionBlocker: true);
                break;
            case ImperialInvocationType.Emote:
                _chat.TrySendInGameICMessage(caster, text, InGameICChatType.Emote, ChatTransmitRange.Normal,
                    checkRadioPrefix: false, ignoreActionBlocker: true);
                if (spell.Comp.InvocationSelf is { } self)
                    _popup.PopupEntity(Loc.GetString(self), caster, caster);
                break;
        }
    }

    #endregion

    #region Уровни

    /// <summary>level_spell(): уровень +1, откат меньше на cooldown_reduction_per_rank (не меньше 0,25 с).</summary>
    public bool LevelSpell(Entity<ImperialSpellComponent> spell, bool bypassCap = false)
    {
        if (spell.Comp.MaxLevel <= 1 || !bypassCap && spell.Comp.Level >= spell.Comp.MaxLevel)
            return false;

        spell.Comp.Level++;
        UpdateCooldown(spell);
        UpdateName(spell);
        Dirty(spell);
        return true;
    }

    /// <summary>delevel_spell().</summary>
    public bool DelevelSpell(Entity<ImperialSpellComponent> spell)
    {
        if (spell.Comp.MaxLevel <= 1 || spell.Comp.Level <= 1)
            return false;

        spell.Comp.Level--;
        UpdateCooldown(spell);
        UpdateName(spell);
        Dirty(spell);
        return true;
    }

    /// <summary>Текущий откат с учётом уровня.</summary>
    public TimeSpan GetCooldown(ImperialSpellComponent spell)
    {
        var reduced = spell.Cooldown - spell.CooldownReductionPerRank * (spell.Level - 1);
        if (spell.CooldownReductionPerRank < TimeSpan.Zero)
            return reduced;

        return reduced < TimeSpan.FromSeconds(0.25) ? TimeSpan.FromSeconds(0.25) : reduced;
    }

    private void UpdateCooldown(Entity<ImperialSpellComponent> spell)
    {
        _actions.SetUseDelay(spell.Owner, GetCooldown(spell.Comp));
    }

    /// <summary>get_spell_title(): «Огненный шар [ускоренное]».</summary>
    private void UpdateName(Entity<ImperialSpellComponent> spell)
    {
        var name = spell.Comp.BaseName ?? Name(spell);
        if (spell.Comp.Level is >= 2 and <= 6)
            name = Loc.GetString("imperial-spell-title", ("name", name), ("title", Loc.GetString($"imperial-spell-title-{spell.Comp.Level}")));
        _meta.SetEntityName(spell, name);
    }

    #endregion

    /// <summary>Действие-заклинание с прототипом <paramref name="proto"/> у разума или тела.</summary>
    public Entity<ImperialSpellComponent>? FindSpell(EntityUid user, string proto)
    {
        foreach (var action in _actions.GetActions(user))
        {
            if (MetaData(action.Owner).EntityPrototype?.ID == proto && TryComp<ImperialSpellComponent>(action.Owner, out var spell))
                return (action.Owner, spell);
        }

        return null;
    }
}
