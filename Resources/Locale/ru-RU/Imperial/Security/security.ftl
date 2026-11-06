## Снаряжение СБ (tgstation/BandaStation)

# ─── Телепорт гулага ───

gulag-teleporter-locked = { CAPITALIZE($machine) } заперт!
gulag-teleporter-verb-eject = Выпустить
gulag-teleporter-verb-enter = Залезть
gulag-teleporter-breakout-start = Вы упираетесь в стенку и начинаете выдавливать дверь... (это займёт около { $time } с.)
gulag-teleporter-breakout-others = { CAPITALIZE($user) } пинает дверь { $machine }!
gulag-teleporter-breakout-done = { CAPITALIZE($user) } вырывается из { $machine }!
gulag-teleport-dizzy = От телепортации немного кружится голова.

# ─── Консоль телепорта ───

gulag-console-title = Консоль телепорта трудового лагеря
gulag-console-none = —
gulag-console-teleporter = Телепорт
gulag-console-location = Расположение
gulag-console-open = Открыть
gulag-console-close = Закрыть
gulag-console-lock = Запереть
gulag-console-unlock = Отпереть
gulag-console-no-teleporter = Телепорт не найден. Поставьте его вплотную к консоли.
gulag-console-scan = Искать
gulag-console-beacon = Маяк лагеря
gulag-console-no-beacon = Не найден
gulag-console-prisoner = Заключённый
gulag-console-name = Имя
gulag-console-crimstat = Статус
gulag-console-id = ID заключённого
gulag-console-insert-id = Вставить ID
gulag-console-goal = Цель (очки)
gulag-console-teleport = Отправить в лагерь
gulag-console-access-denied = Доступ запрещён.
gulag-console-unlock-first = Сначала отоприте телепорт.
gulag-console-close-first = Сначала закройте телепорт.
gulag-console-no-id = Внимание: нельзя отправить заключённого без вставленного ID!
gulag-console-default-goal = Цель для { $id } по умолчанию: { $goal } очков.

# ─── Консоль управления заключёнными ───

prisoner-management-title = Консоль управления заключёнными
prisoner-management-id = ID заключённого
prisoner-management-points = Очки
prisoner-management-reset = Сбросить очки
prisoner-management-eject = Извлечь ID
prisoner-management-implants = Импланты
prisoner-management-no-implants = Нет отслеживаемых имплантов.
prisoner-management-inject = Ввести { $amount } ед.
prisoner-management-self-destruct = Самоуничтожение
prisoner-management-warn-placeholder = Сообщение в голову...
prisoner-management-warn = Предупредить
prisoner-implant-info-id = ID
prisoner-implant-info-volume = Объём
prisoner-implant-info-location = Местоположение
prisoner-implant-self-destruct = Вы чувствуете крошечный толчок изнутри — один из ваших имплантов выгорает.
prisoner-implant-warn = Вы слышите голос в голове: «{ $message }»
prisoner-chem-implant-beep = Вы слышите тихий писк.
prisoner-chem-implant-empty = Вы слышите тихий щелчок в груди.

# ─── Трудовой лагерь ───

labor-camp-map-name = Трудовой лагерь
labor-stacker-rejected = { CAPITALIZE($item) } не засчитывается. Принимаются только листы ценных материалов.
labor-stacker-accepted = Принято листов: { $count }. Несобранных очков: { $points }.
labor-claim-title = Консоль сбора очков
labor-claim-status = Статус
labor-claim-id-points = Очки на ID
labor-claim-unclaimed = Несобранные очки
labor-claim-claim = Забрать очки
labor-claim-move = Вернуться на станцию
labor-claim-no-goal = Цель не установлена!
labor-claim-goal-met = Норма выполнена!
labor-claim-points-away = До нормы осталось { $points } очков.
labor-claim-no-id = ID заключённого не обнаружен.
labor-claim-transferred = Очки переведены.
labor-claim-no-valid-id = Не найден ID для перевода очков.
labor-claim-no-shuttle = Точка возвращения на станции не найдена.
labor-claim-finished = Срок отбыт, возвращение на станцию.
labor-claim-announce = { $person } вернулся на станцию. Минералы и ID заключённого готовы к получению.
labor-claim-emagged = { CAPITALIZE($machine) } выпускает несколько искр!
labor-checker-no-goal = Для этого ID цель не требуется.
labor-checker-id = ID: { $name }.
labor-checker-points = Собрано очков: { $points } / { $goal }.
labor-checker-info = Очки начисляются за выплавленные листы, сданные в сборщик трудового лагеря. Выполните норму, чтобы выйти на свободу.

# ─── Станция возврата снаряжения ───

