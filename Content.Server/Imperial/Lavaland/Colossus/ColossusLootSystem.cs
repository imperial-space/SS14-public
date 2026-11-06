using Robust.Shared.Prototypes;
using Content.Shared.Imperial.Helpers;
using Content.Shared.Dataset;
using Content.Server.Actions;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Imperial.Lavaland.ColossusLoot;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffectNew;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Content.Shared.Damage.Prototypes;

namespace Content.Server.Imperial.Lavaland.Colossus;

public sealed class ColossusLootSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _mutedUntil = new();

    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceStopLocalizedDataset = "ImperialColossusVoiceStop";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceWeakenLocalizedDataset = "ImperialColossusVoiceWeaken";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceSleepLocalizedDataset = "ImperialColossusVoiceSleep";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceVomitLocalizedDataset = "ImperialColossusVoiceVomit";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceSilenceLocalizedDataset = "ImperialColossusVoiceSilence";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceWakeLocalizedDataset = "ImperialColossusVoiceWake";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceHealLocalizedDataset = "ImperialColossusVoiceHeal";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoicePainLocalizedDataset = "ImperialColossusVoicePain";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceBurnLocalizedDataset = "ImperialColossusVoiceBurn";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceStandLocalizedDataset = "ImperialColossusVoiceStand";
    private static readonly ProtoId<LocalizedDatasetPrototype> ImperialColossusVoiceRestLocalizedDataset = "ImperialColossusVoiceRest";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EyeOfGodComponent, HeldRelayedEvent<DropAttemptEvent>>(OnEyeDropAttempt);

        SubscribeLocalEvent<VoiceOfGodComponent, ClothingGotEquippedEvent>(OnVoiceEquipped);
        SubscribeLocalEvent<VoiceOfGodComponent, ClothingGotUnequippedEvent>(OnVoiceUnequipped);
        SubscribeLocalEvent<VoiceOfGodPrepareActionEvent>(OnVoicePrepareAction);
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpokeWithVoice);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_mutedUntil.Count == 0)
            return;

        var now = _timing.CurTime;
        var toRemove = new List<EntityUid>();

        foreach (var (uid, until) in _mutedUntil)
        {
            if (until > now)
                continue;

            RemComp<MutedComponent>(uid);
            toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
        {
            _mutedUntil.Remove(uid);
        }
    }

    private static void OnEyeDropAttempt(Entity<EyeOfGodComponent> eye, ref HeldRelayedEvent<DropAttemptEvent> args)
    {
        args.Args.Cancel();
    }

    private void OnVoiceEquipped(Entity<VoiceOfGodComponent> voice, ref ClothingGotEquippedEvent args)
    {
        _actions.AddAction(args.Wearer, ref voice.Comp.PrepareActionEntity, voice.Comp.PrepareAction, voice.Owner);
        Dirty(voice);
    }

    private void OnVoiceUnequipped(Entity<VoiceOfGodComponent> voice, ref ClothingGotUnequippedEvent args)
    {
        _actions.RemoveProvidedActions(args.Wearer, voice.Owner);

        voice.Comp.PrepareActionEntity = null;
        RemComp<ColossusLootAwaitingVoiceCommandComponent>(args.Wearer);
        Dirty(voice);
    }

    private void OnVoicePrepareAction(VoiceOfGodPrepareActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetEquippedVoice(args.Performer, out var voice))
            return;

        var cords = voice.Comp;
        if (_timing.CurTime < cords.NextUse)
        {
            var left = (cords.NextUse - _timing.CurTime).TotalSeconds;
            _popup.PopupEntity(Loc.GetString("imperial-lavaland-colossus-loot-system-1", ("arg0", (left).ToString("0"))), args.Performer, args.Performer);
            args.Handled = true;
            return;
        }

        EnsureComp<ColossusLootAwaitingVoiceCommandComponent>(args.Performer);
        _popup.PopupEntity(Loc.GetString("imperial-lavaland-colossus-loot-system-2"), args.Performer, args.Performer);
        args.Handled = true;
    }

    private void OnEntitySpokeWithVoice(EntitySpokeEvent args)
    {
        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        if (!TryGetEquippedVoice(args.Source, out var voice))
            return;

        var hasPrefix = TryStripPrefix(args.Message, out var text);
        var wasPending = RemComp<ColossusLootAwaitingVoiceCommandComponent>(args.Source);
        if (!hasPrefix && !wasPending)
            return;

        if (!hasPrefix)
            text = args.Message.Trim().ToLowerInvariant();

        if (_timing.CurTime < voice.Comp.NextUse)
            return;

        if (!TryResolveCommand(text, out var command, out var cooldown))
        {
            // Даже пустая/неизвестная команда через Глас Бога уходит в базовый кулдаун.
            voice.Comp.NextUse = _timing.CurTime + TimeSpan.FromSeconds(voice.Comp.BaseCooldown);
            Dirty(voice);
            return;
        }

        _chat.TrySendInGameICMessage(args.Source, args.Message, InGameICChatType.Speak, false, hideLog: true);
        ApplyCommand(args.Source, command, voice.Comp.Radius);

        voice.Comp.NextUse = _timing.CurTime + TimeSpan.FromSeconds(cooldown);
        Dirty(voice);
    }

    private bool TryGetEquippedVoice(EntityUid wearer, out Entity<VoiceOfGodComponent> voice)
    {
        if (_inventory.TryGetSlotEntity(wearer, "neck", out var neckItem) &&
            TryComp<VoiceOfGodComponent>(neckItem, out var voiceComp))
        {
            voice = (neckItem.Value, voiceComp);
            return true;
        }

        voice = default;
        return false;
    }

    /// <summary>
    /// ImperialHolyComponent.Role >= Priest. Компонент священника ищется по имени,
    /// чтобы лаваленд не зависел от сборки капеллана.
    /// </summary>
    private bool IsPriest(EntityUid uid)
    {
        if (!EntityManager.ComponentFactory.TryGetRegistration("ImperialHoly", out var registration)
            || !EntityManager.TryGetComponent(uid, registration.Type, out var holy))
            return false;

        var role = registration.Type.GetField("Role")?.GetValue(holy);
        return role != null && Convert.ToByte(role) >= 2;
    }

    private void ApplyCommand(EntityUid speaker, VoiceOfGodCommand command, float radius)
    {
        // Капеллан говорит голосом бога вдвое сильнее (voice_of_god_power = 2 в SS13).
        var power = IsPriest(speaker) ? 2f : 1f;

        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(Transform(speaker).Coordinates, radius, nearby);

        var canAffectSelf = command is VoiceOfGodCommand.Stand or VoiceOfGodCommand.Wake;

        foreach (var target in nearby)
        {
            if (target == speaker && !canAffectSelf)
                continue;

            if (TryComp<MobStateComponent>(target, out var mobState) && mobState.CurrentState == MobState.Dead)
                continue;

            switch (command)
            {
                case VoiceOfGodCommand.Stop:
                    _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(4 * power));
                    _stun.TryKnockdown(target, TimeSpan.FromSeconds(4 * power), force: true);
                    break;
                case VoiceOfGodCommand.Weaken:
                    _stamina.TakeStaminaDamage(target, 80f * power, source: speaker);
                    break;
                case VoiceOfGodCommand.Sleep:
                    _status.TryAddStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(3 * power));
                    break;
                case VoiceOfGodCommand.Vomit:
                    _chat.TryEmoteWithChat(target, "Vomit");
                    break;
                case VoiceOfGodCommand.Silence:
                    EnsureComp<MutedComponent>(target);
                    _mutedUntil[target] = _timing.CurTime + TimeSpan.FromSeconds(20 * power);
                    break;
                case VoiceOfGodCommand.Wake:
                    WakeAndStand(target);
                    break;
                case VoiceOfGodCommand.Heal:
                    {
                        var spec = new DamageSpecifier();
                        spec.DamageDict[BluntDamageType] = FixedPoint2.New(-20 * power);
                        _damageable.TryChangeDamage(target, spec, origin: speaker);
                        break;
                    }
                case VoiceOfGodCommand.Pain:
                    {
                        var spec = new DamageSpecifier();
                        spec.DamageDict[BluntDamageType] = FixedPoint2.New(15 * power);
                        _damageable.TryChangeDamage(target, spec, origin: speaker);
                        break;
                    }
                case VoiceOfGodCommand.Burn:
                    {
                        if (TryComp<FlammableComponent>(target, out var flammable))
                        {
                            _flammable.AdjustFireStacks(target, flammable.FirestacksOnIgnite, flammable);
                            _flammable.Ignite(target, speaker, flammable);
                        }
                        break;
                    }
                case VoiceOfGodCommand.Stand:
                    WakeAndStand(target);
                    break;
                case VoiceOfGodCommand.Rest:
                    _status.TryAddStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(2 * power));
                    break;
            }
        }
    }

    private void WakeAndStand(EntityUid target)
    {
        _status.TryRemoveStatusEffect(target, SleepingSystem.StatusEffectForcedSleeping);
        _stun.TryUnstun(target);
        _stun.TryStanding(target);
    }

    private static bool TryStripPrefix(string message, out string text)
    {
        var trimmed = message.Trim();
        if (trimmed.StartsWith(":~"))
        {
            text = trimmed[2..].Trim().ToLowerInvariant();
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static bool TryResolveCommand(string text, out VoiceOfGodCommand command, out float cooldown)
    {
        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceStopLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Stop;
            cooldown = 120f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceWeakenLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Weaken;
            cooldown = 120f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceSleepLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Sleep;
            cooldown = 120f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceVomitLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Vomit;
            cooldown = 120f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceSilenceLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Silence;
            cooldown = 120f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceWakeLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Wake;
            cooldown = 60f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceHealLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Heal;
            cooldown = 60f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoicePainLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Pain;
            cooldown = 60f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceBurnLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Burn;
            cooldown = 60f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceStandLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Stand;
            cooldown = 60f;
            return true;
        }

        if (ContainsAny(text, ImperialLocalizedLists.Get(ImperialColossusVoiceRestLocalizedDataset)))
        {
            command = VoiceOfGodCommand.Rest;
            cooldown = 30f;
            return true;
        }

        command = default;
        cooldown = 15f;
        return false;
    }

    private static bool ContainsAny(string text, params string[] words)
    {
        foreach (var word in words)
        {
            if (text.Contains(word))
                return true;
        }

        return false;
    }
}
