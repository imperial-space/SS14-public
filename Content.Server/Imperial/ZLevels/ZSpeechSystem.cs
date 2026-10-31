using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Imperial.ZLevels;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Речь сквозь этажи: сказанное вслух рядом с проёмом (открытое пространство, пустота, решётка, стекло) слышно
/// на соседнем этаже рядом с тем же местом — с пометкой «сверху» или «снизу». Шёпот через перекрытие не слышен.
/// </summary>
public sealed class ZSpeechSystem : EntitySystem
{
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    /// <summary>Как далеко от проёма ищется источник и как далеко от него слышно на соседнем этаже.</summary>
    private const int OpenRange = 2;
    private const float HearRange = 8f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
    }

    private void OnSpoke(EntitySpokeEvent args)
    {
        if (args.ObfuscatedMessage != null || TerminatingOrDeleted(args.Source))
            return;
        var coords = _xform.GetMapCoordinates(args.Source);
        if (!_map.TryGetMap(coords.MapId, out var map) || !HasComp<ZLevelMapComponent>(map))
            return;

        var nameEv = new TransformSpeakerNameEvent(args.Source, Name(args.Source));
        RaiseLocalEvent(args.Source, nameEv);
        var name = FormattedMessage.EscapeText(nameEv.VoiceName);
        var text = FormattedMessage.EscapeText(args.Message);

        foreach (var up in new[] { true, false })
        {
            if (_z.GetStep(coords, up) is not { } target)
                continue;
            // Звук идёт через проём того этажа, сквозь который проходит: вверх — над говорящим, вниз — у говорящего.
            if (!HasOpenNear(up ? target : coords))
                continue;
            // Для слушающих наверху говорят снизу, и наоборот.
            var wrapped = Loc.GetString(up ? "zlevel-speech-from-below" : "zlevel-speech-from-above", ("name", name), ("message", text));
            foreach (var session in _players.Sessions)
            {
                if (session.AttachedEntity is not { } listener)
                    continue;
                var pos = _xform.GetMapCoordinates(listener);
                if (pos.MapId != target.MapId || (pos.Position - target.Position).Length() > HearRange)
                    continue;
                _chat.ChatMessageToOne(ChatChannel.Local, args.Message, wrapped, args.Source, false, session.Channel);
            }
        }
    }

    private bool HasOpenNear(MapCoordinates coords)
    {
        for (var dx = -OpenRange; dx <= OpenRange; dx++)
        for (var dy = -OpenRange; dy <= OpenRange; dy++)
        {
            if (_z.IsTransparent(new MapCoordinates(coords.Position + new Vector2(dx, dy), coords.MapId)))
                return true;
        }
        return false;
    }
}
