# Общий интерфейс
death-note-ui-title = Тетрадь смерти
death-note-admin-ui-title = Журнал Тетради смерти
death-note-ui-previous-page = Назад
death-note-ui-next-page = Далее
death-note-ui-line-placeholder = Впишите истинное имя…
death-note-ui-line-tooltip = Можно написать только имя, добавить причину и время либо поставить «!» после имени для особого сценария. Нажмите Enter.
death-note-ui-page-number = Страница { $page } из { $count }
death-note-ui-spread-number = Разворот { $spread } из { $count }
death-note-ui-writable-page-heading = Страница { $page }

# Безопасные ответы автору
death-note-feedback-accepted = Запись внесена в тетрадь.
death-note-feedback-invalid-format = Напишите имя; при желании добавьте «умрёт от причины» и время. Особый сценарий: «Имя !в HH:MM:SS …».
death-note-feedback-invalid-name = Введите имя.
death-note-feedback-name-too-long = Введённое имя слишком длинное.
death-note-feedback-cause-too-long = Причина смерти слишком длинная.
death-note-feedback-invalid-time = Время должно быть указано в формате HH:MM:SS.
death-note-feedback-time-in-past = Нельзя назначить уже прошедшее время.
death-note-feedback-time-too-soon = Для этой причины смерти назначьте более позднее время.
death-note-feedback-unknown-preset = Такая причина смерти не поддерживается.
death-note-feedback-invalid-page = Такой страницы в тетради нет.
death-note-feedback-page-full = На этой странице больше нет свободных строк.
death-note-feedback-entry-limit = В тетради больше нет свободных строк.
death-note-feedback-cooldown = Чернила ещё не высохли.
death-note-feedback-no-pen = Для записи нужна подходящая ручка.
death-note-feedback-access-denied = Сейчас вы не можете писать в этой тетради.
death-note-feedback-replay = Эта форма уже была отправлена.
death-note-feedback-technical-error = Запись не удалось внести из-за технической ошибки.

# Страница правил
death-note-rule-page =
    I. Тот, чьё истинное имя начертано в этой тетради, умрёт.
    II. Если участь не названа, через сорок секунд сердце человека остановится.
    III. Причину можно начертать словами «умрёт от…», а час — поставить после имени.
    IV. Указанный час отсчитывается от начала смены; без него остаётся сорок секунд.
    V. Уже запечатанную судьбу нельзя стереть или переписать.
    VI. Особый символ «!» после имени превращает оставшуюся строку в прямое предписание жертве.
    VII. Тетрадь не подчинит невозможному и не отменит законы этого мира.
    VIII. Ложное или общее имя оставит страницу без ответа; последствия истинной записи могут коснуться свидетелей.

# Названия пресетов
death-note-preset-heart-attack = Сердечный приступ
death-note-preset-immovable-rod = Неостановимый стержень
death-note-preset-meteor = Метеорит
death-note-preset-explosion = Локальный взрыв
death-note-preset-electrocution = Электрический разряд
death-note-preset-fire = Возгорание
death-note-preset-poison = Отравление
death-note-preset-asphyxiation = Удушье
death-note-preset-carp = Космические карпы
death-note-preset-spiders = Гигантские пауки