gulag-reclaimer-title = Станция возврата снаряжения
gulag-reclaimer-empty = Нет хранящегося снаряжения.
gulag-reclaimer-release = Выдать
gulag-reclaimer-lost-signal = Внимание! Жизненные показатели ранее обработанного заключённого не найдены. Выдаю снаряжение!
gulag-reclaimer-emagged = Проверка ID сбита.

# ─── Compli-o-nator ───

sec-hailer-name = Compli-o-Nator { $user }
sec-hailer-broken = Система оповещения { $mask } сломана.
sec-hailer-heat-1 = { CAPITALIZE($mask) } начинает нагреваться.
sec-hailer-heat-2 = { CAPITALIZE($mask) } опасно перегревается от частого использования!
sec-hailer-overload = Модулятор питания { $mask } перегружается и ломается.
sec-hailer-restrictor-middle = Вы ставите ограничитель в среднее положение.
sec-hailer-restrictor-last = Вы ставите ограничитель в последнее положение.
sec-hailer-restrictor-first = Вы ставите ограничитель в первое положение.
sec-hailer-restrictor-broken = Вы крутите ограничитель, но ничего не происходит — похоже, он сломан.
sec-hailer-restrictor-cut = Вы сломали ограничитель!
sec-hailer-emagged = Голосовая схема выжжена.
sec-hailer-phrase-emag = ДА ЧТОБ ТЕБЯ, КУСОК ТУХЛОГО ДЕРЬМА, ПРОВАЛИСЬ В САМУЮ ВОНЮЧУЮ КАНАЛИЗАЦИЮ ВСЕЛЕННОЙ, ЖАЛКАЯ ОБЕЗЬЯНА, ВЫРОДОК ИЗ САМЫХ ГЛУБИН ПОМОЙКИ!
sec-hailer-phrase-halt = СТОЯТЬ! СТОЯТЬ! СТОЯТЬ!
sec-hailer-phrase-bobby = Именем закона, стоять.
sec-hailer-phrase-compliance = Подчинение — в ваших интересах.
sec-hailer-phrase-justice = Готовься к правосудию!
sec-hailer-phrase-running = Бегство только увеличит ваш срок.
sec-hailer-phrase-dontmove = Не двигаться, мразь!
sec-hailer-phrase-floor = Мордой в пол, мразь!
sec-hailer-phrase-robocop = Живым или мёртвым, ты пойдёшь со мной.
sec-hailer-phrase-god = Бог создал этот день для преступников, которых мы не поймали вчера.
sec-hailer-phrase-freeze = Замри, подонок!
sec-hailer-phrase-imperial = Стой, преступное отродье!
sec-hailer-phrase-bash = Стой, или я тебе врежу.
sec-hailer-phrase-harry = Давай, порадуй меня.
sec-hailer-phrase-asshole = Хватит нарушать закон, мудак.
sec-hailer-phrase-stfu = Ты имеешь право заткнуть свою пасть.
sec-hailer-phrase-shutup = Заткнись, преступность!
sec-hailer-phrase-super = Узри гнев золотой молнии.
sec-hailer-phrase-dredd = Я — ЗАКОН!

# ─── Мегафон ───

chat-speech-verb-name-megaphone = Мегафон
chat-speech-verb-megaphone-1 = кричит в мегафон
chat-speech-verb-megaphone-emagged-1 = ревёт в мегафон
megaphone-recharge = { CAPITALIZE($item) } нужно перезарядиться!
megaphone-emagged = Голосовой синтезатор перегружен.

# ─── Телескопическая дубинка ───

telebaton-clumsy = { CAPITALIZE($user) } случайно бьёт себя { $item } по голове!
telebaton-stun-others = { CAPITALIZE($user) } сбивает { $target } с ног телескопической дубинкой!
telebaton-stun-target = { CAPITALIZE($user) } сбивает вас с ног телескопической дубинкой!

# ─── Электростул ───

construction-graph-component-electropack = электропак
electric-chair-assembled = Вы прикручиваете набор шокера к { $chair }, электризуя его!
electric-chair-disassembled = Вы снимаете набор шокера.
electric-chair-armed = Слышно, как в { $chair } замыкается электрическая цепь!
electric-chair-safe = { CAPITALIZE($chair) } щёлкает размыкающейся цепью — пока он безопасен.
electric-chair-shock = { CAPITALIZE($chair) } бьёт мощным разрядом!

gulag-console-set-goal = Задать

# ─── Секвей ───

