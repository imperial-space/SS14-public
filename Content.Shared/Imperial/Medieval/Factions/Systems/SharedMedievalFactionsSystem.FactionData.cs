using System.Diagnostics.CodeAnalysis;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Factions;

public abstract partial class SharedMedievalFactionsSystem
{
    public bool TryGetFactionDataContainer([NotNullWhen(true)] out Entity<FactionDataContainerComponent>? ent)
    {
        ent = null;
        var query = EntityQueryEnumerator<FactionDataContainerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            ent = (uid, comp);
            return true;
        }

        return false;
    }
    public bool TryGetRelation(ProtoId<MedievalFactionPrototype> faction1, ProtoId<MedievalFactionPrototype> faction2, out ProtoId<FactionRelationsPrototype> relation)
    {
        if (!TryGetFactionDataContainer(out var container))
        {
            relation = default;
            return false;
        }
        var relations = container.Value.Comp.Relations;
        if (relations.TryGetValue(faction1, out var dict) && dict.TryGetValue(faction2, out relation))
            return true;
        relation = default;
        return false;
    }

    public bool TryGetFactionMemberData(int id, [NotNullWhen(true)] out FactionMemberData? data)
    {
        data = null;
        if (!TryGetFactionDataContainer(out var container))
            return false;

        foreach (var item in container.Value.Comp.CachedMembers)
        {
            if (item.Value.TryGetValue(id, out data))
                return true;
        }

        return false;
    }

    public bool TryGetFactionGroupObjective(ProtoId<MedievalFactionPrototype> proto, FactionMemberGroup group, [NotNullWhen(true)] out string? val)
    {
        val = null;
        if (!TryGetFactionDataContainer(out var container))
            return false;

        return container.Value.Comp.Objectives.TryGetValue(proto, out var objectives) && objectives.TryGetValue(group, out val);
    }

    public bool GetFactionMemberById(int id, [NotNullWhen(true)] out EntityUid? entity)
    {
        entity = null;

        var query = EntityQueryEnumerator<MedievalFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.MemberID == id)
            {
                entity = uid;
                return true;
            }
        }
        return false;
    }
}
