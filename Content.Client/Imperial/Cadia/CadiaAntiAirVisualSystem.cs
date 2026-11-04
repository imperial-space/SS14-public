using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>Башня зенитной позиции плавно поворачивается к углу, который присылает сервер.</summary>
public sealed class CadiaAntiAirVisualSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string TurretLayer = "turret";
    private readonly Dictionary<EntityUid, Angle> _shown = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CadiaAntiAirComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var aa, out var sprite))
        {
            var shown = _shown.GetValueOrDefault(uid, aa.Aim);
            // Сервер шлёт угол 10 раз в секунду — догоняем его плавно, без рывков.
            var diff = Angle.ShortestDistance(shown, aa.Aim);
            var step = Math.Min(1f, frameTime * 12f);
            shown += new Angle(diff.Theta * step);
            _shown[uid] = shown;
            _sprite.LayerSetRotation((uid, sprite), TurretLayer, shown);
        }
    }
}
