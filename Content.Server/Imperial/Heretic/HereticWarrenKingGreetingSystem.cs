using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Imperial.Heretic.Components;
using Content.Server.Popups;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticWarrenKingGreetingSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;

    private static readonly ProtoId<AccessLevelPrototype> MaintenanceAccess = "Maintenance";
    private static readonly ProtoId<AccessLevelPrototype> ExternalAccess = "External";

    private static readonly SoundPathSpecifier MagicSound =
        new("/Audio/Imperial/heretic/magic.ogg");
    private static readonly SoundCollectionSpecifier SparksSound =
        new("HereticSparks");

    private const float BrandRange = 5f;

    public void OnGreetingCreated(EntityUid spawned, HereticWarrenKingGreetingResultComponent comp, EntityUid caster)
    {
        var coords = Transform(spawned).Coordinates;
        var branded = 0;
        var lockedDoors = 0;

        // Unique brand ID for this specific ritual instance — only cards branded
        // by this ritual can open the doors it locks.
        ProtoId<AccessLevelPrototype> brandId = $"HereticBrand_{Guid.NewGuid():N}";

        foreach (var ent in _lookup.GetEntitiesInRange<IdCardComponent>(coords, BrandRange))
        {
            if (!TryComp<AccessComponent>(ent.Owner, out var access))
                continue;

            access.Tags.Add(brandId);
            access.Tags.Add(MaintenanceAccess);
            access.Tags.Add(ExternalAccess);
            Dirty(ent.Owner, access);
            Spawn("HereticEffectEldritchSparks", Transform(ent.Owner).Coordinates);
            _audio.PlayPvs(SparksSound, ent.Owner);
            branded++;
        }

        foreach (var ent in _lookup.GetEntitiesInRange<DoorComponent>(coords, BrandRange))
        {
            // Airlocks delegate access to a door electronics board inside them.
            // GetMainAccessReader resolves to that inner reader (or the door's own if none).
            if (!TryComp<AccessReaderComponent>(ent.Owner, out _))
                continue;

            if (!_accessReader.GetMainAccessReader(ent.Owner, out var mainReader))
                continue;

            _accessReader.TrySetAccesses(
                mainReader.Value,
                new List<HashSet<ProtoId<AccessLevelPrototype>>> { new HashSet<ProtoId<AccessLevelPrototype>> { brandId } });
            Spawn("HereticEffectEldritchSparks", Transform(ent.Owner).Coordinates);
            _audio.PlayPvs(SparksSound, ent.Owner);
            _audio.PlayPvs(MagicSound, ent.Owner);
            lockedDoors++;
        }

        if (branded > 0 || lockedDoors > 0)
        {
            _popup.PopupEntity(
                Loc.GetString("heretic-warren-king-greeting-complete", ("cards", branded), ("doors", lockedDoors)),
                caster, caster, PopupType.Large);
        }
        else
        {
            _popup.PopupEntity(
                Loc.GetString("heretic-warren-king-greeting-empty"),
                caster, caster, PopupType.Medium);
        }

        QueueDel(spawned);
    }
}
