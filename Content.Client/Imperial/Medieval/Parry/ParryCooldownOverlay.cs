using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Client.Input;
using Robust.Client.Player;
using Content.Shared.MeleeParry.Components;
using Content.Shared.Hands.EntitySystems;
using System.Numerics;

namespace Content.Client.Imperial.Medieval.MeleeParry;

public sealed class ParryCooldownOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly IGameTiming _timing;
    private readonly IInputManager _input;
    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly SharedHandsSystem _hands;

    public ParryCooldownOverlay(IGameTiming timing, IInputManager input, IEntityManager entMan, IPlayerManager player, SharedHandsSystem hands)
    {
        _timing = timing;
        _input = input;
        _entMan = entMan;
        _player = player;
        _hands = hands;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var playerEnt = _player.LocalEntity;
        if (playerEnt == null || !_entMan.TryGetComponent<MeleeParryStorageComponent>(playerEnt, out var parryStorage))
            return;

        if (_timing.CurTime >= parryStorage.NextParryTime)
            return;

        var cooldownDuration = TimeSpan.FromSeconds(parryStorage.CooldownParry);
        var startTime = parryStorage.NextParryTime - cooldownDuration;
        var elapsed = _timing.CurTime - startTime;

        var progress = Math.Clamp(elapsed.TotalSeconds / cooldownDuration.TotalSeconds, 0.0, 1.0);

        var handle = args.ScreenHandle;
        var center = _input.MouseScreenPosition.Position;

        // Параметры кольца
        var innerR = 40f;
        var outerR = 50f;
        var segments = 64;

        float fullCircle = (float)Math.PI * 2;
        float startAngle = -(float)Math.PI / 2 + (fullCircle * (float)progress);
        float sweepAngle = fullCircle * (1.0f - (float)progress);

        DrawPie(handle, center, innerR, outerR, startAngle, sweepAngle, segments);
    }

    private void DrawPie(DrawingHandleScreen handle, Vector2 center, float innerRadius, float outerRadius, float startAngle, float sweepAngle, int segments)
    {
        if (sweepAngle <= 0) return;

        var vertices = new Vector2[(segments + 1) * 2];

        for (var i = 0; i <= segments; i++)
        {
            var angle = startAngle + (sweepAngle * (i / (float)segments));
            var direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));

            // Внешняя точка
            vertices[i * 2] = center + direction * outerRadius;
            // Внутренняя точка
            vertices[i * 2 + 1] = center + direction * innerRadius;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, vertices, Color.White.WithAlpha(0.2f));
    }
}
