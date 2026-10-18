using System.Numerics;
using Content.Shared.Imperial.Cargo;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cargo;

/// <summary>
/// Капсула снабжения TG: begin_launch() — падение под углом со шлейфом из 13 клубов дыма и свечением двигателя,
/// end_launch() — посадка, обломки, угасание свечения и дыма.
/// </summary>
public sealed class ImperialSupplyPodVisualizerSystem : VisualizerSystem<ImperialSupplyPodComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string LandingRsi = "/Textures/Imperial/Cargo/landing.rsi";
    private const int SmokeParts = 13;

    /// <summary>SUPPLYPOD_X_OFFSET у спрайта 64×64: центр капсулы на полклетки выше клетки.</summary>
    private static readonly Vector2 BaseOffset = new(0, 0.5f);

    protected override void OnAppearanceChange(EntityUid uid, ImperialSupplyPodComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        if (AppearanceSystem.TryGetData<ImperialSupplyPodState>(uid, ImperialSupplyPodVisuals.State, out var state, args.Component)
            && SpriteSystem.LayerMapTryGet(sprite, "base", out var baseLayer, false))
        {
            var suffix = component.Bluespace ? "_bluespace" : string.Empty;
            SpriteSystem.LayerSetRsiState(sprite, baseLayer, (state == ImperialSupplyPodState.Open ? "open" : "closed") + suffix);
        }

        AppearanceSystem.TryGetData<bool>(uid, ImperialSupplyPodVisuals.Rubble, out var rubble, args.Component);
        foreach (var key in new[] { "rubble_bg", "rubble_fg" })
        {
            if (SpriteSystem.LayerMapTryGet(sprite, key, out var layer, false))
                SpriteSystem.LayerSetVisible(sprite, layer, rubble);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialSupplyPodComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var pod, out var sprite))
        {
            var sinceLanding = (float) (now - pod.LandTime).TotalSeconds;
            // После посадки дым и свечение гаснут за ~3.5 с, дальше анимировать нечего.
            if (sinceLanding > 4f)
                continue;

            Animate((uid, sprite), pod, sinceLanding);
        }
    }

    private void Animate(Entity<SpriteComponent> sprite, ImperialSupplyPodComponent pod, float sinceLanding)
    {
        var ent = (sprite.Owner, sprite.Comp);
        var falling = Math.Max(pod.Falling, 0.01f);

        // Положение капсулы на краю круга радиусом 13 клеток и поворот носом к месту посадки.
        var angle = MathHelper.DegreesToRadians(pod.Angle);
        var start = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * SmokeParts;
        var rotation = MathF.Atan2(start.X, start.Y);
        var end = new Vector2(MathF.Sin(rotation) * 20f / 32f, -MathF.Abs(MathF.Sin(rotation)) * 4f / 32f);

        var progress = Math.Clamp(1f + sinceLanding / falling, 0f, 1f);
        var landed = sinceLanding >= 0;
        var offset = landed ? Vector2.Zero : Vector2.Lerp(start, end, progress);
        var layerRotation = landed ? Angle.Zero : new Angle(-rotation);

        foreach (var key in new[] { "base", "glow" })
        {
            if (!SpriteSystem.LayerMapTryGet(ent, key, out var layer, false))
                continue;
            SpriteSystem.LayerSetOffset(ent, layer, BaseOffset + offset);
            SpriteSystem.LayerSetRotation(ent, layer, layerRotation);
        }

        // end_glow(): свечение гаснет за min(delays[POD_OPENING], 2.5 с).
        if (SpriteSystem.LayerMapTryGet(ent, "glow", out var glow, false))
        {
            var fade = Math.Min(pod.Opening, 2.5f);
            var alpha = landed ? Math.Clamp(1f - sinceLanding / fade, 0f, 1f) : 1f;
            SpriteSystem.LayerSetColor(ent, glow, Color.White.WithAlpha(alpha));
            SpriteSystem.LayerSetVisible(ent, glow, alpha > 0);
        }

        EnsureSmoke(ent);
        for (var i = 1; i <= SmokeParts; i++)
        {
            if (!SpriteSystem.LayerMapTryGet(ent, $"smoke{i}", out var layer, false))
                continue;

            // Клуб i появляется, когда капсула пролетает мимо него, и сразу тускнее на 20/255 за номер.
            var appearAt = -falling + falling / SmokeParts * (SmokeParts - i);
            var alpha = sinceLanding < appearAt ? 0f : Math.Max(0, 255 - i * 20) / 255f;
            if (landed)
                alpha *= Math.Clamp(1f - sinceLanding / 2f, 0f, 1f);

            SpriteSystem.LayerSetOffset(ent, layer, new Vector2(MathF.Sin(rotation) * i, MathF.Abs(MathF.Cos(rotation)) * i));
            SpriteSystem.LayerSetRotation(ent, layer, new Angle(-rotation));
            SpriteSystem.LayerSetColor(ent, layer, Color.White.WithAlpha(alpha));
            SpriteSystem.LayerSetVisible(ent, layer, alpha > 0);
        }
    }

    private void EnsureSmoke(Entity<SpriteComponent?> ent)
    {
        if (SpriteSystem.LayerMapTryGet(ent, "smoke1", out _, false))
            return;

        for (var i = 1; i <= SmokeParts; i++)
        {
            var layer = SpriteSystem.AddLayer(ent, new PrototypeLayerData
            {
                RsiPath = LandingRsi,
                State = i == 1 ? "smoke_start" : "smoke",
                Visible = false,
            }, null);
            SpriteSystem.LayerMapSet(ent, $"smoke{i}", layer);
        }
    }
}

/// <summary>pod_landingzone: метка поворачивается на 90°, а внутренний ползунок сжимается к прилёту капсулы.</summary>
public sealed class ImperialPodLandingZoneVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialPodLandingZoneComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var zone, out var sprite))
        {
            var total = (float) (zone.Arrival - zone.Start).TotalSeconds;
            var progress = total <= 0 ? 1f : Math.Clamp((float) (now - zone.Start).TotalSeconds / total, 0f, 1f);
            var ent = (uid, sprite);

            if (_sprite.LayerMapTryGet(ent, "lz", out var lz, false))
                _sprite.LayerSetRotation(ent, lz, Angle.FromDegrees(-90 * progress));

            if (_sprite.LayerMapTryGet(ent, "slider", out var slider, false))
                _sprite.LayerSetScale(ent, slider, Vector2.One * MathHelper.Lerp(1.5f, 0.01f, progress));
        }
    }
}

/// <summary>update_overlays() маяка: оранжевый — связан, жёлтый — готов, зелёный — капсула летит.</summary>
public sealed class ImperialSupplyPodBeaconVisualizerSystem : VisualizerSystem<ImperialSupplyPodBeaconComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialSupplyPodBeaconComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !SpriteSystem.LayerMapTryGet((uid, args.Sprite), "light", out var layer, false))
            return;

        AppearanceSystem.TryGetData<string>(uid, ImperialSupplyPodBeaconVisuals.Light, out var light, args.Component);
        SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, !string.IsNullOrEmpty(light));
        if (!string.IsNullOrEmpty(light))
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), layer, light);
    }
}
