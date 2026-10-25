using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Emoting.Components;
using Content.Shared.Abilities.Mime;
using Content.Shared.ActionBlocker;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.EmotePanel;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.Wagging;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.EmotePanel;

/// <summary>
/// /datum/emote TG: проверки can_run_emote, выбор текста, звук с откатами и эффекты run_emote.
/// Эмоции не ванильные: чат получает готовый текст, а ванильные наборы звуков используются как библиотека.
/// </summary>
public sealed partial class ImperialEmoteSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ImperialEmoteHandSystem _handItems = default!;

    /// <summary>initial(/datum/emote::cooldown) BandaStation.</summary>
    private const float DefaultCooldown = 3f;

    /// <summary>manual_general_emote_audio_cooldown.</summary>
    private static readonly TimeSpan GeneralSoundCooldown = TimeSpan.FromSeconds(3);

    private static readonly EntProtoId MonkeyBase = "MobBaseAncestor";

    /// <summary>Фразы ванильных эмоций: такой текст сам по себе вызвал бы ванильную эмоцию.</summary>
    private readonly HashSet<string> _vanillaTriggers = new();

    public override void Initialize()
    {
        base.Initialize();
        InitializeEffects();

        SubscribeNetworkEvent<ImperialEmoteListRequest>(OnListRequest);
        SubscribeNetworkEvent<ImperialEmotePlayMessage>(OnPlay);
        SubscribeNetworkEvent<ImperialEmotePreviewMessage>(OnPreview);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildTriggers();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EmotePrototype>())
            BuildTriggers();
    }

    private void BuildTriggers()
    {
        _vanillaTriggers.Clear();
        foreach (var emote in _proto.EnumeratePrototypes<EmotePrototype>())
        {
            foreach (var trigger in emote.ChatTriggers)
                _vanillaTriggers.Add(trigger.ToLowerInvariant());
        }
    }

    #region Сеть

    /// <summary>ui_static_data панели: эмоции, которые существо вообще может выполнить.</summary>
    private void OnListRequest(ImperialEmoteListRequest msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        var list = new List<ImperialEmoteEntry>();
        foreach (var proto in _proto.EnumeratePrototypes<ImperialEmotePrototype>())
        {
            if (!CanRun(uid, proto, statusCheck: false))
                continue;

            list.Add(new ImperialEmoteEntry
            {
                Id = proto.ID,
                Hands = proto.Hands || proto.HandItem != null,
                Visible = proto.Visible,
                Audible = proto.Audible,
                Sound = HasSound(uid, proto) && ShouldPlaySound(uid, proto),
                Param = Loc.TryGetString($"imperial-emote-param-{proto.ID}", out _),
                Action = proto.IsAction,
            });
        }

        RaiseNetworkEvent(new ImperialEmoteListMessage(list), args.SenderSession);
    }

    private void OnPlay(ImperialEmotePlayMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid || !_proto.TryIndex<ImperialEmotePrototype>(msg.Id, out var proto))
            return;

        var param = msg.Param?.Trim();
        if (param is { Length: > ImperialEmotePanel.MaxTextLength })
            param = param[..ImperialEmotePanel.MaxTextLength];

        TryEmote(uid, proto, string.IsNullOrEmpty(param) ? null : param, intentional: true);
    }

    /// <summary>preview_sound: звук слышит только сам игрок.</summary>
    private void OnPreview(ImperialEmotePreviewMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid || !_proto.TryIndex<ImperialEmotePrototype>(msg.Id, out var proto))
            return;

        if (!TryGetSound(uid, proto, out var set, out var sound))
        {
            _popup.PopupEntity(Loc.GetString("imperial-emote-preview-none", ("emote", Loc.GetString(proto.NameLoc))), uid, uid);
            return;
        }

        if (set != null && proto.VanillaSound is { } vanilla && set.Sounds.TryGetValue(vanilla, out var vanillaSound))
            sound = vanillaSound;

        if (sound != null)
            _audio.PlayGlobal(sound, Filter.SinglePlayer(args.SenderSession), false, AudioParams.Default.WithVolume(-2f));
    }

    #endregion

    #region Проверки

    public bool IsKind(EntityUid uid, ImperialEmoteMob mob)
    {
        return mob switch
        {
            ImperialEmoteMob.Living => HasComp<MobStateComponent>(uid) && !HasComp<StationAiHeldComponent>(uid),
            ImperialEmoteMob.Carbon => HasComp<HumanoidProfileComponent>(uid) || IsMonkey(uid) || IsAlien(uid),
            ImperialEmoteMob.Human => HasComp<HumanoidProfileComponent>(uid),
            ImperialEmoteMob.Monkey => IsMonkey(uid),
            ImperialEmoteMob.Alien => IsAlien(uid),
            ImperialEmoteMob.Silicon => HasComp<BorgChassisComponent>(uid) || HasComp<StationAiHeldComponent>(uid),
            ImperialEmoteMob.Ai => HasComp<StationAiHeldComponent>(uid),
            _ => false,
        };
    }

    public bool IsMonkey(EntityUid uid)
    {
        if (MetaData(uid).EntityPrototype is not { } proto)
            return false;

        foreach (var (id, _) in _proto.EnumerateAllParents<EntityPrototype>(proto.ID, true))
        {
            if (id == MonkeyBase)
                return true;
        }

        return false;
    }

    /// <summary>Ксеноморф из Imperial/Xenomorph: проверка по имени, чтобы не зависеть от его кода.</summary>
    public bool IsAlien(EntityUid uid) => HasNamed(uid, "Xenomorph") || IsLarva(uid);

    public bool IsLarva(EntityUid uid) => HasNamed(uid, "XenoLarva");

    private bool HasNamed(EntityUid uid, string component)
    {
        return Factory.TryGetRegistration(component, out var reg) && HasComp(uid, reg.Type);
    }

    public bool IsMime(EntityUid uid)
    {
        return TryComp<MimePowersComponent>(uid, out var mime) && !mime.VowBroken;
    }

    /// <summary>Животные (simple/basic mob TG): живые, но не гуманоиды, обезьяны, ксеноморфы и не синтетики.</summary>
    private bool IsAnimal(EntityUid uid)
    {
        return IsKind(uid, ImperialEmoteMob.Living) && !IsKind(uid, ImperialEmoteMob.Carbon) && !IsKind(uid, ImperialEmoteMob.Silicon);
    }

    /// <summary>stat TG: 0 в сознании, 2 без сознания, 3 в критическом, 4 мёртв.</summary>
    private int GetStat(EntityUid uid)
    {
        if (_mobState.IsDead(uid))
            return 4;
        if (_mobState.IsCritical(uid))
            return 3;
        if (HasComp<SleepingComponent>(uid))
            return 2;
        return 0;
    }

    private bool HasHands(EntityUid uid)
    {
        return TryComp<HandsComponent>(uid, out var hands) && hands.Count > 0;
    }

    /// <summary>TRAIT_HANDS_BLOCKED: связан, оглушён или рук нет.</summary>
    public bool HandsBlocked(EntityUid uid)
    {
        if (!TryComp<HandsComponent>(uid, out var hands) || hands.Count == 0)
            return true;

        if (TryComp<CuffableComponent>(uid, out var cuffs) && cuffs.CuffedHandCount > 0)
            return true;

        return !_blocker.CanInteract(uid, null);
    }

    public bool CanRun(EntityUid uid, ImperialEmotePrototype proto, bool statusCheck, bool intentional = false)
    {
        if (!proto.Mobs.Any(m => IsKind(uid, m)))
            return false;

        if (proto.Species.Count > 0 &&
            (!TryComp<HumanoidProfileComponent>(uid, out var profile) || !proto.Species.Contains(profile.Species)))
            return false;

        if ((proto.Hands || proto.HandItem != null) && !HasHands(uid))
            return false;

        foreach (var requirement in proto.Requires)
        {
            if (!CheckRequirement(uid, requirement))
                return false;
        }

        if (proto.Effect == ImperialEmoteEffect.AiDisplay && !Factory.TryGetRegistration(AiDisplayComponent, out _))
            return false;

        if (!statusCheck)
            return true;

        var stat = GetStat(uid);
        if (stat > (int) proto.Stat)
        {
            if (intentional)
            {
                var key = stat switch
                {
                    4 => "imperial-emote-cant-dead",
                    3 => "imperial-emote-cant-crit",
                    _ => "imperial-emote-cant-unconscious",
                };
                _popup.PopupEntity(Loc.GetString(key), uid, uid);
            }

            return false;
        }

        if (proto.Hands && HandsBlocked(uid))
        {
            if (intentional)
                _popup.PopupEntity(Loc.GetString("imperial-emote-cant-hands"), uid, uid);
            return false;
        }

        return true;
    }

    private bool CheckRequirement(EntityUid uid, ImperialEmoteRequirement requirement)
    {
        switch (requirement)
        {
            case ImperialEmoteRequirement.CanSpeak:
                return _blocker.CanSpeak(uid) || IsMime(uid);
            case ImperialEmoteRequirement.TwoHands:
                return TryComp<HandsComponent>(uid, out var hands) && hands.Count >= 2;
            case ImperialEmoteRequirement.Glasses:
                return _inventory.TryGetSlotEntity(uid, "eyes", out var glasses) &&
                       MetaData(glasses.Value).EntityPrototype?.ID.StartsWith("ClothingEyesGlasses") == true;
            case ImperialEmoteRequirement.Tail:
                return HasComp<WaggingComponent>(uid);
            case ImperialEmoteRequirement.Wings:
                return TryComp<HumanoidProfileComponent>(uid, out var profile) && profile.Species == "Moth" ||
                       BodySounds(uid)?.Sounds.ContainsKey("Flap") == true;
            default:
                return true;
        }
    }

    #endregion

    #region Текст

    /// <summary>select_message_type и select_param: null — эмоция без текста.</summary>
    private string? SelectMessage(EntityUid uid, ImperialEmotePrototype proto, string? param)
    {
        var id = proto.ID;
        if (param != null && Loc.TryGetString($"imperial-emote-param-{id}", out var withParam, ("entity", uid), ("target", param)))
            return withParam;

        var variant = "msg";
        if (IsMime(uid) && Has("mime"))
            variant = "mime";

        if (IsAlien(uid) && !IsLarva(uid) && Has("alien"))
            variant = "alien";
        else if (IsLarva(uid) && Has("larva"))
            variant = "larva";
        else if (IsKind(uid, ImperialEmoteMob.Ai) && Has("ai"))
            variant = "ai";
        else if (IsMonkey(uid) && Has("monkey"))
            variant = "monkey";
        else if (HasComp<BorgChassisComponent>(uid) && Has("robot"))
            variant = "robot";
        else if (IsAnimal(uid) && Has("animal"))
            variant = "animal";

        return Loc.TryGetString($"imperial-emote-{variant}-{id}", out var message, ("entity", uid)) ? message : null;

        bool Has(string kind) => Loc.TryGetString($"imperial-emote-{kind}-{id}", out _);
    }

    /// <summary>Эмоция уходит в чат готовым текстом; совпадение с фразой ванильной эмоции разрываем, чтобы не сработала и она.</summary>
    private void SendChat(EntityUid uid, string message)
    {
        if (_vanillaTriggers.Contains(message.TrimEnd('.', '!', '?', '…').ToLowerInvariant()))
            message += "​";

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Emote, ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }

    #endregion

    #region Звук

    private EmoteSoundsPrototype? VocalSounds(EntityUid uid)
    {
        return TryComp<VocalComponent>(uid, out var vocal) && vocal.EmoteSounds is { } id && _proto.TryIndex(id, out var set) ? set : null;
    }

    private EmoteSoundsPrototype? BodySounds(EntityUid uid)
    {
        return TryComp<BodyEmotesComponent>(uid, out var body) && body.SoundsId is { } id && _proto.TryIndex(id, out var set) ? set : null;
    }

    /// <summary>get_sound: сначала набор звуков вида, затем свои звуки по виду и полу.</summary>
    private bool TryGetSound(EntityUid uid, ImperialEmotePrototype proto, out EmoteSoundsPrototype? set, out SoundSpecifier? sound)
    {
        set = null;
        sound = null;

        if (proto.VanillaSound is { } vanilla)
        {
            foreach (var candidate in new[] { VocalSounds(uid), BodySounds(uid) })
            {
                if (candidate == null || !candidate.Sounds.ContainsKey(vanilla))
                    continue;

                set = candidate;
                return true;
            }
        }

        TryComp<HumanoidProfileComponent>(uid, out var profile);
        if (profile != null && proto.SpeciesSounds.TryGetValue(profile.Species, out var speciesSound))
        {
            sound = speciesSound;
            return true;
        }

        // Звуки по полу есть только у гуманоидов: physique TG.
        if (profile != null && (proto.SoundMale != null || proto.SoundFemale != null))
        {
            sound = profile.Sex == Sex.Female ? proto.SoundFemale ?? proto.SoundMale : proto.SoundMale ?? proto.SoundFemale;
            return sound != null;
        }

        sound = proto.Sound;
        return sound != null;
    }

    private bool HasSound(EntityUid uid, ImperialEmotePrototype proto) => TryGetSound(uid, proto, out _, out _);

    /// <summary>should_play_sound: немота и обет мима глушат звуки голосом, но не руками.</summary>
    private bool ShouldPlaySound(EntityUid uid, ImperialEmotePrototype proto)
    {
        if (!proto.Audible || proto.Hands)
            return true;

        return !HasComp<MutedComponent>(uid) && !IsMime(uid);
    }

    /// <summary>Откаты звука TG: общий и для конкретной эмоции. Возвращает false, если эмоция сейчас не прозвучит.</summary>
    private bool TryStartSoundCooldown(ImperialEmotePanelComponent comp, ImperialEmotePrototype proto)
    {
        var now = _timing.CurTime;
        if (now < comp.NextSound || comp.NextSpecificSound.TryGetValue(proto.ID, out var next) && now < next)
            return false;

        comp.NextSound = now + GeneralSoundCooldown;
        comp.NextSpecificSound[proto.ID] = now + TimeSpan.FromSeconds(proto.SoundCooldown);
        return true;
    }

    private void PlaySound(EntityUid uid, ImperialEmotePrototype proto, EmoteSoundsPrototype? set, SoundSpecifier? sound)
    {
        if (set != null && proto.VanillaSound is { } vanilla)
        {
            _chat.TryPlayEmoteSound(uid, set, vanilla);
            return;
        }

        if (sound == null)
            return;

        var audioParams = AudioParams.Default.WithVolume(-2f);
        if (proto.Vary)
            audioParams = audioParams.WithVariation(0.125f);

        _audio.PlayPvs(sound, uid, audioParams);
    }

    #endregion

    /// <summary>emote(): откат эмоции, проверки, текст со звуком и эффект.</summary>
    public bool TryEmote(EntityUid uid, ImperialEmotePrototype proto, string? param = null, bool intentional = false, string? messageOverride = null)
    {
        var comp = EnsureComp<ImperialEmotePanelComponent>(uid);
        var now = _timing.CurTime;

        // check_cooldown: только для эмоций, которые игрок делает сам.
        if (intentional)
        {
            if (comp.Cooldowns.TryGetValue(proto.ID, out var ready) && now < ready)
            {
                if (proto.Cooldown > DefaultCooldown)
                {
                    _popup.PopupEntity(Loc.GetString("imperial-emote-cooldown",
                        ("seconds", Math.Ceiling((ready - now).TotalSeconds))), uid, uid);
                }

                OnCooldownFailed(uid, proto);
                return false;
            }

            comp.Cooldowns[proto.ID] = now + TimeSpan.FromSeconds(proto.Cooldown);
        }

        if (!CanRun(uid, proto, statusCheck: true, intentional))
            return false;

        var message = messageOverride ?? ModifyMessage(uid, proto, param, SelectMessage(uid, proto, param));
        if (message != null)
        {
            if (TryGetSound(uid, proto, out var set, out var sound) && ShouldPlaySound(uid, proto))
            {
                if (!TryStartSoundCooldown(comp, proto))
                    return false;

                PlaySound(uid, proto, set, sound);
            }

            SendChat(uid, message);
        }

        ApplyEffect(uid, proto);

        if (proto.HandItem is { } handItem)
            _handItems.GiveHandItem(uid, handItem);

        return true;
    }

    public bool TryEmote(EntityUid uid, string id, bool intentional = false)
    {
        return _proto.TryIndex<ImperialEmotePrototype>(id, out var proto) && TryEmote(uid, proto, null, intentional);
    }
}
