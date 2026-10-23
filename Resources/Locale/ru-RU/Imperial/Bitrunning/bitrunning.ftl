## Битраннинг TG

bitrunning-domain-no-desc = Описание отсутствует.
bitrunning-domain-map-name = Виртуальный домен: { $domain }
bitrunning-domain-info-none = Информация отсутствует! Обратитесь за помощью при нужде.

## Сервер
bitrunning-server-ready-announcement = Отчёт квантового сервера: тепловые системы в рабочем состоянии. Переходим к настройке домена.
bitrunning-server-no-domain = домен не выбран!
bitrunning-server-stop-current = остановите текущий домен!
bitrunning-server-clients-connected = все клиенты должны отключиться!
bitrunning-server-init-failed = инициализация провалена!
bitrunning-server-domain-loaded = домен загружен.
bitrunning-server-shutting-down = отключение домена...
bitrunning-server-alerting-clients = оповещаем клиентов...
bitrunning-server-user-shutting-down = { CAPITALIZE($user) } начинает отключать сервер!
bitrunning-threat-deletion = Вы помечены на удаление! Спасибо за службу.
bitrunning-crate-unidentified = Неопознанный ящик
bitrunning-crate-is-target = Похоже, это тот самый ящик, который мы ищем!

## Консоль
bitrunning-console-redacted = ███████
bitrunning-console-scan-limited = Возможности сканирования ограничены. Не удаётся определить данные о домене.
bitrunning-console-title = Квантовая консоль
bitrunning-console-not-connected = Нет подключения к серверу!
bitrunning-console-wait = Подождите...
bitrunning-console-broadcast = Прямой эфир
bitrunning-console-broadcast-tooltip = Включить/отключить трансляцию вашего битрана на станционные развлекательные экраны.
bitrunning-console-random = Случайно
bitrunning-console-random-tooltip = Получите случайный домен для дополнительных наград. Взвешено по вашим текущим очкам. Минимум - 1 очко.
bitrunning-console-points-tooltip = Полученные очки для покупки доменов.
bitrunning-console-domains = Виртуальные домены
bitrunning-console-tab-peaceful = Мирные
bitrunning-console-tab-easy = Легкие
bitrunning-console-tab-medium = Средние
bitrunning-console-tab-hard = Сложные
bitrunning-console-load = Загрузить
bitrunning-console-loaded-button = Загружен
bitrunning-console-stop-first = Остановите текущий домен.
bitrunning-console-modular-suffix = {" "}(Модульное)
bitrunning-console-secondary-suffix = {" "}(Доступны дополнительные задачи)
bitrunning-console-ghosts-suffix = {" "}(Взаимодействие с призраками)
bitrunning-console-cost-tooltip = Требуемые очки для покупки домена.
bitrunning-console-reward-tooltip = Награда за выполнение домена.
bitrunning-console-none = Никакие
bitrunning-console-avatars = Подключенные клиенты
bitrunning-console-retries-tooltip = Пропускная способность позволяет создать новое соединение.
bitrunning-console-refresh = Обновить
bitrunning-console-refresh-tooltip = Обновить информацию аватаров.
bitrunning-console-avatar-as = { $pilot } как
bitrunning-console-none-loaded = Ничего не загружено
bitrunning-console-stop = Остановить домен
bitrunning-console-stop-tooltip = Начинает отключение. Уведомит всех подключенных.
bitrunning-tgui-confirm = Подтвердить?

