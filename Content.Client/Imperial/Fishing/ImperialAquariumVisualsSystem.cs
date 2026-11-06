using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Fishing;

/// <summary>
/// Внешний вид аквариума (update_overlays, aquarium_content): стекло воды или воздуха, трещины, панель,
/// а внутри — декор и плавающие рыбы (swim_animation); мёртвые рыбы и рыбы без воды лежат на дне.
/// </summary>
public sealed class ImperialAquariumVisualsSystem : VisualizerSystem<ImperialAquariumComponent>
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly ResPath TanksRsi = new("/Textures/Imperial/Fishing/tanks.rsi");
    private const string ContentPrefix = "imperial-aquarium-content-";

    /// <summary>2 децисекунды на пиксель в TG.</summary>
    private const float SwimSpeed = 5f;

    private sealed class Visual
    {
        public EntityUid Entity;
        public string Key = string.Empty;
        public Vector2 Position;
        public Vector2 Origin;
        public Vector2 Target;
        public int MovesLeft;
        public bool FacingRight;
        public int Width;
        public int Height;
        public bool IsFish;
    }

    private readonly Dictionary<EntityUid, List<Visual>> _visuals = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialAquariumComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ImperialAquariumComponent> ent, ref ComponentShutdown args)
    {
        _visuals.Remove(ent);
    }

    protected override void OnAppearanceChange(EntityUid uid, ImperialAquariumComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        AppearanceSystem.TryGetData<bool>(uid, ImperialAquariumVisuals.PanelOpen, out var panel, args.Component);
        AppearanceSystem.TryGetData<bool>(uid, ImperialAquariumVisuals.Air, out var air, args.Component);
        AppearanceSystem.TryGetData<bool>(uid, ImperialAquariumVisuals.Broken, out var broken, args.Component);

        var baseState = component.BaseState;
        if (component.Portable)
        {
            if (SpriteSystem.LayerMapTryGet(sprite, "panel", out var panelLayer, false))
                SpriteSystem.LayerSetRsiState(sprite, panelLayer, baseState + "_panel" + (panel ? "_open" : string.Empty));
            if (SpriteSystem.LayerMapTryGet(sprite, "glass", out var glass, false))
                SpriteSystem.LayerSetRsiState(sprite, glass, baseState + (air ? "_air" : "_water"));
            return;
        }

        if (SpriteSystem.LayerMapTryGet(sprite, "panel", out var structurePanel, false))
            SpriteSystem.LayerSetVisible(sprite, structurePanel, panel);
        if (SpriteSystem.LayerMapTryGet(sprite, "glass", out var structureGlass, false))
            SpriteSystem.LayerSetRsiState(sprite, structureGlass, baseState + "_glass_" + (air ? "air" : "water") + (broken ? "_broken" : string.Empty));
        if (SpriteSystem.LayerMapTryGet(sprite, "cracks", out var cracks, false))
            SpriteSystem.LayerSetVisible(sprite, cracks, broken);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<ImperialAquariumComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var aquarium, out var sprite))
        {
            if (!_container.TryGetContainer(uid, ImperialAquariumComponent.ContainerId, out var container))
                continue;

            var ent = (uid, sprite);
            var visuals = _visuals.GetValueOrDefault(uid);
            var contents = container.ContainedEntities;
            if (visuals == null || visuals.Count != contents.Count || visuals.Any(v => !contents.Contains(v.Entity)))
                visuals = Rebuild((uid, aquarium, sprite), contents);

            var air = aquarium.Fluid == ImperialFishFluid.Air;
            foreach (var visual in visuals)
            {
                if (!SpriteSystem.LayerMapTryGet(ent, visual.Key, out var layer, false))
                    continue;

                if (visual.IsFish)
                    Swim(aquarium, visual, air || CompOrNull<ImperialFishComponent>(visual.Entity)?.Dead != false, frameTime);

                SpriteSystem.LayerSetOffset(ent, layer, visual.Position / 32f);
                SpriteSystem.LayerSetScale(ent, layer, new Vector2(visual.FacingRight ? -1 : 1, 1));
            }
        }
    }

    /// <summary>Пересоздаёт слои содержимого между корпусом и стеклом.</summary>
    private List<Visual> Rebuild(Entity<ImperialAquariumComponent, SpriteComponent> aquarium, IReadOnlyList<EntityUid> contents)
    {
        var (uid, comp, spriteComp) = aquarium;
        Entity<SpriteComponent?> sprite = (uid, spriteComp);
        if (_visuals.TryGetValue(uid, out var old))
        {
            foreach (var visual in old)
                SpriteSystem.RemoveLayer(sprite, visual.Key, false);
        }

        var result = new List<Visual>();
        var ordered = contents
            .Select(e => (Entity: e, Order: GetOrder(e)))
            .OrderBy(e => e.Order)
            .ToList();

        var index = 0;
        foreach (var (content, order) in ordered)
        {
            if (!TryGetSpecifier(comp, content, out var specifier, out var width, out var height))
                continue;

            if (!SpriteSystem.LayerMapTryGet(sprite, "glass", out var glassIndex, false))
                glassIndex = spriteComp.AllLayers.Count();

            var key = ContentPrefix + index++;
            var layer = SpriteSystem.AddLayer(sprite, specifier, glassIndex);
            SpriteSystem.LayerMapSet(sprite, key, layer);

            var visual = new Visual
            {
                Entity = content,
                Key = key,
                Width = width,
                Height = height,
                IsFish = order == 2,
            };

            if (visual.IsFish)
            {
                // randomize_aquarium_position().
                var (min, max) = Zone(comp, visual);
                visual.Position = visual.Origin = new Vector2(_random.Next(min.X, max.X + 1), _random.Next(min.Y, max.Y + 1));
                visual.Target = visual.Position;
            }

            result.Add(visual);
        }

        _visuals[uid] = result;
        return result;
    }

    /// <summary>AQUARIUM_LAYER_MODE: за стеклом (песок), дно, рыбы, верх.</summary>
    private int GetOrder(EntityUid content)
    {
        if (HasComp<ImperialFishComponent>(content))
            return 2;

        return CompOrNull<ImperialAquariumPropComponent>(content)?.Layer switch
        {
            ImperialAquariumLayer.BehindGlass => 0,
            ImperialAquariumLayer.Top => 3,
            ImperialAquariumLayer.Auto => 2,
            _ => 1,
        };
    }

    private bool TryGetSpecifier(ImperialAquariumComponent aquarium, EntityUid content, out SpriteSpecifier specifier, out int width, out int height)
    {
        specifier = SpriteSpecifier.Invalid;
        width = 0;
        height = 0;

        if (TryComp<ImperialAquariumPropComponent>(content, out var prop))
        {
            if (prop.Sprite != null && TryComp<SpriteComponent>(content, out var propSprite)
                && propSprite.AllLayers.FirstOrDefault() is { } propLayer && propLayer.RsiState.Name is { } propState)
            {
                specifier = new SpriteSpecifier.Rsi(new ResPath("/Textures/" + prop.Sprite), propState);
                return true;
            }

            if (prop.State == null)
                return false;

            specifier = new SpriteSpecifier.Rsi(TanksRsi, prop.State + (aquarium.Portable ? "_fish_tank" : string.Empty));
            return true;
        }

        if (!TryComp<ImperialFishComponent>(content, out var fish) || !TryComp<SpriteComponent>(content, out var fishSprite))
            return false;

        width = fish.SpriteWidth;
        height = fish.SpriteHeight;
        var rsi = fishSprite.BaseRSI;
        if (rsi == null)
            return false;

        // dedicated_in_aquarium_icon_state или «<состояние>_small».
        var baseState = fishSprite.AllLayers.FirstOrDefault()?.RsiState.Name;
        var candidates = new[] { fish.AquariumState, baseState + "_small", baseState };
        foreach (var state in candidates)
        {
            if (state == null || !rsi.TryGetState(state, out _))
                continue;

            specifier = new SpriteSpecifier.Rsi(rsi.Path, state);
            return true;
        }

        return false;
    }

    private static (Vector2i Min, Vector2i Max) Zone(ImperialAquariumComponent aquarium, Visual visual)
    {
        var avgW = visual.Width / 2;
        var avgH = visual.Height / 2;
        var min = new Vector2i(aquarium.ZoneMin.X + avgW - 16, aquarium.ZoneMin.Y + avgH - 16);
        var max = new Vector2i(aquarium.ZoneMax.X - avgW - 16, aquarium.ZoneMax.Y - avgH - 16);
        return (min, Vector2i.ComponentMax(min, max));
    }

    /// <summary>swim_animation() / dead_animation().</summary>
    private void Swim(ImperialAquariumComponent aquarium, Visual visual, bool dead, float frameTime)
    {
        var (min, max) = Zone(aquarium, visual);
        if (dead)
        {
            var bottom = new Vector2(visual.Position.X, min.Y);
            visual.Position = MoveTowards(visual.Position, bottom, SwimSpeed * 4 * frameTime);
            visual.MovesLeft = 0;
            return;
        }

        if (visual.Position == visual.Target)
        {
            if (visual.MovesLeft <= 0)
                visual.MovesLeft = _random.Next(3, 6);

            visual.MovesLeft--;
            visual.Target = visual.MovesLeft == 0
                ? visual.Origin
                : new Vector2(_random.Next(min.X, max.X + 1), _random.Next(min.Y, max.Y + 1));

            // По умолчанию спрайт смотрит влево.
            if (visual.Target.X != visual.Position.X)
                visual.FacingRight = visual.Target.X > visual.Position.X;
        }

        visual.Position = MoveTowards(visual.Position, visual.Target, SwimSpeed * frameTime);
    }

    private static Vector2 MoveTowards(Vector2 from, Vector2 to, float step)
    {
        var delta = to - from;
        var length = delta.Length();
        return length <= step ? to : from + delta / length * step;
    }
}