# Административный журнал
death-note-admin-notebook = Тетрадь
death-note-admin-owner = Владелец
death-note-admin-writer = Автор записи
death-note-admin-page-line = Страница и строка
death-note-admin-entered-name = Введённое имя
death-note-admin-cause = Причина
death-note-admin-preset = Распознанный пресет
death-note-admin-destruction = Сопутствующий ущерб
death-note-admin-destruction-minimal = минимальный
death-note-admin-destruction-low = низкий
death-note-admin-destruction-moderate = умеренный
death-note-admin-destruction-high = высокий
death-note-admin-destruction-extreme = катастрофический
death-note-admin-destruction-unknown = не определён
death-note-admin-target = Выбранная цель
death-note-admin-status = Статус
death-note-admin-effect-state = Смертельный эффект
death-note-admin-death-state = Смерть цели
death-note-admin-state-started = запущен
death-note-admin-state-not-started = не запущен
death-note-admin-state-confirmed = подтверждена
death-note-admin-state-not-confirmed = не подтверждена
death-note-admin-result = Результат
death-note-admin-error = Ошибка
death-note-admin-refresh = Обновить
death-note-admin-loading = Загрузка страницы…
death-note-admin-summary = Всего записей в текущей смене: { $count }
death-note-admin-no-records = В текущей смене записей ещё нет.
death-note-admin-entry-status-submitted = внесена
death-note-admin-entry-status-scheduled = назначена
death-note-admin-entry-status-custom-delivered = особое предписание доставлено
death-note-admin-entry-status-executing = исполняется
death-note-admin-entry-status-effect-started = эффект запущен
death-note-admin-entry-status-death-confirmed = смерть подтверждена
death-note-admin-entry-status-failed = исполнение завершилось ошибкой
death-note-admin-entry-status-target-missing = цель не найдена
death-note-admin-entry-status-target-already-dead = цель уже мертва
death-note-admin-entry-status-ambiguous-name = имя неоднозначно
death-note-admin-entry-status-invalid-format = неверный формат
death-note-admin-entry-status-cancelled = отменена
death-note-admin-entry-status-unknown = неизвестный статус
death-note-admin-failure-none = без ошибки
death-note-admin-failure-target-missing = цель не найдена
death-note-admin-failure-target-already-dead = цель уже мертва
death-note-admin-failure-target-deleted = сущность цели удалена
death-note-admin-failure-target-no-longer-alive = цель больше не жива
death-note-admin-failure-ambiguous-name = найдено несколько целей с таким именем
death-note-admin-failure-invalid-format = неверный формат записи
death-note-admin-failure-invalid-name = неверное имя
death-note-admin-failure-invalid-time = неверный формат времени
death-note-admin-failure-time-in-past = назначенное время уже прошло
death-note-admin-failure-time-too-soon = до назначенного времени недостаточно времени для сценария
death-note-admin-failure-unknown-preset = причина смерти не распознана
death-note-admin-failure-name-too-long = имя слишком длинное
death-note-admin-failure-cause-too-long = причина смерти слишком длинная
death-note-admin-failure-invalid-page = неверная страница
death-note-admin-failure-page-full = страница заполнена
death-note-admin-failure-entry-limit-reached = достигнут предел записей
death-note-admin-failure-cooldown = действует задержка между записями
death-note-admin-failure-replay = повторная отправка записи
death-note-admin-failure-no-pen = отсутствует письменный предмет
death-note-admin-failure-access-denied = доступ запрещён
death-note-admin-failure-handler-error = ошибка обработчика причины смерти
death-note-admin-failure-round-ended = смена завершена
death-note-admin-failure-technical-error = техническая ошибка
death-note-admin-failure-unknown = неизвестная ошибка

# Административные команды
cmd-deathnoteinfluence-desc = Показать подключённому игроку особое влияние Тетради смерти.
cmd-deathnoteinfluence-help = { $command } <ckey> <текст>
cmd-deathnoteinfluence-hint-ckey = <ckey>
cmd-deathnoteinfluence-hint-text = <текст особого влияния>
cmd-deathnoteinfluence-error-usage = Использование: { $help }
cmd-deathnoteinfluence-error-not-connected = Игрок не подключён.
cmd-deathnoteinfluence-error-empty = Текст влияния не может быть пустым.
cmd-deathnoteinfluence-error-too-long = Текст влияния не может быть длиннее { $limit } символов.
cmd-deathnoteinfluence-server-console = консоль сервера
cmd-deathnoteinfluence-success = Влияние Тетради смерти отправлено игроку { $player }.