## Нетпод
bitrunning-netpod-invalid-user = неверный пользователь.
bitrunning-netpod-no-server = нет подключения к серверу!
bitrunning-netpod-nothing-loaded = ничего не загружено!
bitrunning-netpod-connecting = установка соединения...
bitrunning-netpod-out-of-bandwidth = кончилась пропускная способность!
bitrunning-netpod-forced-disconnect = Вы были насильно отключены от своего аватара! Ваши мысли в смятении!
bitrunning-netpod-draining = Машина отключается и начинает сливать воду.
bitrunning-netpod-prying = { CAPITALIZE($user) } начинает вскрывать нетпод!
bitrunning-netpod-scan-on = Включить скан
bitrunning-netpod-scan-off = Выключить скан
bitrunning-netpod-scan-enabled = сканирование включено
bitrunning-netpod-scan-disabled = сканирование выключено
bitrunning-netpod-examine-no-server = Оно ни к чему не подключено. Нетподы должны стоять не дальше 4 клеток от сервера.
bitrunning-netpod-examine-help = Перетащите себя на нетпод, чтобы начать подключение. Нахождение в нетподе лечит некоторые раны. Если кто-то вмешается в работу нетпода, пользователь получит предупреждение.
bitrunning-netpod-examine-scan-on = Сканирование пользователя включено: аватар при создании будет похож на пользователя.
bitrunning-netpod-examine-scan-off = Сканирование пользователя выключено (альт-клик — включить).
bitrunning-netpod-examine-occupant = Сейчас внутри: { $occupant }.

## Голо-лестница и монитор
bitrunning-ladder-no-connection = соединение не обнаружено
bitrunning-ladder-blocked = Сущность в симуляции не даёт вам отступить. Завершите задание или устраните препятствие, чтобы безопасно выйти.
bitrunning-ladder-disconnecting = отключение...
bitrunning-host-monitor-no-data = данные не распознаны
bitrunning-host-monitor-health = Текущее здоровье хоста: { $health }%

## Сертификат
bitrunning-certificate-title = [head=1]Сертификат о прохождении домена[/head]
bitrunning-certificate-domain = [bold]{ $domain }[/bold]{ $random ->
    [1] {" "}(случайно)
   *[0] {""}
}
bitrunning-certificate-difficulty = - Сложность: { $difficulty }
bitrunning-certificate-threats = - Угрозы: { $threats }
bitrunning-certificate-base = - Начальная награда: { $points }{ $random ->
    [1] {" "}+1
   *[0] {""}
}
bitrunning-certificate-bonus = - Общий бонус: { $bonus }x
bitrunning-certificate-bonuses = [bold]Бонусы[/bold]
bitrunning-certificate-bonus-random = - Случайно: +0.2
bitrunning-certificate-bonus-multiplayer = - Множитель: +{ $bonus }
bitrunning-certificate-bonus-nohit = - Без урона: +{ $bonus }
bitrunning-certificate-bonus-threats = - Угрозы: +{ $bonus }
bitrunning-certificate-bonus-parts = - Компоненты: +{ $bonus }
bitrunning-certificate-time = Время прохождения: { $time }
bitrunning-certificate-grade = [head=2]Оценка: { $grade }[/head]

## Тревоги
alerts-bitrunning-complete-name = Домен завершён
alerts-bitrunning-complete-desc = Домен завершён. Нажмите, чтобы безопасно выйти.
alerts-bitrunning-shutdown-name = Перезагрузка домена
alerts-bitrunning-shutdown-desc = Домен перезагружается. Найдите выход.
alerts-bitrunning-crowbar-name = Нарушение работы нетпода
alerts-bitrunning-crowbar-desc = Кто-то пытается вскрыть нетпод. Найдите выход.
alerts-bitrunning-integrity-name = Целостность нарушена
alerts-bitrunning-integrity-desc = Нетпод повреждён. Найдите выход.
alerts-bitrunning-threat-name = Обнаружена угроза
alerts-bitrunning-threat-desc = Присутствуют аномалии в потоке данных.

