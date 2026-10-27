using System.Numerics;
using Content.Server.Power.Components;
using Content.Server.Radio;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ZLevels;

/// <summary>Невидимый ретранслятор рации на этаже (только сервер).</summary>
[RegisterComponent]
public sealed partial class ZRadioRelayComponent : Component;

/// <summary>
/// Рация между этажами. В SS14 обычные каналы работают только в пределах одной карты и только при сервере
/// телекоммуникаций на ней, а этажи — разные карты. На каждом этаже стопки стоит невидимый ретранслятор:
/// он работает как сервер с каналами всех включённых серверов стопки (сломали сервер внизу — замолчали все этажи)
/// и пересылает услышанное на своём этаже приёмникам на остальных этажах.
/// </summary>
public sealed class ZRadioSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private readonly Dictionary<EntityUid, EntityUid> _relays = new();
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZRadioRelayComponent, RadioReceiveEvent>(OnRelayReceive);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + TimeSpan.FromSeconds(1);
        UpdateRelays();
    }

    /// <summary>Ретранслятор на каждом этаже многоэтажной стопки; каналы — от включённых настоящих серверов стопки.</summary>
    private void UpdateRelays()
    {
        var stacks = new Dictionary<string, List<EntityUid>>();
        var maps = EntityQueryEnumerator<ZLevelMapComponent>();
        while (maps.MoveNext(out var mapUid, out var z))
        {
            if (string.IsNullOrEmpty(z.Stack) || z.Above == null && z.Below == null)
                continue;
            if (!stacks.TryGetValue(z.Stack, out var list))
                stacks[z.Stack] = list = new();
            list.Add(mapUid);
        }

        var wanted = new HashSet<EntityUid>();
        foreach (var (_, levels) in stacks)
        {
            var mapIds = new HashSet<MapId>();
            foreach (var level in levels)
                mapIds.Add(Comp<MapComponent>(level).MapId);

            var channels = new HashSet<ProtoId<RadioChannelPrototype>>();
            var servers = EntityQueryEnumerator<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
            while (servers.MoveNext(out var server, out _, out var keys, out var power, out var xform))
            {
                if (!HasComp<ZRadioRelayComponent>(server) && power.Powered && mapIds.Contains(xform.MapID))
                    channels.UnionWith(keys.Channels);
            }

            foreach (var level in levels)
            {
                wanted.Add(level);
                var relay = EnsureRelay(level);
                var holder = Comp<EncryptionKeyHolderComponent>(relay);
                holder.Channels.Clear();
                holder.Channels.UnionWith(channels);
            }
        }

        foreach (var (map, relay) in new Dictionary<EntityUid, EntityUid>(_relays))
        {
            if (wanted.Contains(map) && !TerminatingOrDeleted(relay))
                continue;
            if (!TerminatingOrDeleted(relay))
                QueueDel(relay);
            _relays.Remove(map);
        }
    }

    private EntityUid EnsureRelay(EntityUid map)
    {
        if (_relays.TryGetValue(map, out var existing) && !TerminatingOrDeleted(existing))
            return existing;
        var relay = Spawn(null, new MapCoordinates(Vector2.Zero, Comp<MapComponent>(map).MapId));
        _metaData.SetEntityName(relay, "z-level radio relay");
        AddComp<ZRadioRelayComponent>(relay);
        AddComp<TelecomServerComponent>(relay);
        AddComp<EncryptionKeyHolderComponent>(relay);
        var power = AddComp<ApcPowerReceiverComponent>(relay);
        power.NeedsPower = false;
        var radio = AddComp<ActiveRadioComponent>(relay);
        radio.ReceiveAllChannels = true;
        _relays[map] = relay;
        return relay;
    }

    /// <summary>Услышанное на своём этаже — приёмникам на остальных этажах стопки (дальние каналы и так слышны всем).</summary>
    private void OnRelayReceive(Entity<ZRadioRelayComponent> ent, ref RadioReceiveEvent args)
    {
        if (args.Channel.LongRange)
            return;
        var relayXform = Transform(ent);
        if (relayXform.MapUid is not { } map || !TryComp<ZLevelMapComponent>(map, out var z) || string.IsNullOrEmpty(z.Stack))
            return;
        var stackMaps = new HashSet<MapId>();
        foreach (var level in _z.GetStack(z.Stack))
            stackMaps.Add(Comp<MapComponent>(level).MapId);

        var channel = args.Channel;
        var ev = args;
        var receivers = new List<EntityUid>();
        var query = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (query.MoveNext(out var receiver, out var radio, out var xform))
        {
            if (HasComp<ZRadioRelayComponent>(receiver) || radio.GlobalReceive
                || xform.MapID == relayXform.MapID || !stackMaps.Contains(xform.MapID))
                continue;
            if (!radio.ReceiveAllChannels
                && (!radio.Channels.Contains(channel.ID)
                    || TryComp<IntercomComponent>(receiver, out var intercom) && !intercom.SupportedChannels.Contains(channel.ID)))
                continue;
            receivers.Add(receiver);
        }

        foreach (var receiver in receivers)
        {
            var attempt = new RadioReceiveAttemptEvent(channel, ev.RadioSource, receiver);
            RaiseLocalEvent(ref attempt);
            RaiseLocalEvent(receiver, ref attempt);
            if (attempt.Cancelled)
                continue;
            RaiseLocalEvent(receiver, ref ev);
        }
    }
}
