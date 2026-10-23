using System.Linq;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>
/// Сценарии доменов (virtual_domain/domains/*.dm, setup_domain): очки к ящику за смерти (grasslands_hunt, island_brawl),
/// за глотки (beach_bar) и за жертвы на руне (heretic_hunt).
/// </summary>
public sealed partial class QuantumServerSystem
{
    [Dependency] private readonly IngestionSystem _ingestion = default!;

    private static readonly TimeSpan ScriptInterval = TimeSpan.FromSeconds(1);
    private static readonly SoundSpecifier SacrificeSound = new SoundPathSpecifier("/Audio/Magic/staff_animation.ogg");
    private const string DrinkType = "Drink";

    private void InitializeDomainScripts()
    {
        SubscribeLocalEvent<BitrunningAvatarComponent, IngestingEvent>(OnAvatarIngesting);
    }

    private void UpdateDomainScripts(Entity<QuantumServerComponent> ent, TimeSpan now)
    {
        if (ent.Comp.DomainMap is not { } map || now < ent.Comp.NextDeathScan || !_proto.TryIndex(ent.Comp.Domain, out var domain))
            return;

        ent.Comp.NextDeathScan = now + ScriptInterval;
        UpdateObjectives(ent);

        if (domain.DeathPoints > 0)
            ScanDeaths(ent, domain, map);

        ScanSacrifices(ent);
    }

    /// <summary>on_death(): каждая смерть подходящего существа даёт очки.</summary>
    private void ScanDeaths(Entity<QuantumServerComponent> ent, BitrunningDomainPrototype domain, EntityUid map)
    {
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != map || !_mobs.IsDead(uid) || ent.Comp.CountedDeaths.Contains(uid))
                continue;

            if (domain.DeathFilter.Count > 0
                && (MetaData(uid).EntityPrototype is not { } proto || !domain.DeathFilter.Contains(proto.ID)))
                continue;

            ent.Comp.CountedDeaths.Add(uid);
            AddCratePoints(ent, domain.DeathPoints);
        }
    }

    /// <summary>heretic_hunt/check_loc(): тело на большой руне — очки по рангу, тело распадается.</summary>
    private void ScanSacrifices(Entity<QuantumServerComponent> ent)
    {
        if (ent.Comp.DomainGrid is not { } grid)
            return;

        var runes = new List<(EntityUid Rune, Vector2i Tile)>();
        var runeQuery = EntityQueryEnumerator<BitrunningSacrificeRuneComponent, TransformComponent>();
        while (runeQuery.MoveNext(out var rune, out _, out var xform))
        {
            if (xform.GridUid == grid)
                runes.Add((rune, _loader.TileOf(grid, xform.Coordinates)));
        }

        if (runes.Count == 0)
            return;

        var bodies = new List<(EntityUid Body, float Points, EntityUid Rune)>();
        var query = EntityQueryEnumerator<BitrunningSacrificeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sacrifice, out var xform))
        {
            if (xform.GridUid != grid || !_mobs.IsDead(uid) || _container.IsEntityInContainer(uid))
                continue;

            var tile = _loader.TileOf(grid, xform.Coordinates);
            foreach (var (rune, runeTile) in runes)
            {
                if (tile == runeTile)
                    bodies.Add((uid, sacrifice.Points, rune));
            }
        }

        foreach (var (body, points, rune) in bodies)
        {
            Spawn("EffectSparks", Transform(body).Coordinates);
            QueueDel(body);
            AddCratePoints(ent, points);
            _audio.PlayPvs(SacrificeSound, rune);

            var crateNear = _lookup.GetEntitiesInRange<BitrunningEncryptedCacheComponent>(Transform(rune).Coordinates, 1.5f).Any();
            _popup.PopupEntity(Loc.GetString(crateNear ? "bitrunning-ritual-complete" : "bitrunning-sacrifice-accepted"), rune);
        }
    }

    /// <summary>beach_bar/on_drink_drank(): каждый глоток в домене приближает ящик.</summary>
    private void OnAvatarIngesting(Entity<BitrunningAvatarComponent> ent, ref IngestingEvent args)
    {
        if (!TryComp<QuantumServerComponent>(ent.Comp.Server, out var server)
            || !_proto.TryIndex(server.Domain, out var domain) || domain.DrinkPoints <= 0)
            return;

        if (_ingestion.GetEdibleType(args.Food)?.Id != DrinkType)
            return;

        AddCratePoints((ent.Comp.Server, server), domain.DrinkPoints);
    }
}