## Домены
bitrunning-domain-breeze-bay-name = Прохладный залив
bitrunning-domain-breeze-bay-desc = Городок на берегу моря с большим лесом на севере.
bitrunning-domain-breeze-bay-help = Всё просто! Наслаждайтесь лучами, половите рыб и хорошо проведите время! Только не дайте крабам куснуть вас.
bitrunning-domain-ash-drake-name = Пепельный ад
bitrunning-domain-ash-drake-desc = Здесь обитает пепельный дрейк, могущественный дракон, который бороздит поверхность Лаваленда.
bitrunning-domain-blood-drunk-miner-name = Кровавые раскопки
bitrunning-domain-blood-drunk-miner-desc = Немногие покидают поверхность Лаваленда без парочки шрамов. Другие остаются, обезумев от охоты.
bitrunning-domain-bubblegum-name = Пропитанное кровью логово
bitrunning-domain-bubblegum-desc = Король демонов резни. Бубльгум — массивное, громоздкое чудовище, жаждущее насилия.
bitrunning-domain-colossus-name = Небесное испытание
bitrunning-domain-colossus-desc = Массивное древнее чудовище по имени Колосс. Суд грядёт.
bitrunning-domain-hierophant-name = Арена ревнителя
bitrunning-domain-hierophant-desc = Танцуйте, марионетки, танцуйте!
bitrunning-domain-xeno-nest-name = Заражение ксеноморфами
bitrunning-domain-xeno-nest-desc = Сканеры нашего корабля обнаружили формы жизни неизвестного происхождения. Дружественные попытки связаться с ними не увенчались успехом.
bitrunning-domain-xeno-nest-help = Вы находитесь на бесплодной планете, наполненной враждебными существами. Где-то здесь ящик — он не спрятан, но находится под охраной. Ожидайте сопротивления.
bitrunning-domain-clown-planet-name = Планета клоунов
bitrunning-domain-clown-planet-desc = В самых тёмных уголках космоса есть только Хонк.
bitrunning-domain-clown-planet-help = Испытания Хонкости начались. Вдали раздаются звуки велосипедных гудков. Этот мир — своего рода головоломка — существует в легендах как последняя проверка вашей смешнявости.
bitrunning-domain-vaporwave-name = Космический пережиток
bitrunning-domain-vaporwave-desc = Зависшая в безмолвной пустоте космоса Неоновая реликвия — призрачное эхо ретрофутуристической эпохи. Отдохните и насладитесь видом.

## Нетпод: снаряжение аватара
bitrunning-netpod-ui-title = Нетпод
bitrunning-netpod-ui-select = Выберите образ
bitrunning-netpod-ui-search = Поиск
bitrunning-netpod-ui-jobs = Профессии
bitrunning-netpod-ui-none = Нет

## Торгомат NexaCache
bitrunning-vendor-no-account = Банковский счёт не обнаружен!
bitrunning-vendor-express-done = Благодарим вас за покупку!
bitrunning-vendor-cargo-done = Благодарим вас за покупку! Товар прибудет следующим грузовым рейсом!
bitrunning-vendor-announce = Битраннер заказал оборудование, которое прибудет на грузовом шаттле! Пожалуйста, убедитесь, что оно будет доставлено как можно скорее!
bitrunning-vendor-order-reason = Заказ NexaCache
bitrunning-vendor-orderer-rank = Поставщик битрана
bitrunning-vendor-category-flair = Разное
bitrunning-vendor-category-tech = Технологии
bitrunning-vendor-credit = np
bitrunning-vendor-shopping = Покупки
bitrunning-vendor-checkout = Оплата
bitrunning-vendor-condense = Сжать
bitrunning-vendor-expand = Расширить
bitrunning-vendor-search = Поиск предмета...
bitrunning-vendor-cost-tooltip = Стоимость { $cost } { $credit } заказа
bitrunning-vendor-list = Список покупок:
bitrunning-vendor-nothing = Ничего!
bitrunning-vendor-nothing-hint = (Закажем чего, а?)
bitrunning-vendor-purchase = Покупки: { $cost }
bitrunning-vendor-express = Экспресс: { $cost }
bitrunning-vendor-min-total = Общая сумма должна быть больше или равна { $value }
bitrunning-vendor-need-item = Закажите хотя бы 1 предмет
bitrunning-vendor-purchase-tooltip = Ваши покупки прибудут в отдел снабжения, можете понадеятся, что ваши коллеги доставят их вам. На 35% дешевле, чем экспресс-доставка.
bitrunning-vendor-express-tooltip = Отправляет ваши покупки мгновенно.
bitrunning-vendor-order-sent = Заказ отправлен! Машина перезаряжается...

