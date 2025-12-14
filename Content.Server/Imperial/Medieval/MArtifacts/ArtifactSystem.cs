

using Content.Shared.Actions;
using Content.Shared.Dataset;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Imperial.Medieval.Artifacts;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Artifacts;

public sealed class ArtifactSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ArtifactComponent, MapInitEvent>(Init);
        SubscribeLocalEvent<ArtifactComponent, MeleeHitEvent>(Relay);
        SubscribeLocalEvent<ArtifactComponent, GetItemActionsEvent>(Relay);
        SubscribeLocalEvent<ArtifactComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(Relay);
        SubscribeLocalEvent<ArtifactComponent, ExaminedEvent>(Inspect);
        SubscribeLocalEvent<ArtifactComponent, ComponentGetState>(GetState);
    }
    private void GetState(EntityUid uid, ArtifactComponent component, ref ComponentGetState args)
    {
        args.State = new ArtifactSpriteState()
        {
            Path = component.CurrentSprite
        };
    }
    public void Inspect(EntityUid uid, ArtifactComponent component, ExaminedEvent args)
    {
        var str = Loc.GetString("imperial-medieval-artifact");
        foreach (var ability in component.Abilities)
        {
            str = $"{str}{Environment.NewLine}- {Name(ability.Value)}";
        }
        args.PushMarkup(str);
    }
    public void Relay<T>(EntityUid _, ArtifactComponent component, T args) where T : EntityEventArgs
    {
        foreach (var (_, value) in component.Abilities)
        {
            RaiseLocalEvent(value, args);
        }
    }
    public void Init(EntityUid uid, ArtifactComponent component, MapInitEvent args)
    {
        var abilities = new List<string>();
        abilities.AddRange(_proto.Index<DatasetPrototype>("artifactabilities").Values);
        _meta.SetEntityName(uid, $"{_random.Pick(_proto.Index<DatasetPrototype>("artifactFirstName").Values)} {_random.Pick(_proto.Index<DatasetPrototype>("artifactLastName").Values)}");
        if (component.StartAbilities != null)
        {
            foreach (var ability in component.StartAbilities)
            {
                var ent = Spawn(ability);
                component.Abilities.Add(ability, ent);
            }
        }
        for (int i = 1; i <= component.AmountToRandomize; ++i)
        {
            var ability = _random.PickAndTake(abilities);
            var ent = Spawn(ability);
            //Console.WriteLine(ability);
            component.Abilities.Add(ability, ent);
        }
        foreach (var (_, value) in component.Abilities)
        {
            var comp = EnsureComp<ArtifactAbilityComponent>(value);
            comp.OwnerUid = uid;
            RaiseLocalEvent(value, new ArtifactInit(uid));
        }
        component.CurrentSprite = _random.Pick(_proto.Index<DatasetPrototype>(component.SpriteDataset).Values);
        Dirty(uid, component);
    }
}

public sealed class ArtifactInit : EntityEventArgs
{
    public EntityUid Uid;
    public ArtifactInit(EntityUid uid)
    {
        Uid = uid;
    }
}
