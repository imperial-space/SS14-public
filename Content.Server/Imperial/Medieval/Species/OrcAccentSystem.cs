using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class OrcAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrcAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, OrcAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;



        if (message.StartsWith("я", StringComparison.Ordinal))
        {
            message.Remove(0, 1).Insert(0, "моя");
        }

        message = Regex.Replace(message, "ты+", "твоя");
        message = Regex.Replace(message, "Ты+", "Твоя");
        message = Regex.Replace(message, "ТЫ+", "ТВОЯ");

        message = Regex.Replace(message, "тебе+", "твоя");
        message = Regex.Replace(message, "Тебе+", "Твоя");
        message = Regex.Replace(message, "ТЕБЕ+", "ТВОЯ");

        message = Regex.Replace(message, "маг+", "колдубей");
        message = Regex.Replace(message, "Маг+", "Колдубей");
        message = Regex.Replace(message, "МАГ+", "КОЛДУБЕЙ");

        message = Regex.Replace(message, "волшебник+", "колдубей");
        message = Regex.Replace(message, "Волшебник+", "Колдубей");
        message = Regex.Replace(message, "ВОЛШЕБНИК+", "КОЛДУБЕЙ");

        message = Regex.Replace(message, "чародей+", "колдубей");
        message = Regex.Replace(message, "Чародей+", "Колдубей");
        message = Regex.Replace(message, "ЧАРОДЕЙ+", "КОЛДУБЕЙ");

        message = Regex.Replace(message, "не+", "ны");
        message = Regex.Replace(message, "Не+", "Ны");
        message = Regex.Replace(message, "НЕ+", "НЫ");

        message = Regex.Replace(message, "ай+", "ать");
        message = Regex.Replace(message, "АЙ+", "АТЬ");

        message = Regex.Replace(message, "да ", "ды ");
        message = Regex.Replace(message, "Да ", "Ды ");
        message = Regex.Replace(message, "ДА ", "ДЫ ");

        message = Regex.Replace(message, "аю+", "ать");
        message = Regex.Replace(message, "АЮ+", "АТЬ");

        message = Regex.Replace(message, "ешь+", "ать");
        message = Regex.Replace(message, "ЕШЬ+", "АТЬ");

        message = Regex.Replace(message, "ёшь+", "ать");
        message = Regex.Replace(message, "ЁШЬ+", "АТЬ");

        message = Regex.Replace(message, "ете+", "ать");
        message = Regex.Replace(message, "ЕТЕ+", "АТЬ");

        message = Regex.Replace(message, "ет+", "ать");
        message = Regex.Replace(message, "ЕТ+", "АТЬ");

        message = Regex.Replace(message, "им+", "ать");
        message = Regex.Replace(message, "ИМ+", "АТЬ");

        message = Regex.Replace(message, "ишь+", "ать");
        message = Regex.Replace(message, "ИШЬ+", "АТЬ");

        message = Regex.Replace(message, "ите+", "ать");
        message = Regex.Replace(message, "ИТЕ+", "АТЬ");

        message = Regex.Replace(message, "ят+", "ать");
        message = Regex.Replace(message, "ЯТ+", "АТЬ");

        message = Regex.Replace(message, "ал+", "ать");
        message = Regex.Replace(message, "АЛ+", "АТЬ");

        args.Message = message;
    }
}
