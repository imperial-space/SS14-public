using Content.Server.Imperial.Xenobiology.Components;
using Content.Server.Jittering;
using Content.Shared.Examine;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Xenobiology.Systems;

/// <summary>
/// Система дробилки ксено-слаймов.
///
/// Алгоритм:
///   1. Клик по машине → включить/выключить.
///   2. Каждые ScanInterval секунд ищет мёртвых/критических слаймов в радиусе.
///   3. Найденные слаймы добавляются в очередь Processing (uid → оставшееся время).
///      При постановке в очередь играет звук дробления.
///   4. Таймер каждого слайма убывает каждый кадр.
///   5. Когда таймер достигает 0 — слайм удаляется, появляется экстракт.
/// </summary>
public sealed class XenoSlimeCrusherSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup     = default!;
    [Dependency] private readonly SharedAudioSystem  _audio      = default!;
    [Dependency] private readonly JitteringSystem    _jitter     = default!;
    [Dependency] private readonly AppearanceSystem   _appearance = default!;

    // Карта цвет → id прототипа экстракта (порядок совпадает с XenoSlimeColor enum)
    private static readonly string[] ExtractProtos =
    {
        "XenoSlimeExtractGrey",         //  0 Grey
        "XenoSlimeExtractOrange",       //  1 Orange
        "XenoSlimeExtractPurple",       //  2 Purple
        "XenoSlimeExtractBlue",         //  3 Blue
        "XenoSlimeExtractMetal",        //  4 Metal
        "XenoSlimeExtractYellow",       //  5 Yellow
        "XenoSlimeExtractDarkPurple",   //  6 DarkPurple
        "XenoSlimeExtractDarkBlue",     //  7 DarkBlue
        "XenoSlimeExtractSilver",       //  8 Silver
        "XenoSlimeExtractBluespace",    //  9 Bluespace
        "XenoSlimeExtractSepia",        // 10 Sepia
        "XenoSlimeExtractCerulean",     // 11 Cerulean
        "XenoSlimeExtractPyrite",       // 12 Pyrite
        "XenoSlimeExtractGreen",        // 13 Green
        "XenoSlimeExtractRed",          // 14 Red
        "XenoSlimeExtractPink",         // 15 Pink
        "XenoSlimeExtractGold",         // 16 Gold
        "XenoSlimeExtractOil",          // 17 Oil
        "XenoSlimeExtractBlack",        // 18 Black
        "XenoSlimeExtractLightPink",    // 19 LightPink
        "XenoSlimeExtractAdamantine",   // 20 Adamantine
        "XenoSlimeExtractRainbow",      // 21 Rainbow
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenoSlimeCrusherComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<XenoSlimeCrusherComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<XenoSlimeCrusherComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUse(EntityUid uid, XenoSlimeCrusherComponent comp, UseInHandEvent args)
    {
        Toggle(uid, comp);
        args.Handled = true;
    }

    private void OnActivate(EntityUid uid, XenoSlimeCrusherComponent comp, ActivateInWorldEvent args)
    {
        Toggle(uid, comp);
        args.Handled = true;
    }

    private void Toggle(EntityUid uid, XenoSlimeCrusherComponent comp)
    {
        comp.Active = !comp.Active;
        if (!comp.Active)
        {
            comp.Processing.Clear();
            comp.ScanTimer = 0f;
            RemComp<JitteringComponent>(uid);
        }
        _appearance.SetData(uid, XenoSlimeGrinderVisuals.State,
            comp.Active ? XenoSlimeGrinderState.On : XenoSlimeGrinderState.Off);
    }

    private void OnExamined(EntityUid uid, XenoSlimeCrusherComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (comp.Active)
        {
            var count = comp.Processing.Count;
            args.PushMarkup($"[color=green]Дробилка включена[/color]. В очереди: [color=yellow]{count}[/color] слаймов.");
        }
        else
        {
            args.PushMarkup("[color=red]Дробилка выключена.[/color]");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<XenoSlimeCrusherComponent>();
        while (query.MoveNext(out var uid, out var crusher))
        {
            if (!crusher.Active)
                continue;

            // ── 1. Периодический скан новых слаймов ───────────────────────
            crusher.ScanTimer -= frameTime;
            if (crusher.ScanTimer <= 0f)
            {
                crusher.ScanTimer = crusher.ScanInterval;
                ScanForSlimes(uid, crusher);
            }

            // ── 2. Обработка очереди ──────────────────────────────────────
            var toFinish = new List<EntityUid>();

            foreach (var (slime, timeLeft) in crusher.Processing)
            {
                if (!Exists(slime))
                {
                    toFinish.Add(slime);
                    continue;
                }

                crusher.Processing[slime] = timeLeft - frameTime;

                if (crusher.Processing[slime] <= 0f)
                    toFinish.Add(slime);
            }

            foreach (var slime in toFinish)
            {
                if (Exists(slime) &&
                    TryComp<XenoSlimeComponent>(slime, out var slimeComp))
                {
                    SpawnExtract(slime, slimeComp, uid);
                }
                crusher.Processing.Remove(slime);
            }

            if (crusher.Processing.Count == 0 && HasComp<JitteringComponent>(uid))
                RemComp<JitteringComponent>(uid);
        }
    }

    private void ScanForSlimes(EntityUid uid, XenoSlimeCrusherComponent crusher)
    {
        var xform = Transform(uid);

        var found = new HashSet<Entity<XenoSlimeComponent>>();
        _lookup.GetEntitiesInRange(xform.Coordinates, crusher.AbsorbRange, found,
            flags: LookupFlags.Uncontained);

        foreach (var slime in found)
        {
            // Только мёртвые или в крите
            if (!TryComp<MobStateComponent>(slime.Owner, out var mobState))
                continue;

            if (mobState.CurrentState != MobState.Dead &&
                mobState.CurrentState != MobState.Critical)
                continue;

            // Не добавлять повторно
            if (crusher.Processing.ContainsKey(slime.Owner))
                continue;

            crusher.Processing[slime.Owner] = crusher.ProcessTime;

            // Jitter при первом слайме в очереди
            if (!HasComp<JitteringComponent>(uid))
                _jitter.AddJitter(uid, -10, 100);

            // Звук дробления при постановке в очередь
            _audio.PlayPvs(crusher.CrushSound, uid);
        }
    }

    private void SpawnExtract(EntityUid slime, XenoSlimeComponent slimeComp, EntityUid crusher)
    {
        var colorIdx = (int) slimeComp.Color;
        if (colorIdx < 0 || colorIdx >= ExtractProtos.Length)
        {
            QueueDel(slime);
            return;
        }

        // Базовое количество экстрактов по возрасту и размеру
        var baseCount = slimeComp.AgeStage switch
        {
            XenoSlimeAge.Ancient => 6,
            XenoSlimeAge.Old     => 3,
            _                    => slimeComp.IsAdult ? 2 : 1,
        };

        var totalCount = baseCount + slimeComp.SteroidCount;
        var coords  = Transform(crusher).Coordinates;
        var protoId = ExtractProtos[colorIdx];

        for (var i = 0; i < totalCount; i++)
            Spawn(protoId, coords);

        QueueDel(slime);
    }
}
