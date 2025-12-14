using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Magic.SpellCastEffect;

public sealed partial class EffectSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpellCastEffectComponent, MedievalBeforeCastSpellEvent>(OnBeforeCast);
        SubscribeLocalEvent<SpellCastEffectComponent, MedievalAfterCastSpellEvent>(OnAfterCast);
    }

    private void OnBeforeCast(EntityUid uid, SpellCastEffectComponent component, ref MedievalBeforeCastSpellEvent args)
    {
        if ((component.EffectProto != "") && CanSpawn && _net.IsServer)
        {
            var Effect = Spawn(component.EffectProto, Transform(args.Performer).Coordinates);
            _transform.SetParent(Effect, args.Performer);
            CanSpawn = false;
        }
        return;
    }

    private void OnAfterCast(EntityUid uid, SpellCastEffectComponent component, ref MedievalAfterCastSpellEvent args)
    {
        CanSpawn = true;
        return;
    }

    #region Helpers
    private bool CanSpawn = true;
    #endregion
}
