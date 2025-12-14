using System.Linq;
using Content.Shared.Imperial.SpawnOnAction.Components;
using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.Medieval.Trading.Prototypes;


[DataDefinition, NetSerializable, Serializable]
public sealed partial record GuildTradingItem
{
    [DataField]
    public int Cost;

    public bool IsCostChanged;
    public int ChangedCost;

    [DataField]
    public EntProtoId? ProductEntity;

    /// <summary>
    /// Минимальное количество репутации для покупки этого предмета
    /// </summary>
    [DataField]
    public int MinReputation = 0;

    /// <summary>
    /// Минимальное место в таблице репутации гильдии.
    /// Например, на первом месте игрок, у которого больше всего репутации с данной гильдией.
    /// По умолчанию 0 - любое место
    /// </summary>
    [DataField]
    public int MinReputationPlace = 0;

    [DataField] public string? SpawnOnActionWhitelist;

    [DataField]
    public float ReputationForBuying = 2;
    public Guid GuildId;

    [DataField] public string? Name;
    [DataField] public string? Description;

    public (bool, string?) CanBuy(NetEntity ent, Guild guild, IEntityManager? entityManager = null)
    {
        var reputation = guild.GetReputation(ent);

        if (reputation < MinReputation)
            return (false, Loc.GetString("trading-ui-reputation-lack", ("rep", reputation), ("requiredRep", MinReputation)));

        if (MinReputationPlace > 0)
        {
            if (!guild.Reputation.TryGetValue(ent, out var entRep))
                return (false, Loc.GetString("trading-ui-reputation-place-lack", ("requiredPlace", MinReputationPlace)));

            var betterCount = guild.Reputation.Count(x => x.Value > entRep);
            if (betterCount >= MinReputationPlace)
                return (false, Loc.GetString("trading-ui-reputation-place-lack", ("requiredPlace", MinReputationPlace)));
        }


        if (SpawnOnActionWhitelist != null && entityManager != null)
        {
            var entUid = entityManager.GetEntity(ent);
            if (!entityManager.TryGetComponent<SpawnOnActionComponent>(entUid, out var spawnOnAction))
                return (false, null);

            if (spawnOnAction.ActionId != SpawnOnActionWhitelist)
                return (false, null);
        }

        return (true, null);
    }


    public bool Equals(GuildTradingItem? other)
    {
        if (other == null)
            return false;

        return ProductEntity == other.ProductEntity &&
               Cost == other.Cost &&
               MinReputation == other.MinReputation &&
               MinReputationPlace == other.MinReputationPlace;
    }
}
