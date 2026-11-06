using Content.Shared.Imperial.Cadia;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Видимая завеса пилонов: пока идёт подготовка, над каждым пилоном мерцает зелёное поле. С началом обстрела флот
/// Абаддона перегружает сеть пилонов, и поля гаснут.
/// </summary>
public sealed class CadiaPylonVeilFieldSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CadiaPylonVeilSystem _veil = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const string FieldPrototype = "CadiaPylonVeilField";
    private static readonly TimeSpan CheckRate = TimeSpan.FromSeconds(2);

    private readonly Dictionary<EntityUid, EntityUid> _fields = new();
    private TimeSpan _next;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + CheckRate;

        var alive = new HashSet<EntityUid>();
        if (_veil.Active())
        {
            var pylons = EntityQueryEnumerator<CadiaPylonComponent, TransformComponent>();
            while (pylons.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapUid == null)
                    continue;
                alive.Add(uid);
                if (!_fields.TryGetValue(uid, out var field) || TerminatingOrDeleted(field))
                    _fields[uid] = Spawn(FieldPrototype, _xform.GetMapCoordinates(uid, xform));
            }
        }

        foreach (var (pylon, field) in new Dictionary<EntityUid, EntityUid>(_fields))
        {
            if (alive.Contains(pylon))
                continue;
            if (!TerminatingOrDeleted(field))
                QueueDel(field);
            _fields.Remove(pylon);
        }
    }
}