bitrunning-order-medkit-brute = Не корите себя, это всего лишь игра!
bitrunning-order-medkit-burn = Отлично подходит после жарких игровых сессий.
bitrunning-order-item-tier1 = Программа: медицинский луч, взрывчатка C4 или коробка пиццы.
bitrunning-order-item-tier2 = Программа: люкс-инжектор, пистолет или бронежилет.
bitrunning-order-item-tier3 = Программа: продвинутый лазер, двойной энергомеч или мини-бомба.
bitrunning-order-ability-tier1 = Программа: «Призыв сыра» или «Малое исцеление».
bitrunning-order-ability-tier2 = Программа: огненный шар, удар молнии или силовая стена.
bitrunning-order-ability-tier3 = Программа: облик малого пепельного дракона или белого медведя.
bitrunning-order-gimmick-sports = Тематический «спортивный» набор экипировки.
bitrunning-order-gimmick-dungeon = Тематический «подземельный» набор экипировки.
bitrunning-order-pka = Модули протокинетического акселератора (ПКА в комплект не входит).
bitrunning-order-pka-premium = Продвинутые модули протокинетического акселератора.
bitrunning-order-pkc = Трофеи протокинетического крушителя (крушитель в комплект не входит).
bitrunning-order-pkc-premium = Продвинутые трофеи протокинетического крушителя.

## Диски
bitrunning-disk-examine-carry = Этот диск должен быть у вас при себе, когда вы входите в нетпод.
bitrunning-disk-examine-choose = Выберите программу через меню действий (ПКМ).
bitrunning-disk-examine-chosen = Выбрано: [bold]{ $choice }[/bold]. Изменить нельзя.
bitrunning-disk-chosen = выбрано
bitrunning-disk-domain-forbids = Этот домен запрещает часть внешних программ — некоторые диски не будут активны!
bitrunning-disk-load-failed = Как минимум одна программа не загрузилась. Проверьте, нет ли повторов или невыбранных дисков.
bitrunning-disk-load-blocked = Как минимум одна программа заблокирована ограничениями домена.
bitrunning-spell-heal = Вы чувствуете себя немного лучше.

bitrunning-option-medbeam = Медицинский луч
bitrunning-option-c4 = Взрывчатка C4
bitrunning-option-pizza = Коробка пиццы
bitrunning-option-luxury-medipen = Люкс-инжектор выживания
bitrunning-option-pistol = Пистолет
bitrunning-option-vest = Бронежилет
bitrunning-option-advanced-laser = Продвинутый лазер
bitrunning-option-dualsaber = Двойной энергомеч
bitrunning-option-minibomb = Мини-бомба Синдиката
bitrunning-option-cheese = Призыв сыра
bitrunning-option-lesser-heal = Малое исцеление
bitrunning-option-fireball = Огненный шар
bitrunning-option-lightning = Удар молнии
bitrunning-option-forcewall = Силовая стена
bitrunning-option-dragon = Облик пепельного дракона
bitrunning-option-bear = Облик белого медведя
bitrunning-option-pka-range = Модуль дальности
bitrunning-option-pka-damage = Модуль урона
bitrunning-option-pka-cooldown = Модуль скорострельности
bitrunning-option-pka-aoe = Модуль урона по площади
bitrunning-option-pka-passthrough = Модуль сквозь людей
bitrunning-option-pka-repeater = Модуль-повторитель
bitrunning-option-pka-lifesteal = Модуль кражи жизни
bitrunning-option-pka-resonator = Модуль резонатора
bitrunning-option-pka-bounty = Модуль награды
bitrunning-option-pka-indoors = Модуль для помещений
bitrunning-option-trophy-watcher = Крыло наблюдателя
bitrunning-option-trophy-magma = Магмовое крыло
bitrunning-option-trophy-legion = Череп легиона
bitrunning-option-trophy-wolf = Волчье ухо
bitrunning-option-trophy-icewing = Ледяное крыло
bitrunning-option-trophy-tubes = Трубки бластера
bitrunning-option-trophy-eye = Глаз шахтёра
bitrunning-option-trophy-spike = Шип хвоста
bitrunning-option-trophy-claws = Когти демона
bitrunning-option-trophy-vortex = Талисман вихря
bitrunning-option-trophy-icecube = Ледяной куб демона
bitrunning-option-boxer = Боксёр
bitrunning-option-skater = Скейтер
bitrunning-option-archer = Лучник
bitrunning-option-fisher = Рыбак
bitrunning-option-gamer = Геймер
bitrunning-option-alchemist = Алхимик
bitrunning-option-rogue = Разбойник
bitrunning-option-healer = Лекарь
bitrunning-option-wizard = Волшебник


