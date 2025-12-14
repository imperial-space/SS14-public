using System.Linq;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Trading;

[Serializable, NetSerializable]
public sealed class Guild : IEquatable<Guild>
{
    public Guid Id { get; }
    public string Name { get; private set; }

    // TODO: указать здесь стандартную иконку
    public string IconPath { get; private set; } = "";

    public ProtoId<GuildTypePrototype> TypePrototype { get; private set; }

    public List<GuildTradingItem> Items;
    public Dictionary<GuildTradingItem, string> UnavailableItems = new();

    public Dictionary<NetEntity, float> Reputation = new();

    public float GetReputation(NetEntity ent)
    {
        return Reputation.GetValueOrDefault(ent, 0);
    }

    public void AddReputation(NetEntity ent, float val)
    {
        Reputation[ent] = Reputation.GetValueOrDefault(ent) + val;
    }

    private Guild(Guid id, string name, ProtoId<GuildTypePrototype> typePrototype, string iconPath, List<GuildTradingItem>? items = null)
    {
        items ??= new();

        Id = id;
        Name = name;
        TypePrototype = typePrototype;
        IconPath = iconPath;
        Items = items;
    }

    public Guild(GuildTypePrototype prototype, IRobustRandom random, IPrototypeManager prototypeManager)
    {
        foreach (var item in prototype.Items)
        {
            var factor = 1.0 + (random.NextDouble() * 0.4 - 0.2);
            item.ChangedCost = (int)Math.Round(item.Cost * factor);
        }


        Id = Guid.NewGuid();

        var namePrototype = prototypeManager.Index(prototype.Name);
        Name = GenerateName(namePrototype, random);

        Items = prototype.Items.Select(x => x with { }).ToList();
        Items.ForEach(i => i.GuildId = Id);

        TypePrototype = prototype.ID;

        if (prototype.Icons.Count > 0)
        {
            var icon = random.Pick(prototype.Icons);
            IconPath = prototypeManager.Index(icon).TexturePath;
        }
        else if (prototypeManager.TryGetRandom<GuildIconPrototype>(random, out var proto))
        {
            if (proto is not GuildIconPrototype icon)
                return;

            IconPath = icon.TexturePath;
        }
    }

    public string GenerateName(GuildNamePrototype prototype, IRobustRandom random)
    {
        var name = "";
        for (var i = 1; i <= prototype.PartCount; i++)
        {
            var i1 = i;
            var parts = prototype.Parts
                .Where(p => i1 >= p.Min)
                .Where(p => i1 <= p.Max)
                .ToArray();

            var part = random.Pick(parts);
            name += part.Text;
            name += prototype.Split;
        }

        return name;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public bool Equals(Guild? obj)
    {
        if (obj == null)
            return false;

        return Id == obj.Id;
    }

    public Guild Clone()
    {
        return new Guild(Id, Name, TypePrototype, IconPath, Items.Select(x => x with { }).ToList())
        {
            Reputation = new Dictionary<NetEntity, float>(Reputation)
        };
    }

    public Guild CloneWithoutItems()
    {
        return new Guild(Id, Name, TypePrototype, IconPath)
        {
            Reputation = new Dictionary<NetEntity, float>(Reputation)
        };
    }

}