secway-stall = { CAPITALIZE($secway) } фыркает и отказывается ехать!
secway-banana-start = { CAPITALIZE($user) } начинает засовывать { $item } в выхлопную трубу { $secway }.
secway-banana-done = { CAPITALIZE($user) } засовывает { $item } в выхлопную трубу { $secway }.
secway-verb-clean = Вычистить выхлопную трубу
secway-clean-start = { CAPITALIZE($user) } начинает вычищать выхлопную трубу { $secway }...
secway-clean-done = { CAPITALIZE($user) } вычищает выхлопную трубу { $secway }.
secway-examine-banana = [color=yellow]Кажется, что-то застряло в выхлопной трубе...[/color]

# ─── Пейнтбол ───

paintball-color-blue = Вы переключаете маркер на синюю краску.
paintball-color-red = Вы переключаете маркер на красную краску.
paintball-color-green = Вы переключаете маркер на зелёную краску.
paintball-color-yellow = Вы переключаете маркер на жёлтую краску.
paintball-color-name-blue = синий
paintball-color-name-red = красный
paintball-color-name-green = зелёный
paintball-color-name-yellow = жёлтый
paintball-examine-color = Маркер настроен стрелять краской цвета: { $color }. Цвет меняется мультитулом.
paintball-examine-pepper = [color=red]Заряжен магазин перцовых шариков![/color]
paintball-examine-emagged = [color=red]Компрессор опасно искрит.[/color]
paintball-emagged = Вы перегружаете компрессор { $gun }.

# ─── Плакаты розыска ───

wanted-printer-verb = Печать плаката розыска
wanted-printer-title = Печать плакатов и досье
wanted-printer-records = Записи станции
wanted-printer-alias = Имя на плакате
wanted-printer-header = Заголовок (до 8 букв)
wanted-printer-description = Описание
wanted-printer-print-wanted = Розыск
wanted-printer-print-missing = Пропал без вести
wanted-printer-print-rapsheet = Досье
wanted-printer-status-printing = Идёт печать...
wanted-printer-busy = Принтер занят.
wanted-printer-no-crimes = У этого человека нет преступлений.
wanted-printer-printing = Печать...
wanted-printer-no-details = Подробностей нет.
wanted-header-wanted = РОЗЫСК
wanted-header-missing = ПРОПАЛ
wanted-crimes-header = РАЗЫСКИВАЕТСЯ ЗА:
wanted-crime-line = Преступление: { $crime }
wanted-rapsheet-name = досье ({ $name })
wanted-rapsheet-header = [head=2]{ $header }[/head]
wanted-rapsheet-default-header = ДОСЬЕ
wanted-rapsheet-person = Имя: { $name }
wanted-poster-name-wanted = плакат розыска
wanted-poster-name-missing = плакат о пропаже
wanted-poster-examine-header = [bold]{ $header }[/bold]
wanted-poster-placing = Вы начинаете вешать плакат...
wanted-poster-removed = Вы аккуратно снимаете плакат со стены.
wanted-poster-ripped = { CAPITALIZE($user) } срывает плакат!

# ─── Задания СБ ───

security-bounty-title = Терминал заданий СБ
security-bounty-no-pad = Пад заданий не найден — поставьте его в пределах 4 клеток.
security-bounty-status = Состояние
security-bounty-insert-id = Вставьте ID-карту.
security-bounty-id = ID
security-bounty-progress = Прогресс
security-bounty-reward = Награда СБ
security-bounty-pick = Взять задание
security-bounty-request = Новые задания
security-bounty-recalc = Проверить пад
security-bounty-send = Отправить
security-bounty-eject = Извлечь ID
security-bounty-cooldown = Попробуйте снова через { $seconds } с.
security-bounty-contraband-name = Конфискованная контрабанда
security-bounty-contraband-desc = Синдикат постоянно пытается разложить экипажи станций НТ. Отправьте нам вашу последнюю партию изъятой контрабанды: 5 предметов.
security-bounty-patrol-name = Патруль: { $area }
security-bounty-patrol-desc = Проведите плановый обход района «{ $area }» (в пределах { $radius } м от маяка). Пройдите не меньше { $meters } м по району. ID-карта будет сообщать о прогрессе.
security-bounty-progress-patrol = { $progress }/{ $needed } м
security-bounty-progress-contraband = { $progress }/{ $needed } шт.
security-bounty-status-no-id = Пожалуйста, вставьте сначала ID-карту.
security-bounty-status-no-bounty = Пожалуйста, возьмите сначала задание.
security-bounty-status-applies = Задание: цель применима.
security-bounty-status-not-applies = Задание: цель неприменима.
security-bounty-status-sending = Подготовка к отправке...
security-bounty-status-shipped = Найдено целей задания: { $count }.
security-bounty-status-nothing = Цели задания не обнаружены. Отмена.
security-bounty-status-complete = Задание выполнено! { $share } кр. зачислены на счёт СБ. Передайте куб награды в отдел снабжения.
security-bounty-cube-name = куб награды ({ $bounty })
security-bounty-patrol-started = Патруль начат. Пройдите { $meters } м по району, чтобы выполнить задание.
security-bounty-patrol-percent = Патруль выполнен на { $percent }%.
security-bounty-patrol-complete = Патруль завершён. Вернитесь к терминалу заданий за наградой.

