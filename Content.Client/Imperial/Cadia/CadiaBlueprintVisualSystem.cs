using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.Imperial.Cadia;

/// <summary>Голограмма чертежа: силуэт будущей постройки голубым светом, с мерцанием.</summary>
public sealed class CadiaBlueprintVisualSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string HoloLayer = "holo";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaBlueprintComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CadiaBlueprintComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<CadiaBlueprintComponent> ent, ref ComponentStartup args) => Apply(ent);

    private void OnState(Entity<CadiaBlueprintComponent> ent, ref AfterAutoHandleStateEvent args) => Apply(ent);

    private void Apply(Entity<CadiaBlueprintComponent> ent)
    {
        if (string.IsNullOrEmpty(ent.Comp.Prototype) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;
        if (!_sprite.LayerMapTryGet((ent, sprite), HoloLayer, out var index, false))
            return;
        var texture = _sprite.GetPrototypeIcon(ent.Comp.Prototype).Default;
        _sprite.LayerSetTexture((ent, sprite), index, texture);
        // Позиции 4×4 рисуются со смещением, как настоящие.
        _sprite.LayerSetOffset((ent, sprite), index, ent.Comp.Kind == CadiaBlueprintKind.Hydra ? new Vector2(0.5f, 0.5f) : Vector2.Zero);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var t = (float)_timing.RealTime.TotalSeconds;
        var query = EntityQueryEnumerator<CadiaBlueprintComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            var flicker = 0.45f + 0.12f * MathF.Sin(t * 6f + uid.Id) + (MathF.Sin(t * 31f + uid.Id * 3) > 0.97f ? -0.25f : 0f);
            _sprite.SetColor((uid, sprite), new Color(0.35f, 0.85f, 1f, flicker));
        }
    }
}
