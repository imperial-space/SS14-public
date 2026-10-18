using Content.Shared.Speech;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>
/// Речь Халка (hulk.dm из SS13): всё заглавными и с восклицаниями.
/// </summary>
[RegisterComponent]
public sealed partial class HulkAccentComponent : Component;

public sealed class HulkAccentSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HulkAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<HulkAccentComponent> ent, ref AccentGetEvent args)
    {
        var message = args.Message.TrimEnd('.', ' ').ToUpperInvariant();
        args.Message = message.EndsWith('!') ? message + "!" : message + "!!";
    }
}