## Угрозы
bitrunning-glitch-cyber-police-name = Сбой: киберполицейский
bitrunning-glitch-cyber-tac-name = Сбой: кибертактик
bitrunning-glitch-netguardian-name = Сбой: нетгардиан
bitrunning-glitch-desc = Временная роль антагониста в виртуальном домене битраннеров. По окончании вы вернётесь к призракам.
bitrunning-glitch-rules = Вы — защитная программа домена. Уничтожьте нарушителей-битраннеров в домене. Если квантовый сервер взломан, встаньте на клетку расшифровки, чтобы прорваться на станцию.
bitrunning-threat-out-of-bounds = вне границ!
bitrunning-server-emagged = система взломана...
bitrunning-breach-warning = ПРЕДУПРЕЖДЕНИЕ КВАНТОВОГО СЕРВЕРА: обнаружено нарушение безопасности. Выполняется несанкционированная последовательность доступа…
bitrunning-breach-done = ТРЕВОГА КВАНТОВОГО СЕРВЕРА: незарегистрированная сущность материализована на станции.
bitrunning-breach-criminals = Здесь ещё { $count } нарушителей, грабящих ваш домен. Пока они живы, выбраться сложнее.
bitrunning-netguardian-target = цель захвачена.
alerts-bitrunning-breach-name = Нарушение охранной системы
alerts-bitrunning-breach-desc = Враждебное существо проникает в убежище. Найдите выход.

