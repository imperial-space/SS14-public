using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;


namespace Content.Server.Speech.EntitySystems
{
    public sealed class GigaMonkeyAccentSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        private static readonly IReadOnlyList<string> Faces = new List<string>{
            "уу ", " ууууу ", "ууу ", "уу-уу ", "ууу-уу-уу-у ", "у-уууу-у-у "
        }.AsReadOnly();

        private static readonly IReadOnlyList<string> Questions = new List<string>{
            "?", "???", "твоя давать ответ ", "я задать вопрос ", "говори ответ ", "давай говори "
        }.AsReadOnly();

        public override void Initialize()
        {
            SubscribeLocalEvent<GigaMonkeyAccentComponent, AccentGetEvent>(OnAccent);
        }

        public string Accentuate(string message)
        { // тут очень простой алгоритмический код, миртон сам так сказал. хоть я с ним и весь день просрал.

            Dictionary<char, string> replacements = new Dictionary<char, string>();

            replacements['!'] = _random.Pick(Faces);
            replacements['?'] = _random.Pick(Questions);
            replacements['й'] = "бить ";
            replacements['Й'] = "Бить ";
            replacements['ц'] = "ахота ";
            replacements['Ц'] = "Ахота ";
            replacements['у'] = "дабыча ";
            replacements['У'] = "Дабыча ";
            replacements['к'] = "кущац ";
            replacements['К'] = "Кушац ";
            replacements['е'] = "прекол ";
            replacements['Е'] = "Прекол ";
            replacements['н'] = "брад ";
            replacements['Н'] = "Брад ";
            replacements['г'] = "чужак ";
            replacements['Г'] = "Чужак ";
            replacements['ш'] = "благадарить ";
            replacements['Ш'] = "Благадарить ";
            replacements['щ'] = "спать ";
            replacements['Щ'] = "Спать ";
            replacements['з'] = "уше ";
            replacements['З'] = "Уше ";
            replacements['х'] = "виселье хихиха ";
            replacements['Х'] = "Виселье хихиха ";
            replacements['ъ'] = "налить ";
            replacements['Ъ'] = "Налить ";
            replacements['ы'] = "пашел ";
            replacements['Ы'] = "Пашел ";
            replacements['в'] = "манета ";
            replacements['В'] = "Манета ";
            replacements['а'] = "дать ";
            replacements['А'] = "Дать ";
            replacements['п'] = "палка ";
            replacements['П'] = "Палка ";
            replacements['р'] = "трупка мира ";
            replacements['Р'] = "Трупка мира ";
            replacements['о'] = "опана ";
            replacements['О'] = "Опана ";
            replacements['л'] = "чума ";
            replacements['Л'] = "Чума ";
            replacements['д'] = "да ";
            replacements['Д'] = "Да ";
            replacements['ж'] = "жызн ";
            replacements['Ж'] = "Жызн ";
            replacements['э'] = "нипооооон ";
            replacements['Э'] = "Нипооооон ";
            replacements['я'] = "молина ";
            replacements['Я'] = "Молина ";
            replacements['ч'] = "бо-бо ";
            replacements['Ч'] = "Бо-бо ";
            replacements['с'] = "страшни ";
            replacements['С'] = "Страшни ";
            replacements['м'] = "племя ";
            replacements['М'] = "Племя ";
            replacements['и'] = "ни ";
            replacements['И'] = "Ни ";
            replacements['т'] = "твоя ";
            replacements['Т'] = "Твоя ";
            replacements['ь'] = "стоять ";
            replacements['Ь'] = "Стоять ";
            replacements['б'] = "бунд ";
            replacements['Б'] = "Бунд ";
            replacements['ю'] = "работадь ";
            replacements['Ю'] = "Работадь ";
            replacements['ф'] = "если ";
            replacements['Ф'] = "Если ";

            replacements['q'] = "глупи ";
            replacements['Q'] = "Глупи ";
            replacements['w'] = "падмога ";
            replacements['W'] = "Падмога ";
            replacements['e'] = "ходячий кость ";
            replacements['E'] = "Ходячий кость ";
            replacements['t'] = "решать ";
            replacements['T'] = "Решать ";
            replacements['r'] = "гобля ";
            replacements['R'] = "Гобля ";
            replacements['y'] = "умни ";
            replacements['Y'] = "Умни ";
            replacements['u'] = "сюдоооо ";
            replacements['U'] = "Сюдоооо ";
            replacements['i'] = "галова ";
            replacements['I'] = "Галова ";
            replacements['o'] = "очинь мала ";
            replacements['O'] = "Очинь мала ";
            replacements['p'] = "очинь многа ";
            replacements['P'] = "Очинь многа ";
            replacements['a'] = "мысль говорить ";
            replacements['A'] = "Мысль говорить ";
            replacements['s'] = "или ";
            replacements['S'] = "или ";
            replacements['d'] = "говори ";
            replacements['D'] = "Говори ";
            replacements['f'] = "нехорошый ";
            replacements['F'] = "Нехорошый ";
            replacements['g'] = "называнее ";
            replacements['G'] = "Называнее ";
            replacements['h'] = "калекия ";
            replacements['H'] = "Калекия ";
            replacements['j'] = "камень ";
            replacements['J'] = "Камень ";
            replacements['k'] = "савершить пахищенее ";
            replacements['K'] = "Савершить пахищенее ";
            replacements['l'] = "место ";
            replacements['L'] = "Место ";
            replacements['z'] = "лигианер ";
            replacements['Z'] = "Лигианер ";
            replacements['x'] = "выпивка ";
            replacements['X'] = "Выпивка ";
            replacements['c'] = "крушка ";
            replacements['C'] = "Крушка ";
            replacements['v'] = "чорни магия ";
            replacements['V'] = "Чорни магия ";
            replacements['b'] = "нечисть ";
            replacements['B'] = "Нечисть ";
            replacements['n'] = "трафка ";
            replacements['N'] = "Трафка ";
            replacements['m'] = "грустни ";
            replacements['M'] = "Грустни ";


            string transformedMessage = "";

            foreach (char c in message)
            {
                if (replacements.ContainsKey(c))
                {
                    transformedMessage += replacements[c];
                }
                else
                {
                    transformedMessage += _random.Pick(Faces);
                }
            }

            return transformedMessage;
        }

        private void OnAccent(EntityUid uid, GigaMonkeyAccentComponent component, AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message);
        }
    }
}
