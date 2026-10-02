using Content.Shared.Imperial.Fission;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Imperial.Fission;

/// <summary>
/// Камера стержня: состояние корпуса, индикаторы, створки, анимации подъёма/опускания (flick из SS13)
/// и слой отрисовки — поднятая камера перекрывает мобов.
/// </summary>
public sealed class FissionChamberVisualizerSystem : VisualizerSystem<FissionChamberVisualsComponent>
{
    [Dependency] private readonly AnimationPlayerSystem _player = default!;

    private const string AnimationKey = "fission-chamber";

    private static readonly Animation UpAnimation = Flick(FissionChamberLayers.Base, "chamber_up_anim", 0.7f);
    private static readonly Animation DownAnimation = Flick(FissionChamberLayers.Base, "chamber_down_anim", 1.24f);
    private static readonly Animation OpeningAnimation = Flick(FissionChamberLayers.Door, "doors_opening", 0.5f);
    private static readonly Animation ClosingAnimation = Flick(FissionChamberLayers.Door, "doors_closing", 0.7f);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FissionChamberVisualsComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private static Animation Flick(FissionChamberLayers layer, string state, float length)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = layer,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId(state), 0f) },
                },
            },
        };
    }

    private void OnAnimationCompleted(Entity<FissionChamberVisualsComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != AnimationKey || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        Apply(ent, sprite, false);
    }

    protected override void OnAppearanceChange(EntityUid uid, FissionChamberVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        Apply((uid, component), args.Sprite, true);
    }

    private void Apply(Entity<FissionChamberVisualsComponent> ent, SpriteComponent sprite, bool animate)
    {
        var uid = ent.Owner;
        if (!AppearanceSystem.TryGetData<FissionChamberState>(uid, FissionChamberVisuals.State, out var state))
            return;

        AppearanceSystem.TryGetData<FissionChamberStatus>(uid, FissionChamberVisuals.Status, out var status);
        if (!AppearanceSystem.TryGetData<int>(uid, FissionChamberVisuals.Rod, out var rod))
            rod = -1;
        if (!AppearanceSystem.TryGetData<int>(uid, FissionChamberVisuals.Durability, out var durability))
            durability = -1;
        AppearanceSystem.TryGetData<bool>(uid, FissionChamberVisuals.Welded, out var welded);
        AppearanceSystem.TryGetData<bool>(uid, FissionChamberVisuals.Panel, out var panel);

        var spriteEnt = (uid, sprite);
        var baseState = state switch
        {
            FissionChamberState.Up => "chamber_up",
            FissionChamberState.Open => panel ? "chamber_maint" : "chamber_open",
            FissionChamberState.OverloadIdle => "chamber_overload",
            _ => "chamber_down",
        };
        SpriteSystem.LayerSetRsiState(spriteEnt, FissionChamberLayers.Base, baseState);

        SpriteSystem.LayerSetVisible(spriteEnt, FissionChamberLayers.Welded, welded);

        SpriteSystem.LayerSetVisible(spriteEnt, FissionChamberLayers.Status, status != FissionChamberStatus.None);
        if (status != FissionChamberStatus.None)
            SpriteSystem.LayerSetRsiState(spriteEnt, FissionChamberLayers.Status, status.ToString().ToLowerInvariant());

        var category = rod >= 0 ? ((FissionRodCategory) rod).ToString().ToLowerInvariant() : null;
        var down = state == FissionChamberState.Down;
        SpriteSystem.LayerSetVisible(spriteEnt, FissionChamberLayers.Display, down && category != null);
        if (down && category != null)
            SpriteSystem.LayerSetRsiState(spriteEnt, FissionChamberLayers.Display, $"display_{category}");

        SpriteSystem.LayerSetVisible(spriteEnt, FissionChamberLayers.Durability, down && durability >= 0);
        if (down && durability >= 0)
            SpriteSystem.LayerSetRsiState(spriteEnt, FissionChamberLayers.Durability, $"dur_{durability}");

        var open = state == FissionChamberState.Open;
        SpriteSystem.LayerSetVisible(spriteEnt, FissionChamberLayers.RodOverlay, open && category != null);
        if (open && category != null)
            SpriteSystem.LayerSetRsiState(spriteEnt, FissionChamberLayers.RodOverlay, $"{category}_overlay");

        SpriteSystem.LayerSetVisible(spriteEnt, FissionChamberLayers.Door, open);
        if (open)
            SpriteSystem.LayerSetRsiState(spriteEnt, FissionChamberLayers.Door, "door_open");

        var raised = state is FissionChamberState.Up or FissionChamberState.Open;
        SpriteSystem.SetDrawDepth(spriteEnt, (int) (raised ? DrawDepth.OverMobs : DrawDepth.FloorObjects));

        var last = ent.Comp.LastState;
        ent.Comp.LastState = state;
        if (!animate || last == null || last == state)
            return;

        Animation? animation = null;
        if (last is FissionChamberState.Down or FissionChamberState.OverloadIdle && state == FissionChamberState.Up)
            animation = UpAnimation;
        else if (last == FissionChamberState.Up && state is FissionChamberState.Down or FissionChamberState.OverloadIdle)
            animation = DownAnimation;
        else if (last == FissionChamberState.Up && state == FissionChamberState.Open)
            animation = OpeningAnimation;
        else if (last == FissionChamberState.Open && state == FissionChamberState.Up)
        {
            SpriteSystem.LayerSetVisible(spriteEnt, FissionChamberLayers.Door, true);
            animation = ClosingAnimation;
        }

        if (animation == null)
            return;

        _player.Stop(uid, AnimationKey);
        _player.Play(uid, animation, AnimationKey);
    }
}
