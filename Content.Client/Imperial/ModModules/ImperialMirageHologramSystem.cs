using Content.Shared.Imperial.ModModules;
using Robust.Client.GameObjects;
using Robust.Shared.Serialization.Manager;

namespace Content.Client.Imperial.ModModules;

/// <summary>
/// Голограмма гранаты-миража: копирует спрайт бросившего (как морф) и красит его в голубой.
/// </summary>
public sealed class ImperialMirageHologramSystem : EntitySystem
{
    [Dependency] private readonly ISerializationManager _serMan = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly Color HologramColor = Color.FromHex("#7FDBFFAA");

    private readonly HashSet<EntityUid> _copied = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialMirageHologramComponent, ComponentShutdown>((uid, _, _) => _copied.Remove(uid));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<ImperialMirageHologramComponent>();
        while (query.MoveNext(out var uid, out var holo))
        {
            if (_copied.Contains(uid) || holo.Source is not { } source || !TryComp<SpriteComponent>(source, out var sourceSprite))
                continue;

            if (!TryComp<SpriteComponent>(uid, out var dest))
                continue;

            _copied.Add(uid);
            _sprite.CopySprite((source, sourceSprite), (uid, dest));
            _sprite.SetColor((uid, dest), HologramColor);
        }
    }
}