# Кастомное ролевое влияние
death-note-influence-title = Влияние Тетради смерти
death-note-influence-intro = Вы попали под влияние Тетради смерти.
death-note-influence-scenario = Сценарий
death-note-influence-rules = Следуйте указанию только в рамках правил проекта, здравого смысла и текущего ивента. Если выполнение невозможно или нарушает правила, обратитесь к ивентологу через ахелп.

# Эффекты
death-note-heart-attack-popup = Вашу грудь пронзает внезапная боль!
death-note-ceiling-collapse-popup = На вас обрушилась крыша!
death-note-preset-lightning = Удар молнии
death-note-preset-turret = Огонь турели
death-note-preset-blunt-damage = Ударная травма
death-note-preset-slash-damage = Резаные раны
death-note-preset-piercing-damage = Колотые раны
death-note-preset-heat-damage = Сильные ожоги
death-note-preset-cold-damage = Сильное обморожение
death-note-preset-shock-damage = Электрические ожоги
death-note-preset-caustic-damage = Кислотные ожоги
death-note-preset-poison-damage = Токсическое поражение
death-note-preset-radiation-damage = Лучевая болезнь
death-note-preset-cellular-damage = Клеточное повреждение
death-note-preset-bloodloss-damage = Массивная кровопотеря
death-note-unrevivable = Неизвестная сила не позволяет вернуть цель к жизни.
death-note-preset-airlock-accident = Несчастный случай со шлюзом
death-note-preset-disposal-catastrophe = Мусорная катастрофа
death-note-guidance-disposal = Ваша судьба тянет вас к утилизации. До истечения двух минут подойдите к утилизационному блоку и попытайтесь воспользоваться им.
death-note-preset-vending-machine-crush = Падение торгового автомата
death-note-guidance-vending-machine = Необъяснимая потребность ведёт вас к торговому автомату. До истечения двух минут подойдите к нему и попытайтесь воспользоваться им.
death-note-preset-poisoned-food = Отравленная пища
death-note-guidance-poisoned-food = Вас внезапно охватывает мучительный голод. В течение двух минут съешьте что-нибудь — судьба уже выбрала вашу трапезу.
death-note-preset-poisoned-drink = Отравленный напиток
death-note-guidance-poisoned-drink = Вас терзает внезапная неутолимая жажда. В течение двух минут выпейте что-нибудь — судьба уже выбрала ваш напиток.
death-note-preset-ceiling-collapse = Обрушение потолка
death-note-preset-mimic = Мимикрия предмета
death-note-preset-bluespace-anomaly = Блюспейс-аномалия

ent-DeathNote = Тетрадь смерти
    .desc = Чёрная тетрадь, страницы которой ждут настоящих имён.
ent-DeathNote2 = Тетрадь смерти
    .desc = Чёрная тетрадь, страницы которой ждут настоящих имён.
ent-DeathNote3 = Тетрадь смерти
    .desc = Чёрная тетрадь, страницы которой ждут настоящих имён.
ent-DeathNoteUnrevivable = Тетрадь смерти
    .desc = Чёрная тетрадь, страницы которой ждут настоящих имён.
ent-DeathNoteUnrevivable2 = Тетрадь смерти
    .desc = Чёрная тетрадь, страницы которой ждут настоящих имён.
ent-DeathNoteUnrevivable3 = Тетрадь смерти
    .desc = Чёрная тетрадь, страницы которой ждут настоящих имён.
ent-DeathNoteAdminLedger = журнал Тетради смерти
    .desc = Доступный только администраторам журнал событий Тетради смерти.
ent-DeathNoteMimic = мимик Тетради смерти
    .desc = Предмет, которому тетрадь придала голодную и враждебную форму.
ent-DeathNoteTargetedCarp = карп, направляемый Тетрадью смерти
ent-DeathNoteCarpRift = карповый разлом Тетради смерти
ent-DeathNoteTargetedSpider = паук, направляемый Тетрадью смерти