## Домены Б4
bitrunning-domain-abductor-ship-name = Корабль абдукторов
bitrunning-domain-abductor-ship-desc = Высадитесь на корабль абдукторов и заберите их ценности.
bitrunning-domain-abductor-ship-help = Материнский корабль абдукторов непреднамеренно вошёл во враждебное окружение. Сейчас они готовятся покинуть зону, прихватив своё снаряжение и добычу, включая ящик. Будьте осторожны: абдукторы известны продвинутым вооружением.
bitrunning-domain-beach-bar-name = Бар на пляже
bitrunning-domain-beach-bar-desc = Весёлая пляжная обитель, где дружелюбные скелеты подают напитки. Ребят, а как вы погибли?
bitrunning-domain-beach-bar-help = Это место работает на скелетной команде, и они не очень хотят делиться информацией. Может быть, парочка рюмочек жидкого очарования поднимет им настроение. Как говорится, если не можешь их победить — присоединись к ним!
bitrunning-domain-pizza-party-name = Вечеринка с пиццей
bitrunning-domain-pizza-party-desc = Знаменитая пиццерия, которая разорилась из-за того, что могла предложить.
bitrunning-domain-pizza-party-help = Бери фонарик и отправляйся в путь. Твой любимый медведь уже заждался тебя...
bitrunning-domain-grasslands-hunt-name = Луговая охота
bitrunning-domain-grasslands-hunt-desc = Мирная охота в дикой местности.
bitrunning-domain-grasslands-hunt-help = Как охотник, вы должны уметь выслеживать и убивать свою добычу. Проявите себя.
bitrunning-domain-heretic-hunt-name = Еретическая охота
bitrunning-domain-heretic-hunt-desc = Предай своего ближнего, чтобы получить абсолютную силу.
bitrunning-domain-heretic-hunt-help = Еретики требуют жертвоприношений для своих тёмных ритуалов — приносите трупы на руну! Трупы членов экипажа более высокого ранга ценнее и могут нести полезное снаряжение.
bitrunning-domain-island-brawl-name = Островная потасовка
bitrunning-domain-island-brawl-desc = Мирный остров, спрятанный в богом забытом месте. Уровень завершится сам после определённого числа смертей.
bitrunning-domain-island-brawl-help = По всему острову могут быть назначены награды, но главная цель — выжить. Смерти на острове засчитываются в итоговый счёт.
bitrunning-domain-meta-central-name = ИСН Цереброн-централ
bitrunning-domain-meta-central-desc = Время от времени работники требуют соблюдения прав от Нанотрейзен. Это невыгодно.
bitrunning-domain-meta-central-help = Отвечайте на требования работников санкционированным насилием. Соберите ценные материалы, которые могут быть разбросаны вокруг. Помните, чему вас учили: все виновны по умолчанию.
bitrunning-domain-pipedream-name = Завод труб отправки
bitrunning-domain-pipedream-desc = Заброшенный и заражённый завод по производству труб отправки.
bitrunning-domain-pipedream-help = Не так давно здесь кипела бурная деятельность. Рабочие, похоже, уходили в спешке, и производительность теперь на нуле. Что-то, должно быть, разгромило это место, но что?
bitrunning-domain-pirates-name = Бухта корсаров
bitrunning-domain-pirates-desc = Пробейтесь с боем к спрятанным сокровищам, захватите добычу и поспешите сбежать, прежде чем пираты переломят ситуацию.
bitrunning-domain-pirates-help = Переоденьтесь в предоставленную одежду, чтобы слиться с толпой, а затем сразитесь с враждебными пиратами. Хватайте сокровища и выбирайтесь, пока вас не одолели!
bitrunning-domain-psyker-shuffle-name = Хаос из ящиков
bitrunning-domain-psyker-shuffle-desc = Проберитесь в заброшенный уголок виртуального мира, где хранятся все ящики. Внимание — домен почти не освещён.
bitrunning-domain-psyker-shuffle-help = Здесь темно. Не забывайте переходить на шаг и внимательно осматривать каждый встреченный ящик.
bitrunning-domain-psyker-zombies-name = Заражённый домен
bitrunning-domain-psyker-zombies-desc = Ещё один заброшенный уголок виртуального мира, брошенный из-за зомби-вируса. Внимание — домен почти не освещён.
bitrunning-domain-psyker-zombies-help = Этот некогда любимый домен был повреждён вирусом: он нестабилен, полон дыр и ЗОМБИ! Где-то поблизости должно быть оружие. Вооружитесь и закончите начатое киберполицией!
bitrunning-domain-stairs-name = Ледниковый откос
bitrunning-domain-stairs-desc = Коварный подъём, который осилят немногие. Зато это отличное кардио.
bitrunning-domain-stairs-help = Слышали когда-нибудь о «Змеях и лестницах»? Это то же самое, но вместо лестниц — подъёмы, а вместо змей — крутой спуск с обрыва.
bitrunning-domain-syndicate-assault-name = Нападение Синдиката
bitrunning-domain-syndicate-assault-desc = Возьмите на абордаж вражеский корабль и верните украденный груз.
bitrunning-domain-syndicate-assault-help = Группа оперативников Синдиката похитила со станции ценный груз. Они поднялись на борт своего корабля и пытаются сбежать. Проникните на корабль и верните ящик. Будьте осторожны: они очень хорошо вооружены.
bitrunning-domain-wendigo-name = Ледяной пожиратель
bitrunning-domain-wendigo-desc = Легенды гласят о хищных вендиго, скрытых в глубине пещер Айсмуна.

bitrunning-ritual-complete = ритуал завершён
bitrunning-sacrifice-accepted = жертвоприношение принято
bitrunning-option-mini-uzi = Мини-узи

