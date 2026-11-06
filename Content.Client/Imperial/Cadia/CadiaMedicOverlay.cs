using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>Медик видит раненых своей стороны: красный крест, а за краем экрана — стрелка у края.</summary>
public sealed class CadiaMedicOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private static readonly Color Cross = Color.FromHex("#ff3a2a");
    private const float Arm = 7f;
    private const float Width = 2.5f;
    private const float EdgeInset = 24f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaMedicOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entMan.HasComponent<CadiaMedicSightComponent>(local) ||
            !_entMan.TryGetComponent<CadiaFactionMemberComponent>(local, out var mine))
            return;

        var xforms = _entMan.System<SharedTransformSystem>();
        var mapId = _entMan.GetComponent<TransformComponent>(local).MapID;
        var bounds = new Box2(args.ViewportBounds.Left + EdgeInset, args.ViewportBounds.Top + EdgeInset,
            args.ViewportBounds.Right - EdgeInset, args.ViewportBounds.Bottom - EdgeInset);
        var handle = args.ScreenHandle;

        var query = _entMan.EntityQueryEnumerator<MobStateComponent, CadiaFactionMemberComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var state, out var member, out var xform))
        {
            if (uid == local || state.CurrentState != MobState.Critical || member.Faction != mine.Faction || xform.MapID != mapId)
                continue;
            var screen = _eye.WorldToScreen(xforms.GetWorldPosition(xform));
            var clamped = new Vector2(Math.Clamp(screen.X, bounds.Left, bounds.Right), Math.Clamp(screen.Y, bounds.Bottom, bounds.Top));
            handle.DrawRect(UIBox2.FromDimensions(clamped - new Vector2(Arm, Width), new Vector2(Arm * 2, Width * 2)), Cross);
            handle.DrawRect(UIBox2.FromDimensions(clamped - new Vector2(Width, Arm), new Vector2(Width * 2, Arm * 2)), Cross);
        }
    }
}
