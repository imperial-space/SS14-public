## Битраннинг TG

bitrunning-domain-no-desc = Описание отсутствует.
bitrunning-domain-map-name = Виртуальный домен: { $domain }
bitrunning-domain-info-none = У этого домена нет подсказки.

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
bitrunning-console-not-connected = Квантовый сервер не найден. Поставьте сервер вплотную к консоли.
bitrunning-console-points = Очки: { $points }
bitrunning-console-ready = Статус: { $ready ->
    [true] готов
   *[false] остывает
}
bitrunning-console-loaded = Загружен: { $domain }
bitrunning-console-none-loaded = Домен не загружен
bitrunning-console-retries = Осталось подключений: { $retries }
bitrunning-console-occupants = Подключено: { $count }
bitrunning-console-random = Случайный домен
bitrunning-console-stop = Остановить домен
bitrunning-console-refresh = Обновить
bitrunning-console-load = Загрузить
bitrunning-console-cost = Стоимость: { $cost }
bitrunning-console-reward = Награда: { $reward }
bitrunning-console-difficulty = Сложность: { $difficulty ->
    [0] нет
    [1] низкая
    [2] средняя
   *[3] высокая
}
bitrunning-console-modular = модульный
bitrunning-console-secondary = есть побочные цели
bitrunning-console-avatars = Аватары
bitrunning-console-avatar = { $name } (пилот { $pilot }): здоровье { $health }, грубый { $brute }, ожоги { $burn }, токсины { $tox }, удушье { $oxy }
bitrunning-console-domains = Домены

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
bitrunning-netpod-outfit-category = Снаряжение аватара
bitrunning-netpod-outfit-chosen = Аватар будет одет как: { $job }

## Торгомат NexaCache
bitrunning-vendor-title = NexaCache
bitrunning-vendor-no-account = Счёт не найден: держите ID-карту или КПК с NT Pay.
bitrunning-vendor-holder = Счёт: { $holder }
bitrunning-vendor-points = Очки битраннинга: { $points }
bitrunning-vendor-not-enough = Недостаточно очков битраннинга.
bitrunning-vendor-express-done = Благодарим за покупку! Товар материализован.
bitrunning-vendor-cargo-done = Благодарим за покупку! Товар прибудет следующим грузовым рейсом.
bitrunning-vendor-express = Экспресс ({ $cost })
bitrunning-vendor-cargo = Рейсом ({ $cost })
bitrunning-vendor-category-flair = Атрибутика
bitrunning-vendor-category-tech = Программы

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