## Гост-роли доменов
bitrunning-ghost-actor-rules = Вы — существо виртуального домена. Вы вернётесь к призракам, когда домен будет сброшен. Защищайте свой домен от нарушителей!
bitrunning-ghost-beach-name = Виртуальный пляжный бомж
bitrunning-ghost-beach-desc = Ты, типа, полностью виртуальная имитация чувака, братан. Приехал на весенние каникулы оттянуться.
bitrunning-ghost-lifeguard-name = Виртуальный спасатель
bitrunning-ghost-lifeguard-desc = От вас зависит, чтобы никто не утонул и не был съеден вредоносными программами.
bitrunning-ghost-bartender-name = Виртуальный пляжный бармен
bitrunning-ghost-bartender-desc = Обеспечьте поступление виртуальных напитков и помогите чувакам имитировать опьянение.
bitrunning-ghost-islander-name = Островитянин
bitrunning-ghost-islander-desc = Не позволяйте никому разрушить ваше идеальное место для отдыха. Договаривайтесь с другими... или нет!
bitrunning-ghost-pirate-name = Виртуальный пират
bitrunning-ghost-pirate-desc = Какая-то сухопутная крыса охотится за вашими сокровищами. Остановите их! Яррр!
bitrunning-ghost-syndie-name = Виртуальный оперативник Синдиката
bitrunning-ghost-syndie-desc = Ревут сигналы тревоги! Нас берут на абордаж!

bitrunning-bodycam-name = Аватар { $name }

# AvatarHelp
bitrunning-help-title = Информация о домене
bitrunning-help-welcome = Добро пожаловать в виртуальный домен.
bitrunning-help-search-title = Поиск
bitrunning-help-search = Изучите местность и принесите в убежище ящик. Обращайте пристальное внимание на информацию о домене и на контекстные подсказки.
bitrunning-help-cache-title = Ящик
bitrunning-help-cache = Принесите ящик к обозначенной зоне в вашем убежище. Место отправки будет выглядеть необычно для окружения. Осмотрите убежище, чтобы найти его.
bitrunning-help-disconnect-title = Отключение
bitrunning-help-disconnect = Лестница предоставляет самый безопасный способ выхода из домена, если ящик еще не вытащили. При разрыве соединения, ваш нетпод предоставляет ограниченную возможность к реанимации.
bitrunning-help-safety-title = Безопасность
bitrunning-help-safety = Пока вы подключены, вы в какой-то мере защищены от опасностей окружающей среды и опасности извне, но не полностью. Обращайте внимания на оповещения и тревоги.
bitrunning-help-retries-title = Ограниченные попытки
bitrunning-help-retries = Создание аватаров требует огромной пропускной способности. Не тратьте их впустую.
bitrunning-help-danger-title = Реальная опасность
bitrunning-help-danger = Помните, что вы физически связаны с вашим аватаром. Вы - чужой во враждебном окружении. Оно попытается силой изгнать вас.

# Цели домена
bitrunning-objective-issuer = Виртуальный домен
bitrunning-objective-cache-find = Найдите зашифрованный тайник
bitrunning-objective-cache-boss = Победите стража домена
bitrunning-objective-cache-points = Наберите очки для тайника
bitrunning-objective-cache-generic = Исследуйте домен и найдите зашифрованный тайник.
bitrunning-objective-cache-points-count = Очки тайника: { $points } из { $goal }.
bitrunning-objective-cache-found = Тайник найден — несите его в убежище.
bitrunning-objective-cache-spawned = Тайник появился — найдите его и несите в убежище.
bitrunning-objective-cache-delivered = Тайник доставлен.
bitrunning-objective-deliver-desc = Принесите зашифрованный тайник на точку отправки в убежище — клетки, необычные для окружения. Тайник нельзя открыть, его расшифрует байтфорж. После доставки можно безопасно выйти через голо-лестницу или тревогу «Домен завершён».
bitrunning-objective-deliver-done = Тайник доставлен, награда ждёт на байтфорже. Выходите через голо-лестницу или тревогу «Домен завершён».
bitrunning-objective-curiosity-desc = В домене спрятаны диковинки. Доставьте их на ту же точку отправки — за каждую байтфорж выдаст дополнительную награду. Доставлено: { $count } из { $total }.

