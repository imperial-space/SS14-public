using System.Numerics;
using System.Text;
using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Decals;
using Content.Server.Doors.Systems;
using Content.Server.Imperial.Heretic.Rule;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Antag;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Decals;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Follower.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Muting;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using CancellationTokenSource = System.Threading.CancellationTokenSource;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Жертвы и вознесение.
/// </summary>
public sealed partial class HereticSystem
{
    public void AddSacrifice(EntityUid bodyUid, int amount = 1)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp))
            return;
        comp.SacrificeCount += amount;
        Dirty(bodyUid, comp);
    }

    public int GetHighValueSacrificeCount(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.CurrentEntity == null)
            return 0;
        if (!TryComp<HereticComponent>(mind.CurrentEntity.Value, out var heretic))
            return 0;
        return heretic.HighValueSacrificeCount;
    }

    public void AddHighValueSacrifice(EntityUid bodyUid, int amount = 1)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp))
            return;
        comp.HighValueSacrificeCount += amount;
        Dirty(bodyUid, comp);
    }

    public int GetSummonCount(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.CurrentEntity == null)
            return 0;
        if (!TryComp<HereticComponent>(mind.CurrentEntity.Value, out var heretic))
            return 0;
        return heretic.SummonCount;
    }

    public void AddSummon(EntityUid bodyUid, int amount = 1)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp))
            return;
        comp.SummonCount += amount;
        Dirty(bodyUid, comp);
    }

    public void AddAscensionCorpse(EntityUid bodyUid)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp))
            return;
        comp.AscensionCorpsesQualified++;
        Dirty(bodyUid, comp);
        TryTriggerAscension(bodyUid);
    }

    public void TryTriggerAscension(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp))
            return;
        if (comp.AscensionTriggered)
            return;

        comp.AscensionTriggered = true;

        // Все пути: эффект возвышения срабатывает сразу, без action-кнопки
        var ascendedEv = new HereticPathAscendedEvent(uid, comp);
        RaiseLocalEvent(ref ascendedEv);

        _popup.PopupEntity(Loc.GetString("heretic-ascended"), uid, uid, PopupType.LargeCaution);

        var ascendSound = comp.CurrentPath switch
        {
            HereticPath.Ash => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_ash.ogg",
            HereticPath.Lock => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_knock.ogg",
            HereticPath.Flesh => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_flesh.ogg",
            HereticPath.Void => "/Audio/Imperial/heretic/ascend_void.ogg",
            HereticPath.Blade => "/Audio/Imperial/heretic/sound_music_antag_heretic_ascend_blade.ogg",
            HereticPath.Rust => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_rust.ogg",
            HereticPath.Moon => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_moon.ogg",
            _ => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_heretic_gain.ogg",
        };
        _audio.PlayGlobal(new SoundPathSpecifier(ascendSound), Filter.Broadcast(), false);

        // Full heal on ascension
        _damageSystem.SetAllDamage(uid, 0);

        // Passive ascension bonus: 50% brute and burn damage resistance
        var buff = EnsureComp<DamageProtectionBuffComponent>(uid);
        buff.Modifiers["HereticAscension"] = new ProtoId<DamageModifierSetPrototype>("HereticAscended");
        Dirty(uid, buff);

        // Path-specific transformation popup
        var transformMsg = comp.CurrentPath switch
        {
            HereticPath.Ash => "heretic-ascended-ash",
            HereticPath.Flesh => "heretic-ascended-flesh",
            HereticPath.Void => "heretic-ascended-void",
            HereticPath.Lock => "heretic-ascended-lock",
            HereticPath.Blade => "heretic-ascended-blade",
            HereticPath.Rust => "heretic-ascended-rust",
            HereticPath.Moon => "heretic-ascended-moon",
            _ => null
        };
        if (transformMsg != null)
            _popup.PopupEntity(Loc.GetString(transformMsg), uid, uid, PopupType.LargeCaution);

        var ascensionChatMsg = comp.CurrentPath switch
        {
            HereticPath.Ash => "heretic-ascension-message-ash",
            HereticPath.Flesh => "heretic-ascension-message-flesh",
            HereticPath.Void => "heretic-ascension-message-void",
            HereticPath.Lock => "heretic-ascension-message-lock",
            HereticPath.Blade => "heretic-ascension-message-blade",
            HereticPath.Rust => "heretic-ascension-message-rust",
            HereticPath.Moon => "heretic-ascension-message-moon",
            HereticPath.Cosmos => "heretic-ascension-message-cosmos",
            _ => "heretic-ascension-message-general"
        };
        SendHereticMessage(uid, Loc.GetString(ascensionChatMsg));

        // Global music broadcast
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/heretic/heretic_music.ogg"), Filter.Broadcast(), true, AudioParams.Default.WithVolume(-10f));

        // CentComm announcement
        var hereticName = MetaData(uid).EntityName;
        var (ccKey, ccColor) = comp.CurrentPath switch
        {
            HereticPath.Ash => ("heretic-ascension-cc-ash", Color.FromHex("#FF8C42")),
            HereticPath.Flesh => ("heretic-ascension-cc-flesh", Color.FromHex("#E53935")),
            HereticPath.Void => ("heretic-ascension-cc-void", Color.FromHex("#8B6FFF")),
            HereticPath.Lock => ("heretic-ascension-cc-lock", Color.FromHex("#FFD700")),
            HereticPath.Blade => ("heretic-ascension-cc-blade", Color.FromHex("#B0BEC5")),
            HereticPath.Rust => ("heretic-ascension-cc-rust", Color.FromHex("#D2691E")),
            HereticPath.Moon => ("heretic-ascension-cc-moon", Color.FromHex("#90CAF9")),
            HereticPath.Cosmos => ("heretic-ascension-cc-cosmos", Color.FromHex("#00E5FF")),
            _ => ("heretic-ascension-cc-announcement", Color.Gold)
        };
        _chatServer.DispatchGlobalAnnouncement(
            Loc.GetString(ccKey, ("name", hereticName)),
            playSound: false,
            colorOverride: ccColor);

        SetPassiveLevel(uid, comp, 3);

        Dirty(uid, comp);

        SendInfoBuiState(uid, comp);

        RaiseLocalEvent(new HereticAscendedEvent(uid));
    }

}