# ─── Импланты СБ ───

security-implant-teleport-blocked = Вы чувствуете, что телепортируетесь, но вас внезапно отбрасывает туда, где вы только что были!
security-implant-exile-blocked = Имплант изгнания отклоняет перемещение — путь на станцию для вас закрыт.
security-implant-exile-shuttle = Консоль шаттла блокируется: обнаружен имплант изгнания.
security-implant-beacon-warp = Имплант-маяк: { $name }
security-implant-beacon-safe = Носитель находится в безопасной среде.
security-implant-beacon-hazard = Носитель находится в опасной среде!
security-implant-beacon-unreachable = Сигнал маяка недоступен для телепортации!
prisoner-implant-info-status = Состояние

# ─── Щит со вспышкой ───

construction-graph-component-riot-shield = щит
strobe-shield-bulb-burnt = Нет смысла менять лампочку на перегоревшую!
strobe-shield-bulb-start = Вы начинаете менять лампочку...

# ─── Пакет для улик ───

evidence-bag-desc-empty = Пустой пакет для улик.
evidence-bag-desc-filled = Пакет для улик, в котором лежит { $item }. { $desc }
evidence-bag-empty = { CAPITALIZE($bag) } пуст.
evidence-bag-emptied = Вы вытряхиваете { $bag }.
evidence-bag-emptied-others = { CAPITALIZE($user) } вытряхивает { $bag }.

# ─── Голобейдж ───

holobadge-no-id = Размахивать значком, не проведя по нему ID, довольно бессмысленно.
holobadge-display-others = { CAPITALIZE($user) } предъявляет удостоверение внутренней службы безопасности Нанотрейзен. На нём написано: { $name }, служба безопасности НТ.
holobadge-display-self = Вы предъявляете удостоверение внутренней службы безопасности Нанотрейзен. На нём написано: { $name }, служба безопасности НТ.
holobadge-rejected = { CAPITALIZE($badge) } отклоняет ваши недостаточные права доступа.
holobadge-imprinted = Вы отпечатываете данные своего ID на значке.
holobadge-name = голобейдж ({ $name })
holobadge-desc = Этот светящийся синий значок отмечает { $name } как ЗАКОН.
holobadge-thrust-others = { CAPITALIZE($user) } вторгается в личное пространство { $target }, настойчиво тыча { $badge } в лицо.
holobadge-thrust-self = Вы вторгаетесь в личное пространство { $target }, настойчиво тыча { $badge } в лицо. Вы — ЗАКОН!
holobadge-emagged = Вы проводите картой и взламываете проверку доступа голобейджа.
holobadge-already-emagged = { CAPITALIZE($badge) } уже взломан.

# ─── Перцовый баллончик ───

reagent-name-imperial-condensed-capsaicin = конденсированный капсаицин
reagent-desc-imperial-condensed-capsaicin = Химическое вещество, используемое для самообороны и полицейской работы.
reagent-effect-guidebook-pepper-spray = { $chance ->
    [1] Обжигает
   *[other] обжигает
  } глаза и слизистые: слепота и падение, если нет защиты рта и глаз
pepper-spray-burns = Глаза горят огнём! Вы ничего не видите!

# ─── Гамма-оружейка ───

gamma-armory-sender = Центральное командование
gamma-armory-deploy = Центральное командование развернуло шаттл гамма-оружейки.
gamma-armory-recall = Центральное командование отозвало шаттл гамма-оружейки.
gamma-armory-sent = Гамма-оружейка отправлена к станции.
gamma-armory-recalled = Гамма-оружейка отозвана на ЦК.
gamma-armory-in-transit = Шаттл гамма-оружейки сейчас в пути. Попробуйте чуть позже.
gamma-armory-no-station = Станция не найдена.
gamma-armory-error = Шаттл гамма-оружейки недоступен.
gamma-armory-map-name = Центральное командование: гамма-оружейка
gamma-armory-shuttle-name = Гамма-оружейка
medbeam-lost = Вы теряете контроль над лучом!
medbeam-crossed = Медлучи пересекаются и ВЗРЫВАЮТСЯ!
gamma-shield-block = Энергощит поглощает удар!
