using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class ZveresAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZveresAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, ZveresAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;
        message = Regex.Replace(
            message,
            "Р+",
            _random.Pick(new List<string>() { "Рр", "Ррр" })
        );
        message = Regex.Replace(
            message,
            "р+",
            _random.Pick(new List<string>() { "рр", "ррр" })
        );
        args.Message = message;
    }
}
