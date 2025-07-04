using System.Collections.Frozen;
using System.Linq;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Customization;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Customization;

public sealed class CustomizationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ActorSystem _actor = default!;


    private FrozenDictionary<string, FrozenDictionary<EntProtoId, List<EntProtoId>>> _map =
        new Dictionary<string, FrozenDictionary<EntProtoId, List<EntProtoId>>>().ToFrozenDictionary();

    public override void Initialize()
    {
        base.Initialize();

        ReloadPrototypes();
        _prototype.PrototypesReloaded += OnPrototypesReloaded;

        SubscribeLocalEvent<CustomizableComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _prototype.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<CustomizationPrototype>())
            return;

        ReloadPrototypes();
    }

    private void ReloadPrototypes()
    {
        _map = _prototype
            .EnumeratePrototypes<CustomizationPrototype>()
            .ToDictionary(prototype => prototype.Holder, prototype => prototype.Map.ToFrozenDictionary())
            .ToFrozenDictionary();
    }

    private void OnGetVerbs(Entity<CustomizableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract)
            return;

        var user = args.User;
        if (!HasComp<CustomizerComponent>(user))
            return;

        if (!_mind.TryGetMind(args.User, out _, out var mindComponent))
            return;

        if (!_actor.TryGetSession(mindComponent.Owner, out var session))
            return;

        if (session is null)
            return;

        if (!_map.TryGetValue(session.UserId.ToString(), out var map))
            return;

        if (!map.TryGetValue(MetaData(ent).EntityPrototype?.ID ?? string.Empty, out var customizations))
            return;

        foreach (var customization in customizations)
        {
            if (!_prototype.TryIndex(customization, out var prototype))
                continue;

            args.Verbs.Add(new Verb
            {
                Text = prototype.Name,
                Category = VerbCategory.Customization,
                Act = () => OnCustomize(ent, customization, user),
                Impact = LogImpact.Medium,
            });
        }
    }

    private void OnCustomize(Entity<CustomizableComponent> ent, EntProtoId customization, EntityUid user)
    {
        var transform = Transform(ent);
        if (!HasComp<MapGridComponent>(transform.ParentUid))
        {
            _popup.PopupClient("customize-only-in-world", user, user, PopupType.MediumCaution);
            return;
        }

        Spawn(customization, transform.Coordinates);
        Del(ent);
    }
}
