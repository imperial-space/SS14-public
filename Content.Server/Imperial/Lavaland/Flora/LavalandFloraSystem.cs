using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Imperial.Lavaland.Flora;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Flora;

/// <summary>Перенос structure/flora (harvest, regrow) для флоры и камней лаваленда.</summary>
public sealed class LavalandFloraSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandFloraComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LavalandFloraComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<LavalandFloraComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<LavalandFloraComponent, LavalandFloraHarvestDoAfterEvent>(OnHarvest);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandFloraComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Harvested && now >= comp.RegrowAt)
                Regrow((uid, comp));
        }
    }

    private string State(LavalandFloraComponent comp)
    {
        return comp.Variants > 0 ? $"{comp.BaseState}{comp.VariantSeparator}{comp.Variant}" : comp.BaseState;
    }

    private void OnMapInit(Entity<LavalandFloraComponent> ent, ref MapInitEvent args)
    {
        var comp = ent.Comp;
        if (comp.Variants > 0)
        {
            if (comp.VariantWeights.Count == comp.Variants)
            {
                var roll = _random.NextFloat() * comp.VariantWeights.Sum();
                comp.Variant = comp.Variants;
                for (var i = 0; i < comp.VariantWeights.Count; i++)
                {
                    roll -= comp.VariantWeights[i];
                    if (roll > 0)
                        continue;
                    comp.Variant = i + 1;
                    break;
                }
            }
            else
            {
                comp.Variant = _random.Next(1, comp.Variants + 1);
            }
        }

        var meta = MetaData(ent);
        comp.OriginalName = meta.EntityName;
        comp.OriginalDesc = meta.EntityDescription;
        if (_light.TryGetLight(ent, out var light))
            comp.OriginalLightRadius = light.Radius;

        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<LavalandFloraComponent> ent)
    {
        var state = State(ent.Comp);
        _appearance.SetData(ent, MegafaunaVisuals.State, ent.Comp.Harvested ? state + "p" : state);
        if (ent.Comp.EmissiveSuffix != null)
            _appearance.SetData(ent, MegafaunaVisuals.Overlay, ent.Comp.Harvested ? string.Empty : state + ent.Comp.EmissiveSuffix);
    }

    private void OnInteractHand(Entity<LavalandFloraComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.ToolTag != null)
            return;
        args.Handled = TryStartHarvest(ent, args.User, null);
    }

    private void OnInteractUsing(Entity<LavalandFloraComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.ToolTag is not { } tag || !_tag.HasTag(args.Used, tag))
            return;
        args.Handled = TryStartHarvest(ent, args.User, args.Used);
    }

    private bool TryStartHarvest(Entity<LavalandFloraComponent> ent, EntityUid user, EntityUid? used)
    {
        if (ent.Comp.Harvested)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-flora-nothing", ("flora", ent.Owner)), ent, user);
            return true;
        }

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.HarvestTime,
            new LavalandFloraHarvestDoAfterEvent(), ent, ent, used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    /// <summary>harvest: продукты по весам, затем отрастание или удаление.</summary>
    private void OnHarvest(Entity<LavalandFloraComponent> ent, ref LavalandFloraHarvestDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Harvested)
            return;
        args.Handled = true;

        var coords = Transform(ent).Coordinates;
        var amount = _random.Next(ent.Comp.AmountLow, ent.Comp.AmountHigh + 1);
        var total = ent.Comp.Products.Values.Sum();
        for (var i = 0; i < amount; i++)
        {
            var roll = _random.NextFloat() * total;
            foreach (var (proto, weight) in ent.Comp.Products)
            {
                roll -= weight;
                if (roll > 0)
                    continue;
                Spawn(proto, coords);
                break;
            }
        }

        _popup.PopupEntity(Loc.GetString(ent.Comp.HarvestMessage, ("flora", ent.Owner)), ent, args.User);

        if (ent.Comp.DeleteOnHarvest)
        {
            if (ent.Comp.SpawnOnHarvest is { } spawn)
                Spawn(spawn, coords);
            QueueDel(ent);
            return;
        }

        ent.Comp.Harvested = true;
        ent.Comp.RegrowAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(ent.Comp.RegrowthLow, ent.Comp.RegrowthHigh));
        if (ent.Comp.HarvestedName is { } name)
            _meta.SetEntityName(ent, Loc.GetString(name));
        if (ent.Comp.HarvestedDesc is { } desc)
            _meta.SetEntityDescription(ent, Loc.GetString(desc));
        if (ent.Comp.HarvestedLightRadius is { } radius && _light.TryGetLight(ent, out var light))
        {
            _light.SetRadius(ent, radius, light);
            _light.SetEnabled(ent, radius > 0, light);
        }

        UpdateVisuals(ent);
    }

    /// <summary>regrow.</summary>
    private void Regrow(Entity<LavalandFloraComponent> ent)
    {
        ent.Comp.Harvested = false;
        _meta.SetEntityName(ent, ent.Comp.OriginalName);
        _meta.SetEntityDescription(ent, ent.Comp.OriginalDesc);
        if (ent.Comp.HarvestedLightRadius != null && _light.TryGetLight(ent, out var light))
        {
            _light.SetRadius(ent, ent.Comp.OriginalLightRadius, light);
            _light.SetEnabled(ent, true, light);
        }

        UpdateVisuals(ent);
    }
}
