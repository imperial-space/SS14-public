using System.Linq;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Factions;

[Prototype]
public sealed class FactionGoalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = default!;

    [DataField(required: true)]
    public string Description = default!;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField(serverOnly: true, required: true)]
    public FactionGoalCompleter Completer = default!;
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class FactionGoalCompleter
{
    public virtual FactionGoalCompleter CreateInstance()
    {
        return this;
    }

    public virtual float GetCompletion(IEntityManager entMan)
    {
        return 0f;
    }

    public virtual string GetDesc(string desctiptionString)
    {
        return desctiptionString;
    }
}
