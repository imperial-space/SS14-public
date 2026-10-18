using System.Linq;
using System.Globalization;
using System.Numerics;
using Content.Shared.Imperial.Manufacturing;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Manufacturing;

/// <summary>
/// update_overlays() машин производственной линии: стрелки входа и выхода на соседних клетках,
/// стрелки маршрутизатора и flick_overlay_view() печати и сортировки.
/// </summary>
public sealed class ImperialManufacturingVisualizerSystem : VisualizerSystem<ImperialManufacturingComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string IoRsi = "/Textures/Imperial/Manufacturing/io.rsi";
    private const string MachinesRsi = "/Textures/Imperial/Manufacturing/machines.rsi";
    private const string IoKey = "imperial-manufacturing-io-";
    private const string RouterKey = "imperial-manufacturing-router-";
    private const string FlickKey = "imperial-manufacturing-flick";

    /// <summary>Когда убрать мигающее наложение.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _flickEnds = new();

    /// <summary>Последний показанный flick, чтобы не повторять его при каждом обновлении внешнего вида.</summary>
    private readonly Dictionary<EntityUid, string> _lastFlick = new();

    protected override void OnAppearanceChange(EntityUid uid, ImperialManufacturingComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);

        if (AppearanceSystem.TryGetData<string>(uid, ImperialManufacturingVisuals.State, out var state, args.Component)
            && SpriteSystem.LayerMapTryGet(sprite, "base", out var baseLayer, false))
            SpriteSystem.LayerSetRsiState(sprite, baseLayer, state);

        for (var i = 0; i < 4; i++)
        {
            SpriteSystem.RemoveLayer(sprite, IoKey + i, false);
            SpriteSystem.RemoveLayer(sprite, RouterKey + i, false);
        }

        if (AppearanceSystem.TryGetData<string>(uid, ImperialManufacturingVisuals.Io, out var io, args.Component))
        {
            var i = 0;
            foreach (var entry in io.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var byond) || i >= 4)
                    continue;

                var layer = SpriteSystem.AddLayer(sprite, new PrototypeLayerData
                {
                    RsiPath = IoRsi,
                    State = $"unres_{byond}",
                    Shader = "unshaded",
                    Color = Color.FromHex(parts[1]),
                    Offset = FromByond(byond).ToVec(),
                }, null);
                SpriteSystem.LayerMapSet(sprite, IoKey + i, layer);
                i++;
            }
        }

        if (AppearanceSystem.TryGetData<string>(uid, ImperialManufacturingVisuals.Router, out var router, args.Component))
        {
            var i = 0;
            foreach (var entry in router.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var byond) || i >= 4)
                    continue;

                var layer = SpriteSystem.AddLayer(sprite, new PrototypeLayerData
                {
                    RsiPath = MachinesRsi,
                    State = $"splitter_{parts[1]}",
                }, null);
                SpriteSystem.LayerSetDirOffset(sprite, layer, DirOffset(uid, FromByond(byond)));
                SpriteSystem.LayerMapSet(sprite, RouterKey + i, layer);
                i++;
            }
        }

        if (AppearanceSystem.TryGetData<string>(uid, ImperialManufacturingVisuals.Flick, out var flick, args.Component)
            && flick != string.Empty
            && (!_lastFlick.TryGetValue(uid, out var last) || last != flick))
        {
            _lastFlick[uid] = flick;
            var parts = flick.Split('|');
            if (parts.Length == 4
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                && int.TryParse(parts[2], out var byond))
            {
                SpriteSystem.RemoveLayer(sprite, FlickKey, false);
                var layer = SpriteSystem.AddLayer(sprite, new PrototypeLayerData
                {
                    RsiPath = MachinesRsi,
                    State = parts[0],
                }, null);
                SpriteSystem.LayerSetDirOffset(sprite, layer, DirOffset(uid, FromByond(byond)));
                SpriteSystem.LayerMapSet(sprite, FlickKey, layer);
                _flickEnds[uid] = _timing.CurTime + TimeSpan.FromSeconds(seconds);
            }
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_flickEnds.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (uid, end) in _flickEnds.ToArray())
        {
            if (now < end)
                continue;

            _flickEnds.Remove(uid);
            if (TryComp<SpriteComponent>(uid, out var sprite))
                SpriteSystem.RemoveLayer((uid, sprite), FlickKey, false);
        }
    }

    private static Direction FromByond(int dir) => dir switch
    {
        1 => Direction.North,
        4 => Direction.East,
        8 => Direction.West,
        _ => Direction.South,
    };

    /// <summary>Наложение с dir = direction: смещение относительно направления самой машины.</summary>
    private SpriteComponent.DirectionOffset DirOffset(EntityUid uid, Direction target)
    {
        var own = Transform(uid).LocalRotation.GetCardinalDir();
        var ccwSteps = ((((int) target - (int) own) / 2) % 4 + 4) % 4;
        return ccwSteps switch
        {
            1 => SpriteComponent.DirectionOffset.CounterClockwise,
            2 => SpriteComponent.DirectionOffset.Flip,
            3 => SpriteComponent.DirectionOffset.Clockwise,
            _ => SpriteComponent.DirectionOffset.None,
        };
    }
}