# Что делать на локации (цель «добыть тайник»)
bitrunning-domain-breeze-bay-objective = Возьмите удочку в убежище и рыбачьте с пирса. Каждая пойманная рыба приносит очки — когда их наберётся достаточно, на пляже появится тайник. Остерегайтесь крабов.
bitrunning-domain-ash-drake-objective = Пройдите через лавовые поля и убейте пепельного дракона. После его смерти на месте боя выпадет зашифрованный тайник.
bitrunning-domain-blood-drunk-miner-objective = Найдите и убейте кровожадного шахтёра — он быстро бегает и бьёт крушителем. Тайник выпадет из его останков.
bitrunning-domain-bubblegum-objective = Найдите в кровавом логове Баблгама и убейте его. Он прыгает сквозь лужи крови — не стойте в них. Тайник выпадет на месте боя.
bitrunning-domain-colossus-objective = Поднимитесь к Колоссу и уничтожьте его, уворачиваясь от залпов. Тайник выпадет из его останков.
bitrunning-domain-hierophant-objective = Войдите на арену Иерофанта и победите его. Держитесь подальше от светящихся клеток — по ним бьёт магия. Тайник выпадет на арене.
bitrunning-domain-wendigo-objective = Найдите древнего хищника ледяной пещеры и убейте его. Тайник выпадет из его останков.
bitrunning-domain-xeno-nest-objective = Тайник не спрятан, но охраняется: пробейтесь через гнездо ксеноморфов вглубь улья и заберите его.
bitrunning-domain-clown-planet-objective = Пройдите испытания планеты клоунов: головоломки, ловушки и хонк. Тайник ждёт в конце пути.
bitrunning-domain-vaporwave-objective = Мирный домен — опасности нет. Прогуляйтесь по пережитку прошлого и найдите тайник.
bitrunning-domain-abductor-ship-objective = Проникните на корабль абдукторов, справьтесь с экипажем и найдите тайник среди их добычи на борту.
bitrunning-domain-beach-bar-objective = Пейте напитки в баре: каждый глоток приносит очки. Когда их наберётся достаточно, бармены раскроют тайник.
bitrunning-domain-pizza-party-objective = Возьмите фонарик и обыщите тёмную пиццерию. Тайник лежит в одном из помещений — не попадитесь медведю.
bitrunning-domain-grasslands-hunt-objective = Выслеживайте и убивайте дичь на лугах: каждая добыча приносит очки. Когда их наберётся достаточно, появится тайник.
bitrunning-domain-heretic-hunt-objective = Убивайте членов экипажа и приносите тела на большую руну. Командиры ценнее. Когда очков жертв хватит, у руны появится тайник.
bitrunning-domain-island-brawl-objective = Выживите на острове: каждая смерть на нём приносит очки. Когда их наберётся достаточно, появится тайник. Диковинки разбросаны по острову.
bitrunning-domain-meta-central-objective = Пройдите по станции, разберитесь с враждебными работниками и найдите тайник в одном из отделов.
bitrunning-domain-pipedream-objective = Обыщите брошенный завод труб отправки и найдите тайник. Выясните, что здесь всё разгромило, — и не дайте ему разгромить вас.
bitrunning-domain-pirates-objective = Переоденьтесь в выданную одежду, разберитесь с пиратами и заберите их сокровище — тайник спрятан в бухте или на корабле.
bitrunning-domain-psyker-shuffle-objective = В темноте осматривайте каждый ящик: тайник прячется среди обычных ящиков.
bitrunning-domain-psyker-zombies-objective = Найдите оружие, пробейтесь через зомби и отыщите тайник в разрушенном домене.
bitrunning-domain-stairs-objective = Поднимайтесь по ледяным склонам и не падайте с обрывов — тайник ждёт на вершине. По пути можно найти диковинку.
bitrunning-domain-syndicate-assault-objective = Проберитесь на корабль Синдиката, разберитесь с оперативниками и верните украденный груз — тайник на борту.
