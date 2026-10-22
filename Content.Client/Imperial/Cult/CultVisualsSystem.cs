using System.Linq;
using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Визуал культа: руны (do_invoke_glow, fail_invoke), rune_spawn, лучи, нимбы, кровавая метка,
/// кровавые искры, видения Апокалипсиса, окраска и иконки культистов.
/// </summary>
public sealed class CultVisualsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private static readonly ResPath EffectsRsi = new("/Textures/Imperial/BloodCult/effects.rsi");
    private static readonly ResPath HaloRsi = new("/Textures/Imperial/BloodCult/halo.rsi");
    private static readonly ResPath TargetRsi = new("/Textures/Imperial/BloodCult/target.rsi");
    private static readonly ResPath ConstructsRsi = new("/Textures/Imperial/BloodCult/constructs.rsi");
    private static readonly ProtoId<FactionIconPrototype> CultistIcon = "CultistFaction";
    private static readonly ProtoId<FactionIconPrototype> MasterIcon = "CultMasterFaction";

    private const string HaloKey = "cult-halo";
    private const string MarkKey = "cult-blood-mark";
    private const string SparkleKey = "cult-sparkles";
    private const string VisionKey = "cult-apoc-vision";
    private const string PulseKey = "cult-pulse";

    private readonly Dictionary<EntityUid, (TimeSpan Start, int Counter)> _glows = new();
    private readonly Dictionary<EntityUid, (TimeSpan Start, int Counter)> _fails = new();
    private readonly Dictionary<EntityUid, TimeSpan> _spawnStarts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CultVisualsComponent, AppearanceChangeEvent>(OnAppearance);
        SubscribeLocalEvent<CultRuneSpawnEffectComponent, ComponentStartup>(OnSpawnEffectStartup);
        SubscribeLocalEvent<CultRuneComponent, ComponentStartup>(OnRuneStartup);
        SubscribeLocalEvent<CultTintComponent, AfterAutoHandleStateEvent>(OnTintState);
        SubscribeLocalEvent<CultTintComponent, ComponentStartup>(OnTintStartup);
        SubscribeLocalEvent<CultTintComponent, ComponentShutdown>(OnTintShutdown);
        SubscribeLocalEvent<CultistComponent, AfterAutoHandleStateEvent>(OnCultistState);
        SubscribeLocalEvent<CultistComponent, ComponentStartup>(OnCultistStartup);
        SubscribeLocalEvent<CultistComponent, ComponentShutdown>(OnCultistShutdown);
        SubscribeLocalEvent<CultistComponent, GetStatusIconsEvent>(OnStatusIcons);
        SubscribeLocalEvent<CultBloodTargetComponent, ComponentShutdown>((uid, _, _) => RemoveLayer(uid, MarkKey));
        SubscribeLocalEvent<CultHorrorMarkComponent, ComponentShutdown>((uid, _, _) => RemoveLayer(uid, SparkleKey));
        SubscribeLocalEvent<CultApocalypseVisionComponent, ComponentShutdown>((uid, _, _) => { RemoveLayer(uid, VisionKey); RemoveLayer(uid, SparkleKey); });
        SubscribeLocalEvent<CultPulseSelectedComponent, ComponentShutdown>((uid, _, _) => RemoveLayer(uid, PulseKey));
    }

    #region Внешний вид

    private void OnAppearance(Entity<CultVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;
        var spriteEnt = (ent.Owner, sprite);

        if (_appearance.TryGetData<string>(ent, CultVisuals.State, out var state, args.Component))
            _sprite.LayerSetRsiState(spriteEnt, CultVisualLayers.Base, state);

        if (_appearance.TryGetData<bool>(ent, CultVisuals.Anchored, out var anchored, args.Component) && ent.Comp.AnchorState != null)
            _sprite.LayerSetRsiState(spriteEnt, CultVisualLayers.Base, anchored ? ent.Comp.AnchorState : ent.Comp.AnchorState + "_off");

        if (_appearance.TryGetData<Color>(ent, CultVisuals.Color, out var color, args.Component))
            _sprite.LayerSetColor(spriteEnt, CultVisualLayers.Base, color);

        var concealed = _appearance.TryGetData<bool>(ent, CultVisuals.Concealed, out var c, args.Component) && c;
        if (ent.Comp.ConcealedRsi != null)
        {
            // Рунная дверь притворяется обычным техническим шлюзом.
            var rsi = concealed ? new ResPath(ent.Comp.ConcealedRsi) : new ResPath("/Textures/Imperial/cult/air.rsi");
            for (var i = 0; i < sprite.AllLayers.Count(); i++)
                _sprite.LayerSetRsi(spriteEnt, i, rsi);
        }
        else
        {
            var baseColor = sprite.Color;
            _sprite.SetColor(spriteEnt, baseColor.WithAlpha(concealed ? 100f / 255f : 1f));
        }

        if (_appearance.TryGetData<int>(ent, CultVisuals.InvokeGlow, out var glow, args.Component)
            && (!_glows.TryGetValue(ent, out var g) || g.Counter != glow) && glow > 0)
            _glows[ent] = (_timing.CurTime, glow);

        if (_appearance.TryGetData<int>(ent, CultVisuals.FailFlash, out var fail, args.Component)
            && (!_fails.TryGetValue(ent, out var f) || f.Counter != fail) && fail > 0)
            _fails[ent] = (_timing.CurTime, fail);
    }

    /// <summary>Цвет руны из прототипа сразу, до прихода данных внешнего вида.</summary>
    private void OnRuneStartup(Entity<CultRuneComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || _appearance.TryGetData<Color>(ent, CultVisuals.Color, out _))
            return;
        _sprite.LayerSetColor((ent.Owner, sprite), CultVisualLayers.Base, ent.Comp.RuneColor);
    }

    private void OnSpawnEffectStartup(Entity<CultRuneSpawnEffectComponent> ent, ref ComponentStartup args)
    {
        _spawnStarts[ent] = _timing.CurTime;
    }

    private void OnTintStartup(Entity<CultTintComponent> ent, ref ComponentStartup args) => ApplyTint(ent);
    private void OnTintState(Entity<CultTintComponent> ent, ref AfterAutoHandleStateEvent args) => ApplyTint(ent);

    private void ApplyTint(Entity<CultTintComponent> ent)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), ent.Comp.Color);
    }

    private void OnTintShutdown(Entity<CultTintComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), Color.White);
    }

    #endregion

    #region Нимб и иконки

    private void OnCultistStartup(Entity<CultistComponent> ent, ref ComponentStartup args) => UpdateHalo(ent);
    private void OnCultistState(Entity<CultistComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateHalo(ent);

    private void OnCultistShutdown(Entity<CultistComponent> ent, ref ComponentShutdown args) => RemoveLayer(ent, HaloKey);

    /// <summary>status_effect/cult_halo: halo1..6 над головой.</summary>
    private void UpdateHalo(Entity<CultistComponent> ent)
    {
        if (!ent.Comp.Halo)
        {
            RemoveLayer(ent, HaloKey);
            return;
        }
        // halo.dmi 32x64 в BYOND привязан к низу тайла: поднимаем на полтайла.
        EnsureLayer(ent, HaloKey, HaloRsi, $"halo{Math.Clamp(ent.Comp.HaloState, 1, 6)}", new Vector2(0f, 0.5f), unshaded: true);
    }

    /// <summary>antag_hud "cult"/"cultmaster": видят культисты и призраки.</summary>
    private void OnStatusIcons(Entity<CultistComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.Leader ? MasterIcon : CultistIcon, out var icon))
            return;
        args.StatusIcons.Add(icon);
    }

    #endregion

    #region Слои

    private void EnsureLayer(EntityUid uid, string key, ResPath rsi, string state, Vector2 offset, bool unshaded = false)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        var ent = (uid, sprite);
        if (!_sprite.LayerMapTryGet(ent, key, out var index, false))
        {
            index = _sprite.AddLayer(ent, new SpriteSpecifier.Rsi(rsi, state));
            _sprite.LayerMapSet(ent, key, index);
            if (unshaded)
                sprite.LayerSetShader(index, "unshaded");
        }
        _sprite.LayerSetRsi(ent, index, rsi, state);
        _sprite.LayerSetOffset(ent, index, offset);
        _sprite.LayerSetVisible(ent, index, true);
    }

    private void SetLayerAlpha(EntityUid uid, string key, float alpha)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || !_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
            return;
        _sprite.LayerSetColor((uid, sprite), index, Color.White.WithAlpha(alpha));
        _sprite.LayerSetVisible((uid, sprite), index, alpha > 0.01f);
    }

    private void RemoveLayer(EntityUid uid, string key)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || !_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
            return;
        _sprite.RemoveLayer((uid, sprite), index);
        _sprite.LayerMapRemove((uid, sprite), key);
    }

    #endregion

    private bool LocalSeesCult()
    {
        if (_player.LocalEntity is not { } local)
            return false;
        return HasComp<CultistComponent>(local) || HasComp<GhostComponent>(local);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = _timing.CurTime;

        // do_invoke_glow: x2 и прозрачность за 0.5 с.
        foreach (var (uid, glow) in _glows.ToArray())
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
            {
                _glows.Remove(uid);
                continue;
            }
            var t = (float) (now - glow.Start).TotalSeconds / 0.5f;
            if (t >= 1f)
            {
                _sprite.SetScale((uid, sprite), Vector2.One);
                _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(1f));
                _glows.Remove(uid);
                continue;
            }
            _sprite.SetScale((uid, sprite), Vector2.One * (1f + t));
            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(1f - t));
        }

        // fail_invoke: красная вспышка, возврат цвета за 0.5 с.
        foreach (var (uid, fail) in _fails.ToArray())
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
            {
                _fails.Remove(uid);
                continue;
            }
            var baseColor = _appearance.TryGetData<Color>(uid, CultVisuals.Color, out var c) ? c : Color.White;
            var t = (float) (now - fail.Start).TotalSeconds / 0.5f;
            if (t >= 1f)
            {
                _sprite.LayerSetColor((uid, sprite), CultVisualLayers.Base, baseColor);
                _fails.Remove(uid);
                continue;
            }
            _sprite.LayerSetColor((uid, sprite), CultVisualLayers.Base, Color.InterpolateBetween(Color.Red, baseColor, t));
        }

        // rune_spawn: проявление (bounce) и сжатие x2 → x1 с поворотом.
        var spawnQuery = EntityQueryEnumerator<CultRuneSpawnEffectComponent, SpriteComponent>();
        while (spawnQuery.MoveNext(out var uid, out var effect, out var sprite))
        {
            if (!_spawnStarts.TryGetValue(uid, out var start))
                _spawnStarts[uid] = start = now;
            var p = Math.Clamp((float) (now - start).TotalSeconds / MathF.Max(effect.Duration, 0.01f), 0f, 1f);
            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(BounceEase(p)));
            _sprite.SetScale((uid, sprite), Vector2.One * (2f - p));
            _sprite.SetRotation((uid, sprite), Angle.FromDegrees(effect.Turn * (1f - p)));
        }

        // Лучи (atom/Beam): растягиваем спрайт на длину.
        var beamQuery = EntityQueryEnumerator<CultBeamComponent, SpriteComponent>();
        while (beamQuery.MoveNext(out var uid, out var beam, out var sprite))
        {
            _sprite.SetScale((uid, sprite), new Vector2(1f, beam.Length));
        }

        var sees = LocalSeesCult();
        var local = _player.LocalEntity;

        // Кровавая метка (cult_team/blood_target_image).
        var markQuery = EntityQueryEnumerator<CultBloodTargetComponent>();
        while (markQuery.MoveNext(out var uid, out _))
        {
            if (sees)
                EnsureLayer(uid, MarkKey, TargetRsi, "glow", Vector2.Zero, unshaded: true);
            else
                RemoveLayer(uid, MarkKey);
        }

        // Импульс мастера: выбранная цель.
        var pulseQuery = EntityQueryEnumerator<CultPulseSelectedComponent>();
        while (pulseQuery.MoveNext(out var uid, out var pulse))
        {
            if (local == pulse.Master)
                EnsureLayer(uid, PulseKey, TargetRsi, "glow", Vector2.Zero, unshaded: true);
            else
                RemoveLayer(uid, PulseKey);
        }

        // Галлюцинации: кровавые искры видят только культисты.
        var horrorQuery = EntityQueryEnumerator<CultHorrorMarkComponent>();
        while (horrorQuery.MoveNext(out var uid, out _))
        {
            if (sees)
                EnsureLayer(uid, SparkleKey, EffectsRsi, "bloodsparkles", Vector2.Zero, unshaded: true);
            else
                RemoveLayer(uid, SparkleKey);
        }

        // Апокалипсис: image_handler — 35 с образ, 2.5 с затухание, 2.5 с проявление.
        var localCult = local != null && HasComp<CultistComponent>(local);
        var visionQuery = EntityQueryEnumerator<CultApocalypseVisionComponent>();
        while (visionQuery.MoveNext(out var uid, out var vision))
        {
            if (uid == local)
                continue;
            var cycle = (float) ((vision.End - now).TotalSeconds % 60.0);
            var alpha = 1f;
            var phase = 60f - cycle;
            if (phase < 2.5f)
                alpha = 1f - phase / 2.5f;
            else if (phase < 3.5f)
                alpha = 0f;
            else if (phase < 6f)
                alpha = (phase - 3.5f) / 2.5f;

            if (localCult)
            {
                RemoveLayer(uid, VisionKey);
                if (!HasComp<CultistComponent>(uid))
                {
                    EnsureLayer(uid, SparkleKey, EffectsRsi, "bloodsparkles", Vector2.Zero, unshaded: true);
                    SetLayerAlpha(uid, SparkleKey, alpha);
                }
                continue;
            }

            EnsureLayer(uid, VisionKey, ConstructsRsi, vision.Construct ?? "cultist", Vector2.Zero);
            SetLayerAlpha(uid, VisionKey, alpha);
        }
    }

    private static float BounceEase(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1)
            return n1 * t * t;
        if (t < 2f / d1)
            return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5f / d1)
            return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }
}

/// <summary>illusion/mock_as(): appearance = original.appearance.</summary>
public sealed class CultIllusionVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CultIllusionComponent, AfterAutoHandleStateEvent>((uid, comp, _) => Copy(uid, comp));
        SubscribeLocalEvent<CultIllusionComponent, ComponentStartup>((uid, comp, _) => Copy(uid, comp));
    }

    private void Copy(EntityUid uid, CultIllusionComponent comp)
    {
        if (comp.Source is not { } source || !TryComp<SpriteComponent>(source, out var src) || !TryComp<SpriteComponent>(uid, out var dst))
            return;
        _sprite.CopySprite((source, src), (uid, dst));
    }
}
