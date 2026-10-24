using System.Linq;
using Content.Server.Screens.Components;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Popups;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.StationAi;

/// <summary>Объявления голосом VOX (ai_say.dm) и картинки ИИ на экранах станции (status_display/ai).</summary>
public sealed partial class ImperialStationAiSystem
{
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;

    /// <summary>GLOB.vox_sounds: слова, для которых есть звук.</summary>
    private HashSet<string>? _voxWords;

    /// <summary>static announcing_vox: откат общий на всех ИИ.</summary>
    private TimeSpan _nextVox;

    private void InitializeVox()
    {
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiAnnouncementActionEvent>(OnAnnouncementAction);
        SubscribeLocalEvent<ImperialStationAiComponent, ImperialAiStatusDisplayActionEvent>(OnStatusDisplayAction);
        SubscribeNetworkEvent<ImperialAiAnnouncementEvent>(OnAnnouncement);
        SubscribeNetworkEvent<ImperialAiStatusDisplayChoiceEvent>(OnStatusDisplayChoice);
    }

    private HashSet<string> VoxWords()
    {
        if (_voxWords != null)
            return _voxWords;

        _voxWords = new HashSet<string>();
        foreach (var path in _resources.ContentFindFiles(new ResPath(ImperialAiVox.Directory)))
        {
            if (path.Extension != "ogg")
                continue;

            var name = path.FilenameWithoutExtension;
            _voxWords.Add(name switch
            {
                "_comma" => ",",
                "_period" => ".",
                _ => name,
            });
        }

        return _voxWords;
    }

    private void OnAnnouncementAction(Entity<ImperialStationAiComponent> ent, ref ImperialAiAnnouncementActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var wait = (float) Math.Max(0, (_nextVox - _timing.CurTime).TotalSeconds);
        RaiseNetworkEvent(new ImperialAiAnnouncementOpenEvent(ent.Comp.LastAnnouncement, wait), actor.PlayerSession);
    }

    /// <summary>announcement(): до 30 слов, все должны быть в словаре VOX, слышат все на карте ИИ.</summary>
    private void OnAnnouncement(ImperialAiAnnouncementEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } ai)
            Announce(ai, ev.Message);
    }

    public bool Announce(EntityUid ai, string text)
    {
        if (!TryComp<ImperialStationAiComponent>(ai, out var comp))
            return false;

        var message = text.Trim();
        if (message.Length == 0)
            return false;

        if (_timing.CurTime < _nextVox)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-vox-wait",
                ("seconds", (int) Math.Ceiling((_nextVox - _timing.CurTime).TotalSeconds))), ai, ai);
            return false;
        }

        comp.LastAnnouncement = message;
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length > 0)
            .Take(ImperialAiVox.MaxWords)
            .ToList();

        var vocabulary = VoxWords();
        var incorrect = words.Where(w => !vocabulary.Contains(w)).Distinct().ToList();
        if (incorrect.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ai-vox-unknown", ("words", string.Join(", ", incorrect))), ai, ai,
                PopupType.MediumCaution);
            return false;
        }

        if (words.Count == 0 || !_ai.TryGetCore(ai, out var core))
            return false;

        _nextVox = _timing.CurTime + ImperialAiVox.Cooldown;
        var map = Transform(core.Owner).MapID;
        var filter = Filter.Empty().AddWhere(session => session.AttachedEntity is { } attached && Transform(attached).MapID == map);

        // minor_announce(): текст объявления и голос по словам.
        _chat.DispatchFilteredAnnouncement(filter, char.ToUpperInvariant(message[0]) + message.Substring(1), ai,
            Loc.GetString("imperial-ai-vox-sender", ("name", Name(ai))), playSound: false);
        RaiseNetworkEvent(new ImperialAiVoxPlayEvent(words), filter);
        Log.Info($"{ToPrettyString(ai)} объявил голосом VOX: {message}");
        return true;
    }

    private void OnStatusDisplayAction(Entity<ImperialStationAiComponent> ent, ref ImperialAiStatusDisplayActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        string? current = null;
        var query = EntityQueryEnumerator<ImperialAiDisplayComponent>();
        if (query.MoveNext(out _, out var display))
            current = display.State;

        RaiseNetworkEvent(new ImperialAiStatusDisplayOpenEvent(current), actor.PlayerSession);
    }

    /// <summary>emotion_display: все экраны станции ИИ показывают выбранную картинку.</summary>
    private void OnStatusDisplayChoice(ImperialAiStatusDisplayChoiceEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } ai && HasComp<ImperialStationAiComponent>(ai))
            SetDisplays(ai, ev.State);
    }

    public void SetDisplays(EntityUid ai, string? state)
    {
        if (state != null && ImperialAiVox.DisplayOptions.All(o => o.State != state))
            return;

        var station = CoreStation(ai);
        var query = EntityQueryEnumerator<ScreenComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (station != null && _station.GetOwningStation(uid, xform) != station)
                continue;

            if (state == null)
            {
                RemComp<ImperialAiDisplayComponent>(uid);
                continue;
            }

            var display = EnsureComp<ImperialAiDisplayComponent>(uid);
            display.State = state;
            Dirty(uid, display);
        }
    }
}
