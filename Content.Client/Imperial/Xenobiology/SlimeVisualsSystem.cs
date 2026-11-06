using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Xenobiology;

/// <summary>
/// mob/living/basic/slime/regenerate_icons(): спрайт по цвету, стадии и смерти, прозрачность, лицо-настроение.
/// </summary>
public sealed class SlimeVisualsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SlimeComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<SlimeComponent, MobStateChangedEvent>(OnMobState);
    }

    private void OnStartup(Entity<SlimeComponent> ent, ref ComponentStartup args) => UpdateVisuals(ent);

    private void OnState(Entity<SlimeComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateVisuals(ent);

    private void OnMobState(Entity<SlimeComponent> ent, ref MobStateChangedEvent args) => UpdateVisuals(ent);

    private void UpdateVisuals(Entity<SlimeComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || !_proto.TryIndex(ent.Comp.SlimeType, out var type))
            return;

        var spriteEnt = (ent.Owner, sprite);
        var stage = ent.Comp.Stage == SlimeLifeStage.Adult ? "adult" : "baby";
        var dead = _mobState.IsDead(ent);
        string state;
        if (!dead)
            state = $"{type.Colour}-{stage}";
        else if (ent.Comp.Cut)
            state = $"{type.Colour}-cut";
        else
            state = $"{type.Colour}-{stage}-dead";

        _sprite.LayerSetRsiState(spriteEnt, SlimeVisualLayers.Base, state);
        _sprite.SetColor(spriteEnt, Color.White.WithAlpha(type.Transparent ? SlimeConstants.TransparencyAlpha : 1f));

        if (!_sprite.LayerMapTryGet(spriteEnt, SlimeVisualLayers.Mood, out var moodLayer, false))
            return;

        var showMood = !dead && ent.Comp.Mood != SlimeMood.None;
        _sprite.LayerSetVisible(spriteEnt, moodLayer, showMood);
        if (showMood)
            _sprite.LayerSetRsiState(spriteEnt, moodLayer, $"aslime-{ent.Comp.Mood.ToString().ToLowerInvariant()}");
    }
}
