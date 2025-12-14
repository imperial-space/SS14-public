using Content.Shared.Actions;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Myrmex;

public sealed partial class MyrmexQueenSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    private readonly string[] _eggSounds = new[] {
        "/Audio/Imperial/Medieval/ant_egg1.ogg",
        "/Audio/Imperial/Medieval/ant_egg2.ogg",
    };

    [ValidatePrototypeId<EntityPrototype>]
    public const string ActionId = "MedievalMyrmexQueenLayEggAction";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MyrmexQueenComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MyrmexQueenComponent, ActionMyrmexQueenLayEggEvent>(OnLayEgg);
    }

    private void OnMapInit(Entity<MyrmexQueenComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.Action, ActionId);
        Dirty(ent);
    }

    private void OnLayEgg(EntityUid uid, MyrmexQueenComponent comp, ref ActionMyrmexQueenLayEggEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MyrmexHungerComponent>(uid, out var hunger))
            return;

        if (!TryComp<TransformComponent>(uid, out var transform))
            return;

        if (hunger.Buffs.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("medieval-myrmex-queen-egg-no-buff"), uid, uid);
            return;
        }

        args.Handled = true;

        var sound = _random.Pick(_eggSounds);
        _audio.PlayPvs(sound, uid);

        hunger.Buffs.Pop();
        Spawn(comp.Egg, transform.Coordinates);
    }
}
