## Оборона Кадии: правило войны, роли, объявления.

cadia-sender-imperium = Командование Кадии
cadia-sender-chaos = Голос Разорителя
cadia-sender-war = Кадия
cadia-side-imperium = Империум
cadia-side-chaos = Хаос
cadia-ghost-role = Война за Кадию

cadia-rules-imperium = Вы — защитник Кадии. Сражайтесь только с силами Хаоса, слушайте командиров и комиссаров, не убивайте своих. Кадия стоит!
cadia-rules-chaos = Вы — слуга Тёмных богов в 13-м Чёрном крестовом походе. Сражайтесь только с защитниками Кадии, слушайте лорда и апостола, не убивайте своих. Кадия падёт!

cadia-brief-title-imperium = { $name } — { $role }. Кадия стоит, пока стоит гвардия!
cadia-brief-title-chaos = { $name } — { $role }. Око раскрылось — Кадия падёт!

## Фазы

cadia-phase-preparation-imperium = Флот Абаддона вышел из Ока Ужаса. Всем подразделениям — занять позиции у пилонов и цитадели Касра, рыть окопы, ставить укрепления. Пока идёт подготовка, пилоны держат завесу: оружие молчит, а посреди поля стоит стена бурана — враг уже высаживается на восточной пустоши, но к нам не пройдёт, пока она не рассеется. У вас пятнадцать минут.
cadia-phase-preparation-chaos = Тринадцатый Чёрный крестовый поход начался. Пусковые шахты открыты: вставайте отделением у шахты на десантной палубе флагмана, правый клик — выбор зоны, и когтистая капсула унесёт вас на пустошь. Пока держится завеса пилонов, оружие молчит, а посреди поля стоит стена бурана — готовьте плацдарм за ней. С началом обстрела буря рассеется. Бомбардировщики спускаются только во время обстрела.
cadia-phase-bombardment-imperium = Воздушная тревога! Флот Хаоса на орбите, начинается обстрел. Всем в укрытия, держать позиции!
cadia-phase-bombardment-chaos = Корабли Чёрного легиона жгут Кадию с орбиты. Выдвигайтесь на рубежи атаки, пока гвардия прячется в норах.
cadia-phase-assault-imperium = Поле пилонов пало! Хаос идёт на штурм. Защищайте пилоны и цитадель любой ценой. Вторая рота Ультрамаринов высаживается на подмогу. Кадия стоит!
cadia-phase-assault-chaos = Поле пало — пилоны уязвимы! Рушьте обелиски, берите цитадель Касра. Во славу Тёмных богов!
cadia-phase-finale-imperium = Абаддон Разоритель высаживается лично вместе с Избранными. Продержитесь до конца — и Кадия выстоит!
cadia-phase-finale-chaos = Магистр войны ступил на Кадию. Разоритель и его Избранные с вами — сокрушите последних защитников!

## Волны

cadia-wave-imperium = Подкрепления прибыли: { $count } { $count ->
    [one] место
    [few] места
   *[other] мест
} в строю.
cadia-wave-chaos = Из варпа идёт новая орда: { $count } { $count ->
    [one] еретик
    [few] еретика
   *[other] еретиков
}.

## Пилоны и цитадель

cadia-pylon-fallen-imperium = Пилон { $number } пал! Осталось пилонов: { $left }. Варп становится сильнее.
cadia-pylon-fallen-chaos = Пилон { $number } обрушен! Осталось: { $left }. Пелена истончается!
cadia-citadel-warning-imperium = Тревога! Враг в цитадели Касра — захвачено { $percent }%. Выбить их!
cadia-citadel-warning-chaos = Цитадель Касра пала на { $percent }%. Держитесь!

## Победа

cadia-win-imperium-held = Кадия выстояла до конца штурма.
cadia-win-chaos-pylons = Все пилоны Кадии обрушены.
cadia-win-chaos-citadel = Цитадель Касра пала.
cadia-win-admin = Так решила воля Императора... или богов.
cadia-win-imperium-boss = Принц демонов изгнан обратно в варп, и Чёрный крестовый поход дрогнул.
cadia-win-exterminatus = Инквизиция предала Кадию огню. Планета мертва, но Хаосу она не досталась.
cadia-exterminatus-announce = ЭКСТЕРМИНАТУС ОБЪЯВЛЕН. Флот начнёт очищение поверхности через одну минуту. Император, прости нас.
cadia-exterminatus-begin = Инквизитор поднимает розетту и начинает обряд Экстерминатуса!

## Вооружение шаттлов и парашюты
cadia-ship-console-busy = За постом уже сидит стрелок.
cadia-ship-console-examine = На борту пушек: { $guns }, бомбоотсеков: { $bays }.
cadia-ship-gunner-enter = Вы за постом стрелка. Мышь — наводка, ЛКМ — огонь, действия — выбор пушки и сброс бомб. Отойдёте — пост будет брошен.
cadia-ship-gun-loaded = Боекомплект пушки: { $ammo }/{ $max }.
cadia-ship-gun-examine = Боекомплект: { $ammo }/{ $max }. Пополняется ящиком зенитных снарядов.
cadia-ship-bomb-not-air = Бомбить можно только из неба над полем боя.
cadia-ship-bomb-dropped = Сброшено бомб: { $count }. Осталось: { $left }.
cadia-ship-bomb-empty = Бомбоотсеки пусты или ещё не готовы.
cadia-ship-bay-full = Бомбоотсек полон.
cadia-ship-bay-loaded = В отсеке бомб: { $bombs }/{ $max }.
cadia-ship-bay-examine = В отсеке бомб: { $bombs }/{ $max }.
cadia-ship-hud-guns = Пушек: { $guns }, снарядов: { $ammo }
cadia-ship-hud-selected = Огонь: { $gun }, снарядов { $ammo }
cadia-ship-hud-selected-all = Огонь: все пушки
cadia-ship-no-guns = На борту нет пушек.
cadia-ship-gun-selected = Выбрана пушка { $number } из { $total }: { $gun }.
cadia-ship-gun-selected-all = Выбраны все пушки ({ $total }).
cadia-ship-hud-bombs = Отсеков: { $bays }, бомб: { $bombs }
cadia-chute-spent = Парашют уже использован.
cadia-chute-not-air = Прыгать можно только с неба над полем боя.
cadia-chute-open = Купол раскрылся! Рулите стропами, пока не коснётесь земли.
cadia-chute-landed = Вы на земле.
cadia-chute-examine-ready = Парашют уложен и готов.
cadia-chute-examine-spent = Парашют использован.

## Окопы и эвакуация
cadia-trench-occupied = Здесь не выкопать.
cadia-medic-call = Тяжелораненый: { $name }, { $dir }, { $meters } м!
cadia-dir-north = на севере
cadia-dir-northeast = на северо-востоке
cadia-dir-east = на востоке
cadia-dir-southeast = на юго-востоке
cadia-dir-south = на юге
cadia-dir-southwest = на юго-западе
cadia-dir-west = на западе
cadia-dir-northwest = на северо-западе
cadia-depot-shovel = Сапёрная лопатка
cadia-depot-steel = Листы стали
cadia-depot-stretcher = Носилки

## Атмосфера
cadia-prince-arrived = Сама реальность кричит: ПРИНЦ ДЕМОНОВ ВЫШЕЛ ИЗ ВАРПА! Изгоните его, и поход дрогнет.

## Снабжение
cadia-depot-verb = { $name } ({ $cost })
cadia-depot-no-points = Депо пока нечего выдать.
cadia-depot-dropping = Заказ принят: груз идёт на парашюте, отойдите от тени!
cadia-depot-examine = Запасов: { $points }/{ $max }. Пополняются со временем. ПКМ — заказать.
cadia-depot-ammo = Ящик боеприпасов
cadia-depot-shells = Ящик снарядов
cadia-depot-flak = Ящик зенитных снарядов
cadia-depot-bombs = Ящик авиабомб
cadia-depot-medical = Медицинский ящик
cadia-depot-parachute = Парашютный ранец
cadia-depot-charger = Зарядная станция
cadia-depot-bolts = Цинк болтов

## Мораль
cadia-morale-shaken = Вас бьёт дрожь. Держаться всё труднее.
cadia-morale-broken = Страх сковывает вас! Руки не слушаются!
cadia-morale-panic = ПАНИКА! Вы падаете и закрываете голову!
cadia-morale-panic-others = { $name } падает в панике!
cadia-morale-steady = Вы снова взяли себя в руки.
cadia-morale-shaking = Руки трясутся, палец срывается со спуска!
cadia-morale-hud-steady = Мораль { $value }%
cadia-morale-hud-shaken = Мораль { $value }%: шаток
cadia-morale-hud-broken = Мораль { $value }%: сломлен

## Наезд
cadia-vehicle-run-over = Вас сбивает { $vehicle }!

## Укрепления
cadia-vehicle-verb-gunpost = Встать к орудию
cadia-vehicle-entered-gunpost = Вы у орудия «{ $vehicle }». Мышь — наводка, ЛКМ — огонь.
cadia-vehicle-weapon-atcannon = Противотанковая пушка
cadia-vehicle-weapon-mortar = Миномёт
cadia-supply-empty = Здесь больше ничего не осталось.
cadia-supply-need-gun = Возьмите в руки оружие, к которому нужна обойма.
cadia-supply-examine = Запас: { $charges }.
cadia-heal-done = Раны промыты и стянуты.

cadia-pillbox-grenade = { $name } закидывает гранату в амбразуру!

## Оружие
cadia-weapon-gets-hot = { $gun } перегревается и обжигает вас!
cadia-pack-examine = Заряд: { $shots }/{ $max }.

## Псайкеры
cadia-psy-silenced = Вы отрезаны от варпа!
cadia-psy-nulled = Рядом неприкасаемый: варп молчит.
cadia-psy-no-charge = Не хватает варп-заряда.
cadia-psy-perils = Опасности варпа! Сила вырывается из-под контроля!
cadia-psy-foresight = Предвидение: врагов на голокарте — { $count }.
cadia-psy-not-psyker = Цель не псайкер.
cadia-psy-suppressed = Что-то перекрыло вам путь в варп!
cadia-psy-hud-charge = Варп-заряд { $charge }/{ $max }
cadia-psy-hud-strain = Напряжение { $strain }%
cadia-psy-hud-silenced = ПОДАВЛЕН

## Демоны
cadia-summon-verb = { $name } (варп: { $cost })
cadia-summon-locked = Откроется, когда падёт половина пилонов или начнётся развязка.
cadia-summon-no-warp = Не хватает силы варпа.
cadia-summon-bloodletter = Кровопускатель
cadia-summon-plaguebearer = Чумоносец
cadia-summon-pink-horror = Розовый ужас
cadia-summon-daemonette = Демонетка
cadia-summon-heldrake = Хелдрейк
cadia-summon-prince = Принц демонов
cadia-daemon-ghost-name = Демон Хаоса
cadia-daemon-ghost-desc = Вас вырвали из варпа ради битвы за Кадию. Убивайте защитников и ломайте пилоны.

## Космодесант и вера
cadia-armor-no-carapace = Без чёрного панциря этот доспех не надеть.
cadia-reductor-alive = Прогеноид забирают только у павшего.
cadia-reductor-empty = Прогеноид уже извлечён.
cadia-reductor-done = Геносемя извлечено.
cadia-seed-sacrificed = Геносемя сгорает в варпе: сила варпа +{ $warp }.
cadia-seed-consecrated = Геносемя освящено: в следующей волне прибудет новый брат.
cadia-mark-khorne = Кхорн принял вас: ваши удары тяжелее.
cadia-mark-nurgle = Нургл принял вас: ваша плоть почти не чувствует ран.
cadia-mark-tzeentch = Тзинч принял вас: щит варпа поглощает удар раз в 30 секунд.
cadia-mark-slaanesh = Слаанеш принял вас: вы быстрее смертных.
cadia-mark-ward = Щит варпа поглощает удар!

## Новые роли
cadia-rank-fleet-captain = Капитан
cadia-rank-fleet-gunner = Канонир
cadia-rank-void-tech = Техник
cadia-rank-inquisitor = Инквизитор
cadia-rank-silence = Сестра
cadia-rank-possessed = Одержимый
cadia-rank-obliterator = Облитератор
cadia-role-fleet-captain = Капитан крейсера
cadia-role-fleet-captain-desc = Командует ударным крейсером на орбите и его батареями.
cadia-role-fleet-captain-brief = Держите пост флота в бункере под цитаделью Касра и наводите батареи крейсера вместе с командованием. Без астропата залпы запаздывают.
cadia-role-fleet-gunner = Канонир флота
cadia-role-fleet-gunner-desc = Работает за консолями макропушек и лэнс-батарей.
cadia-role-fleet-gunner-brief = Выбирайте цели на голокарте, следите за боезапасом и не накрывайте своих.
cadia-role-void-tech = Техник пустотных щитов
cadia-role-void-tech-desc = Обслуживает пустотный щит и подносит снаряды.
cadia-role-void-tech-brief = Держите щит поднятым и подвозите снаряды к макропушкам.
cadia-role-inquisitor = Инквизитор
cadia-role-inquisitor-desc = Ордо Маллеус. Единственный, кто вправе отдать планету огню.
cadia-role-inquisitor-brief = Следите за порчей и помогайте обороне. Экстерминатус — последний довод: он сожжёт поле боя и закончит войну. Применяйте его, только когда Кадия потеряна.
cadia-role-silence = Сестра тишины
cadia-role-silence-desc = Неприкасаемая. Рядом с ней псайкеры обеих сторон лишаются сил.
cadia-role-silence-brief = Вы гасите варп вокруг себя, в том числе у своих псайкеров. Идите на колдунов Хаоса и держитесь подальше от союзных.
cadia-role-possessed = Одержимый
cadia-role-possessed-desc = Легионер, отдавший тело демону. Быстрый и страшный в ближнем бою.
cadia-role-possessed-brief = Врывайтесь в ряды врага. После смерти тело уйдёт в варп вместе с демоном.
cadia-role-obliterator = Облитератор
cadia-role-obliterator-desc = Живое оружие: руки стали стволами, боеприпасы создаёт варп.
cadia-role-obliterator-brief = Стреляйте в боевом режиме: патроны восполняются сами. Медленно идите вперёд и подавляйте огневые точки.
cadia-role-daemon = Демон
cadia-role-daemon-desc = Порождение варпа.
cadia-role-daemon-brief = Пока пилоны стоят, вы слабее: бьёте слабее и получаете больше урона. Ломайте пилоны и убивайте защитников.
cadia-victory-imperium = КАДИЯ СТОИТ! { $reason } Враг отброшен, Око Ужаса вновь запечатано. Император защищает!
cadia-victory-chaos = КАДИЯ ПАЛА. { $reason } Врата Ока Ужаса распахнуты. Галактика горит.

cadia-roundend-title = [color=#e8c66a][bold]Оборона Кадии[/bold][/color]
cadia-roundend-imperium = [color=#e8c66a]Победа Империума: Кадия стоит![/color]
cadia-roundend-chaos = [color=#d23b2a]Победа Хаоса: Кадия пала.[/color]
cadia-roundend-none = Война не закончилась ничем.
cadia-roundend-stats = Погибло защитников: { $imperium }, еретиков: { $chaos }. Пало пилонов: { $pylons }.
cadia-roundend-survivors = Дожили до конца: защитников { $imperium }, еретиков { $chaos }.

## Команда

cmd-cadia-desc = Управление ивентом «Оборона Кадии».
cmd-cadia-help = cadia start | status | phase next|<фаза> | wave imperium|chaos | win imperium|chaos | testmap
cmd-cadia-already = Война уже идёт.
cmd-cadia-started = Война за Кадию началась.
cmd-cadia-no-rule = Война не идёт — сначала cadia start.
cmd-cadia-status = Фаза { $phase }, осталось { $left } с. Пилоны { $alive }/{ $total }, цитадель { $citadel }%, варп { $warp }. Волны: Империум через { $imperium } с, Хаос через { $chaos } с.
cmd-cadia-testmap = Тестовое поле создано на карте { $map }.

## Звания

cadia-rank-lieutenant = Лейтенант
cadia-rank-commissar = Комиссар
cadia-rank-sergeant = Сержант
cadia-rank-trooper = Рядовой
cadia-rank-medic = Медик
cadia-rank-kasrkin = Касркин
cadia-rank-enginseer = Техножрец
cadia-rank-priest = Отец
cadia-rank-sister = Сестра
cadia-rank-psyker = Псайкер
cadia-rank-astropath = Астропат
cadia-rank-chaplain = Капеллан
cadia-rank-librarian = Эпистолярий
cadia-rank-apothecary = Апотекарий
cadia-rank-techmarine = Технодесантник
cadia-rank-um-sergeant = Сержант
cadia-rank-brother = Брат
cadia-rank-chaos-lord = Лорд
cadia-rank-dark-apostle = Тёмный апостол
cadia-rank-sorcerer = Колдун
cadia-rank-demagogue = Пророк
cadia-rank-traitor = Предатель
cadia-rank-heretek = Еретех
cadia-rank-chosen = Избранный

## Роли Империума

cadia-role-lord-castellan = Лорд-кастелян Урсаркар Крид
cadia-role-lord-castellan-desc = Верховный командующий обороной Кадии. Гений тактики, курящий сигару под огнём.
cadia-role-lord-castellan-brief = Вы командуете всей обороной. Распределите полк по пилонам и цитадели, держите связь с лейтенантами и комиссарами, решайте, где встретить штурм. Пока вы живы, Кадия не дрогнет.
cadia-role-colour-sergeant = Колор-сержант Джарран Келл
cadia-role-colour-sergeant-desc = Знаменосец и телохранитель лорда-кастеляна. Его голос перекрывает артиллерию.
cadia-role-colour-sergeant-brief = Держитесь рядом с Кридом и защищайте его ценой жизни. Знамя полка в ваших руках — пока оно поднято, гвардия держит строй.
cadia-role-lieutenant = Лейтенант
cadia-role-lieutenant-desc = Командир взвода Кадийского 8-го полка.
cadia-role-lieutenant-brief = Ведите свой взвод: получите участок обороны от лорда-кастеляна, расставьте сержантов, держите связь через вокс.
cadia-role-commissar = Комиссар
cadia-role-commissar-desc = Политический офицер Официо Префектус. Воплощение дисциплины и болт-пистолета.
cadia-role-commissar-brief = Поддерживайте боевой дух. Трусость и бегство с позиций — повод для показательной кары. Вдохновляйте личным примером: комиссар идёт в атаку первым.
cadia-role-sergeant = Сержант
cadia-role-sergeant-desc = Командир отделения из десяти кадийцев.
cadia-role-sergeant-brief = Соберите отделение, займите позицию по приказу лейтенанта и держите её. Цепной меч — для тех, кто прорвался в окоп.
cadia-role-guardsman = Гвардеец
cadia-role-guardsman-desc = Кадийский ударник. Родился с лазганом в руках, умрёт с ним же.
cadia-role-guardsman-brief = Слушайте сержанта, держите строй, стреляйте залпами. Лазган перезаряжается от батареи — не тратьте выстрелы впустую. Кадия стоит!
cadia-role-medic = Полевой медик
cadia-role-medic-desc = Медик отделения с аптечками и стимуляторами.
cadia-role-medic-brief = Вытаскивайте раненых из-под огня и ставьте их на ноги. Живой гвардеец ценнее мёртвого героя.
cadia-role-vox = Вокс-оператор
cadia-role-vox-desc = Связист с ранцевым вокс-кастером — голос командования на передовой.
cadia-role-vox-brief = Держитесь рядом с офицером, передавайте приказы и доклады. Ваш вокс-кастер слышит командный канал.
cadia-role-flamer = Огнемётчик
cadia-role-flamer-desc = Гвардеец с огнемётом. Очищает окопы и блиндажи святым пламенем.
cadia-role-flamer-brief = Встречайте прорыв в упор. Огонь не разбирает своих — следите, куда льёте прометий.
cadia-role-sniper = Снайпер
cadia-role-sniper-desc = Стрелок с длинным лазганом. Бьёт офицеров врага за километр.
cadia-role-sniper-brief = Займите высоту, выбивайте командиров, колдунов и пророков. Не давайте себя обнаружить.
cadia-role-heavy = Тяжёлое вооружение
cadia-role-heavy-desc = Расчёт ракетной установки против брони и Астартес.
cadia-role-heavy-brief = Ваша цель — космодесант Хаоса и укрепления. Не тратьте ракеты на культистов.
cadia-role-kasrkin = Касркин
cadia-role-kasrkin-desc = Элита Кадийского штурмового корпуса в панцирной броне с хеллганом.
cadia-role-kasrkin-brief = Вы — острие контратак. Выбивайте прорывы, зачищайте захваченные позиции, защищайте цитадель до последнего.
cadia-role-enginseer = Техножрец
cadia-role-enginseer-desc = Жрец Адептус Механикус. Чинит, строит и благословляет машинных духов.
cadia-role-enginseer-brief = Возводите укрепления, чините технику и пилоны. Омниссия ждёт от вас ритуалов, а гвардия — стен.
cadia-role-priest = Полковой священник
cadia-role-priest-desc = Священник Экклезиархии с дробовиком и Писанием.
cadia-role-priest-brief = Ведите гвардию молитвой и примером. Вера — лучший щит против варпа.
cadia-role-sister = Боевая сестра
cadia-role-sister-desc = Сестра Адепта Сороритас ордена Нашей Мученицы в силовой броне с болтером.
cadia-role-sister-brief = Сожгите ересь. Вы крепче гвардейца — стойте в первой линии у пилонов.
cadia-role-psyker = Санкционированный псайкер
cadia-role-psyker-desc = Связанный душой псайкер Астра Милитарум. Опасен и для врага, и для своих.
cadia-role-psyker-brief = Ваш дар держит колдунов Хаоса в узде. Комиссар рядом не случайно.
cadia-role-astropath = Астропат
cadia-role-astropath-desc = Слепой телепат, шлющий зов к флоту через варп.
cadia-role-astropath-brief = Держитесь у командования. Вы — связь Кадии с Империумом, ваша смерть оставит гвардию одну.

cadia-role-um-captain = Капитан Като Сикарий
cadia-role-um-captain-desc = Капитан 2-й роты Ультрамаринов, Мастер Дозора, Рыцарь Талассара.
cadia-role-um-captain-brief = Вторая рота высадилась на Кадию. Ведите братьев туда, где тяжелее всего, и согласуйте удар с лордом-кастеляном. Мы — Ультрамарины, и мы не знаем страха.
cadia-role-um-chaplain = Капеллан
cadia-role-um-chaplain-desc = Хранитель духа роты с крозиусом арканум.
cadia-role-um-chaplain-brief = Ведите братьев в ближний бой и читайте литании ненависти. Еретик, павший к вашим ногам, — лучшая проповедь.
cadia-role-um-librarian = Эпистолярий
cadia-role-um-librarian-desc = Библиарий Ультрамаринов с психосиловым посохом.
cadia-role-um-librarian-brief = Противостойте колдунам Чёрного легиона. Психический капюшон защищает вас — но варп у пилонов становится сильнее.
cadia-role-um-apothecary = Апотекарий
cadia-role-um-apothecary-desc = Лекарь Астартес, хранитель геносемени.
cadia-role-um-apothecary-brief = Возвращайте братьев в строй. Ни одно геносемя не должно достаться врагу.
cadia-role-um-techmarine = Технодесантник
cadia-role-um-techmarine-desc = Космодесантник, обученный на Марсе, с серворукой.
cadia-role-um-techmarine-brief = Чините, укрепляйте и бейтесь силовым топором. Машинные духи слушают вас лучше, чем техножрецов.
cadia-role-um-sergeant = Сержант-ветеран
cadia-role-um-sergeant-desc = Командир отделения Ультрамаринов в красном шлеме.
cadia-role-um-sergeant-brief = Ведите отделение по Кодексу Астартес: огонь, манёвр, удар.
cadia-role-um-tactical = Тактический десантник
cadia-role-um-tactical-desc = Ультрамарин тактического отделения с болтером.
cadia-role-um-tactical-brief = Держите линию рядом с гвардией и выбивайте Астартес Хаоса.
cadia-role-um-assault = Штурмовой десантник
cadia-role-um-assault-desc = Ультрамарин с прыжковым ранцем и цепным мечом.
cadia-role-um-assault-brief = Врывайтесь в гущу врага, рубите колдунов и пророков.
cadia-role-um-devastator = Опустошитель
cadia-role-um-devastator-desc = Ультрамарин с тяжёлым болтером.
cadia-role-um-devastator-brief = Прикрывайте подходы к цитадели шквальным огнём.

## Роли Хаоса

cadia-role-chaos-lord = Лорд Хаоса
cadia-role-chaos-lord-desc = Военачальник Чёрного легиона с демоническим клинком.
cadia-role-chaos-lord-brief = Ведите крестовый поход до прибытия Разорителя. Выберите цель — пилоны или цитадель — и бросьте на неё орду.
cadia-role-dark-apostle = Тёмный апостол
cadia-role-dark-apostle-desc = Жрец Хаоса, разжигающий фанатизм культистов.
cadia-role-dark-apostle-brief = Ведите культ проповедью и крозиусом. Ваши слова — топливо для орды.
cadia-role-sorcerer = Колдун Хаоса
cadia-role-sorcerer-desc = Псайкер Чёрного легиона, черпающий силу из варпа.
cadia-role-sorcerer-brief = С каждым павшим пилоном варп сильнее — и вы тоже. Охотьтесь на псайкеров и библиариев Империума.
cadia-role-demagogue = Пророк культа
cadia-role-demagogue-desc = Вожак культистов, бывший проповедник.
cadia-role-demagogue-brief = Собирайте культистов в отряды и ведите их волнами на окопы гвардии.
cadia-role-cultist = Культист
cadia-role-cultist-desc = Фанатик Тёмных богов с автоганом и ржавым ножом.
cadia-role-cultist-brief = Вас много, их мало. Идите волной, держитесь своего пророка, рушьте пилоны.
cadia-role-traitor = Предатель
cadia-role-traitor-desc = Бывший солдат СПО Кадии, переметнувшийся к Хаосу.
cadia-role-traitor-brief = Вы знаете, как воюет гвардия. Используйте это: обходы, засады, удары в тыл.
cadia-role-heretek = Еретех
cadia-role-heretek-desc = Отступник Механикус, служащий Тёмному механикусу.
cadia-role-heretek-brief = Стройте укрепления плацдарма, ломайте имперскую технику и пилоны.
cadia-role-black-legion = Легионер Чёрного легиона
cadia-role-black-legion-desc = Космодесантник Хаоса, ветеран Долгой войны.
cadia-role-black-legion-brief = Десять тысяч лет вы ждали этого штурма. Ломайте гвардию и рушьте пилоны.
cadia-role-abaddon = Абаддон Разоритель
cadia-role-abaddon-desc = Магистр войны Хаоса в терминаторской броне, с Когтем Гора и Драк'ньеном.
cadia-role-abaddon-brief = Тринадцатый крестовый поход — ваш. Возьмите цитадель Касра или сотрите последние пилоны. Кадия должна пасть.
cadia-role-chosen = Избранный
cadia-role-chosen-desc = Ветеран-телохранитель Абаддона.
cadia-role-chosen-brief = Следуйте за Разорителем и прикрывайте его. Его победа — ваша вечная слава.

## Панель войны

cadia-hud-phase-preparation = ПОДГОТОВКА ОБОРОНЫ
cadia-hud-phase-bombardment = ОРБИТАЛЬНЫЙ ОБСТРЕЛ
cadia-hud-phase-assault = ШТУРМ
cadia-hud-phase-finale = ПРИБЫТИЕ РАЗОРИТЕЛЯ
cadia-hud-phase-ended = ВОЙНА ОКОНЧЕНА
cadia-hud-pylons = Пилоны: { $alive }/{ $total }
cadia-hud-citadel = Цитадель Касра
cadia-hud-wave = Подкрепления через { $time }
cadia-hud-warp = Варп: { $power }
cadia-hud-wave-short = Волна: { $time }
cadia-hud-imperium-won = КАДИЯ СТОИТ!
cadia-hud-chaos-won = КАДИЯ ПАЛА
cadia-hud-ended = Раунд скоро завершится.

## Вокс

chat-radio-cadia-vox = Полковой вокс
chat-radio-cadia-command = Командный вокс
chat-radio-cadia-warband = Банда

## Полёты над Кадией

cadia-flight-map-surface = Кадия-{ $field } — поверхность
cadia-flight-map-air = Кадия-{ $field } — небо
cadia-flight-map-orbit = Кадия-{ $field } — орбита
cadia-flight-level-surface = поверхность
cadia-flight-level-air = небо
cadia-flight-level-orbit = орбиту
cadia-flight-same-level = Это тот же этаж — внутри него летите на двигателях. Чтобы сменить высоту: ПКМ по консоли шаттла → «Взлёт» или «Снижение».
cadia-flight-not-adjacent = «{ $to }» — не соседний этаж для «{ $from }». Набирайте высоту поэтапно: поверхность, небо, орбита — в пределах одного поля.
cadia-flight-space-from-orbit = В варп можно уйти только с орбиты. Сначала поднимитесь выше.
cadia-flight-arrive-orbit = Из варпа можно выйти только на орбиту Кадии.
cadia-flight-landing-blocked = Место посадки занято постройками. Выберите свободную площадку.
cadia-flight-takeoff = ВЗЛЁТ! Шаттл набирает высоту — курс на { $level }.
cadia-flight-descent = СНИЖЕНИЕ! Шаттл идёт вниз — курс на { $level }.
cadia-flight-landed = Посадка завершена. Шаттл на земле.
cadia-flight-arrived = Шаттл вышел на { $level }.
cadia-flight-falling = ШАТТЛ ТЕРЯЕТ ВЫСОТУ! Двигатели не держат — готовьтесь к удару!
cadia-flight-recovered = Тяга восстановлена — шаттл выровнялся!
cadia-flight-crashed = Шаттл рухнул на землю!
cadia-flight-beacon = Маяк: { $level }
cadia-flight-beacon-surface = поверхность Кадии
cadia-flight-beacon-air = небо Кадии
cadia-flight-beacon-orbit = орбита Кадии

## Орбитальные батареи

cadia-cruiser-name = Ударный крейсер «Честь Кадии»
cadia-battery-not-orbit = Батарея молчит: корабль не на орбите Кадии.
cadia-battery-no-surface = Под кораблём нет поверхности Кадии.
cadia-battery-no-target = Цель не выбрана. Отметьте её на голокарте.
cadia-battery-reloading = Орудия перезаряжаются: ещё { $seconds } с.
cadia-battery-no-ammo = Погреб пуст. Подайте снаряды в огневой пост.
cadia-battery-fired-macro = ЗАЛП! { $count } { $count ->
    [one] снаряд идёт
    [few] снаряда идут
   *[other] снарядов идут
} к поверхности.
cadia-battery-fired-lance = ЛЭНС-БАТАРЕЯ, ОГОНЬ! Луч идёт к поверхности.
cadia-battery-lance-no-ammo = Лэнсу снаряды не нужны — он питается от реактора.
cadia-battery-full = Погреб полон.
cadia-battery-loaded = Снаряд подан. В погребе: { $ammo }/{ $max }.
cadia-battery-examine-ammo = В погребе [color=yellow]{ $ammo }/{ $max }[/color] снарядов.
cadia-battery-examine-reload = Орудия перезаряжаются: [color=orange]{ $seconds } с[/color].
cadia-battery-examine-ready = Орудия [color=lime]готовы к залпу[/color].

cadia-battery-ui-fire = ОГОНЬ
cadia-battery-ui-hint = Кликните по голокарте, чтобы назначить цель. За несколько секунд до удара на земле загорится [color=red]красный круг[/color].
cadia-battery-ui-status-macro = Макробатарея на позиции над Кадией.
cadia-battery-ui-status-lance = Лэнс-батарея на позиции над Кадией.
cadia-battery-ui-ammo = Снарядов: { $ammo }/{ $max }
cadia-battery-ui-ready = Готовы к залпу
cadia-battery-ui-reloading = Перезарядка: { $seconds } с
cadia-battery-ui-target = Цель: { $x }, { $y }
cadia-battery-ui-no-target = Цель не выбрана
cadia-battery-ui-no-signal = НЕТ СИГНАЛА С ПОВЕРХНОСТИ
cadia-battery-ui-legend-friendly = свои войска
cadia-battery-ui-legend-pylon = пилоны
cadia-battery-ui-legend-citadel = цитадель Касра
cadia-battery-ui-legend-strike = падающие удары
cadia-battery-ui-legend-ship = наш корабль и шаттлы

## Пустотный щит

cadia-void-shield-collapse = ПУСТОТНЫЙ ЩИТ ПАЛ! Корпус открыт для огня!
cadia-void-shield-reboot = Пустотный щит снова поднят.
cadia-void-shield-examine = Щит поднят: [color=#7cc8ff]{ $percent }%[/color].
cadia-void-shield-examine-down = Щит [color=red]пал[/color], перезапуск через { $seconds } с.

## ПВО и ауспекс

cadia-aa-under-fire = ПО НАМ БЬЁТ ПВО! Манёвр уклонения!
cadia-auspex-contact = АУСПЕКС: { $count ->
    [one] вражеский борт
   *[other] вражеские борта ({ $count })
} над нами — { $bearing }!
cadia-auspex-bearing = { $dir }, { $distance } м
cadia-auspex-examine-clear = Небо чисто.
cadia-auspex-examine-contacts = Вражеских бортов на экране: [color=red]{ $count }[/color].
cadia-auspex-examine-line = • { $name } — { $bearing }, { $level }
cadia-bearing-overhead = прямо над нами
cadia-bearing-north = на севере
cadia-bearing-northeast = на северо-востоке
cadia-bearing-east = на востоке
cadia-bearing-southeast = на юго-востоке
cadia-bearing-south = на юге
cadia-bearing-southwest = на юго-западе
cadia-bearing-west = на западе
cadia-bearing-northwest = на северо-западе

## Зенитные позиции

cadia-aa-verb-enter = Спуститься в бункер
cadia-aa-entered = Вы спустились в бункер: { $name }.
cadia-aa-hatch-jammed = Люк заклинило — позиция разрушена.
cadia-aa-destroyed-inside = ПОЗИЦИЮ РАЗНЕСЛО! Вас выбросило наружу!
cadia-aa-interior-name = Бункер: { $name }
cadia-aa-full = Лента полна.
cadia-aa-loaded = Ящик снарядов заряжен. В ленте: { $ammo }/{ $max }.
cadia-aa-examine-ammo = В ленте [color=yellow]{ $ammo }/{ $max }[/color] снарядов.
cadia-aa-examine-enter = Наводчик работает из бункера — [color=lime]войдите через люк[/color] (используйте позицию).

cadia-aa-ui-level = Этаж цели:
cadia-aa-ui-level-air = Небо
cadia-aa-ui-level-orbit = Орбита
cadia-aa-ui-tag-air = небо
cadia-aa-ui-tag-orbit = орбита
cadia-aa-ui-fire = ОГОНЬ (зажать)
cadia-aa-ui-firing = ВЕДУ ОГОНЬ
cadia-aa-ui-hint = Кликните по радару — башня довернёт туда. Зажмите [color=red]ОГОНЬ[/color]: стреляет, когда башня навелась. Жёлтые кресты — промахи, красные — попадания.
cadia-aa-ui-ammo = Лента: { $ammo }/{ $max }
cadia-aa-ui-no-gun = НЕТ СВЯЗИ С ОРУДИЕМ
cadia-aa-ui-no-aim = Точка прицела не выбрана
cadia-aa-ui-on-target = Наведено
cadia-aa-ui-traversing = Башня доворачивает…
cadia-aa-ui-auspex-on = Ауспекс: данные наведения поступают
cadia-aa-ui-auspex-off = Ауспекс: нет связи
## Камера наводчика

cadia-aa-ui-contacts = Борта над позицией
cadia-aa-ui-sky-clear = Небо чисто
cadia-aa-ui-stop-view = Вернуть обзор в бункер
cadia-aa-ui-locked = цель захвачена
cadia-aa-ui-out-of-range = Цель вне дальности огня
cadia-aa-view-lost = Борт ушёл с радара — камера вернулась в бункер.
cadia-aa-hud-title = ПРИЦЕЛ: { $ship }
cadia-aa-hud-no-lock = Наведите прицел на корпус и нажмите ЛКМ
cadia-aa-hud-hint = ЛКМ по борту — захват и огонь (держать) • закрыть пост — вернуть обзор
cadia-aa-training-target = Учебная мишень

cadia-flight-verb-up = Взлёт: { $level }
cadia-flight-verb-down = Снижение: { $level }

## Гололитический стол

cadia-holo-full = Гололит переполнен — сотрите старые пометки.
cadia-holo-not-yours = Это не ваша пометка. Стереть её может автор или командир.
cadia-holo-commander-only = Очищать слой может только командир.
cadia-holo-blueprint-no-ground = Здесь нет земли — чертёж ставится только на пол.
cadia-holo-blueprint-occupied = Место занято постройкой.
cadia-blueprint-examine = Голограмма чертежа: [color=#4ad8ff]{ $name }[/color]. Нужно [color=yellow]{ $cost }[/color] листов стали — используйте их на голограмме.
cadia-blueprint-need = Нужно { $cost } листов стали.
cadia-blueprint-built = Укрепление возведено по чертежу!

cadia-holo-ui-level = Этаж:
cadia-holo-ui-layers = Слои:
cadia-holo-ui-layer-visible = Показать или скрыть слой
cadia-holo-ui-text-placeholder = Текст подписи…
cadia-holo-ui-clear = Очистить слой
cadia-holo-ui-undo = Отменить своё
cadia-holo-ui-loading = ЗАГРУЗКА ГОЛОЛИТА…
cadia-holo-level-surface = Поверхность
cadia-holo-level-air = Небо
cadia-holo-level-orbit = Орбита
cadia-holo-layer-assault = План штурма
cadia-holo-layer-defense = Оборона
cadia-holo-layer-intel = Разведданные

cadia-holo-tool-line = Линия
cadia-holo-tool-arrow = Стрелка
cadia-holo-tool-circle = Обводка
cadia-holo-tool-marker = Значок
cadia-holo-tool-text = Подпись
cadia-holo-tool-eraser = Ластик
cadia-holo-tool-blueprint = Чертёж

cadia-holo-marker-squad = Отряд
cadia-holo-marker-enemy = Враг
cadia-holo-marker-target = Цель
cadia-holo-marker-vehicle = Техника
cadia-holo-marker-landing = Высадка

cadia-blueprint-sandbags = Мешки с песком
cadia-blueprint-wall = Стена
cadia-blueprint-tanktrap = Противотанковый ёж
cadia-blueprint-razorwire = Колючая проволока
cadia-blueprint-hydra = Позиция «Гидра»
cadia-blueprint-mine = Мина
cadia-blueprint-pillbox = ДОТ
cadia-blueprint-atgun = ПТО
cadia-blueprint-bolternest = Пулемётное гнездо
cadia-blueprint-stakes = Колья
cadia-blueprint-atmine = Противотанковая мина
cadia-blueprint-chaosaltar = Кровавый алтарь
cadia-blueprint-spikebarricade = Шипастая баррикада
cadia-blueprint-warptrap = Варп-ловушка

cadia-holo-hint-line = ЛКМ и тяните — линия.
cadia-holo-hint-arrow = ЛКМ и тяните — стрелка штурма.
cadia-holo-hint-circle = ЛКМ в центре и тяните — обводка района.
cadia-holo-hint-marker = ЛКМ — поставить значок. «Цель» видят огневые посты орбитальных батарей.
cadia-holo-hint-text = Введите текст и кликните — подпись.
cadia-holo-hint-eraser = ЛКМ по пометке — стереть (свою; командир — любую).
cadia-holo-hint-blueprint = ЛКМ по клетке — чертёж: в мире появится голограмма, сапёры построят её из стали.
cadia-holo-hint-nav = Колесо — масштаб, ПКМ — двигать карту.
cadia-holo-hint-readonly = Визор показывает голокарту стороны без правки. ЛКМ или ПКМ — двигать карту, колесо — масштаб. Чертить — за гололитическим столом.

## Отряды

cadia-squad-verb = Отряды
cadia-squad-commander-only = Отрядами распоряжается только командир.
cadia-squad-max = Больше восьми отрядов не создать.
cadia-squad-default-name = Отряд { $number }
cadia-squad-joined = Вы зачислены в отряд «{ $name }».
cadia-squad-now-leader = Вы — командир отряда «{ $name }»!
cadia-squad-order-none = нет
cadia-squad-order-hold = Держать позицию
cadia-squad-order-advance = Наступать
cadia-squad-order-retreat = Отступить
cadia-squad-order-label = { $number }: { $order }
cadia-squad-order-popup-hold = «{ $name }»: ДЕРЖАТЬ ПОЗИЦИЮ!
cadia-squad-order-popup-advance = «{ $name }»: ВПЕРЁД, НАСТУПАТЬ!
cadia-squad-order-popup-retreat = «{ $name }»: ОТСТУПИТЬ!
cadia-squad-order-popup-none = «{ $name }»: приказ снят.
cadia-squad-order-sent = Приказ передан отряду «{ $name }».
cadia-squad-vox-cooldown = Вокс-команда будет готова через { $seconds } с.
cadia-squad-vox-done = Команду услышали бойцов: { $count }.
cadia-squad-vox-suppress = Огонь на подавление
cadia-squad-vox-hold = Стоять насмерть
cadia-squad-vox-charge = Вперёд!
cadia-squad-vox-suppress-desc = Бойцы отряда рядом (14 тайлов) 30 с стреляют вдвое кучнее.
cadia-squad-vox-hold-desc = Бойцы отряда рядом 30 с получают на четверть меньше урона.
cadia-squad-vox-charge-desc = Бойцы отряда рядом 30 с бегут быстрее.
cadia-squad-vox-shout-suppress = Огонь на подавление! Прижмите их к земле!
cadia-squad-vox-shout-hold = Стоять насмерть! Ни шагу назад!
cadia-squad-vox-shout-charge = Вперёд, за Императора!
cadia-squad-vox-buff-suppress = Огонь на подавление — руки твёрже, стрельба кучнее!
cadia-squad-vox-buff-hold = Стоять насмерть — боль не имеет значения!
cadia-squad-vox-buff-charge = Вперёд — ноги несут сами!

cadia-squad-ui-title = Отряды
cadia-squad-ui-squads = Отряды
cadia-squad-ui-fighters = Бойцы стороны
cadia-squad-ui-new-placeholder = Название нового отряда…
cadia-squad-ui-create = Создать отряд
cadia-squad-ui-none = Отрядов пока нет.
cadia-squad-ui-members = { $count } { $count ->
    [one] боец
    [few] бойца
   *[other] бойцов
}
cadia-squad-ui-leader = Командир: { $name }
cadia-squad-ui-no-leader = не назначен
cadia-squad-ui-order = Приказ: { $order }
cadia-squad-ui-disband = Распустить
cadia-squad-ui-vox = Вокс-команда:
cadia-squad-ui-vox-ready = Вокс-команда готова.
cadia-squad-ui-vox-cooldown = Вокс-команда: перезарядка { $seconds } с.
cadia-squad-ui-make-leader = Назначить командиром отряда
cadia-holo-tool-order = Приказ
cadia-holo-no-squads = Нет отрядов — создайте их в окне «Отряды».
cadia-holo-hint-order = Выберите отряд и приказ, затем ЛКМ по карте — точка приказа.

## Комиссар

cadia-commissar-inspire-shout = Держись, солдат! Император смотрит на тебя!
cadia-commissar-inspired = Комиссар рядом — страх отступает!
cadia-commissar-execute-shout = { $name }, ты обвиняешься в трусости. Приговор — смерть!
cadia-commissar-execute-start = Комиссар { $commissar } наставил на вас болт-пистолет!
cadia-commissar-executed = { $name } казнён за трусость!
cadia-commissar-witness = Казнь труса укрепляет вашу решимость!

chat-radio-cadia-squad = Отряд
chat-radio-cadia-warband-squad = Отряд банды
## Техника

cadia-vehicle-verb-driver = Сесть за рычаги (водитель)
cadia-vehicle-verb-gunner = Сесть к орудию (наводчик)
cadia-vehicle-verb-passenger = Сесть в десантный отсек
cadia-vehicle-full = Все места заняты.
cadia-vehicle-seat-taken = Место уже занято.
cadia-vehicle-entered-driver = Вы за рычагами «{ $vehicle }». W/S — ход, A/D — разворот.
cadia-vehicle-entered-gunner = Вы у орудия «{ $vehicle }». Мышь — наводка башни, ЛКМ — огонь.
cadia-vehicle-entered-passenger = Вы в десантном отсеке «{ $vehicle }».
cadia-vehicle-seat-driver = водитель
cadia-vehicle-seat-gunner = наводчик
cadia-vehicle-seat-passenger = десант
cadia-vehicle-weapon-selected = Выбрано: { $weapon }.
cadia-vehicle-weapon-battlecannon = Боевая пушка
cadia-vehicle-weapon-heavybolter = Тяжёлый болтер
cadia-vehicle-weapon-multilaser = Мультилазер
cadia-vehicle-weapon-earthshaker = Пушка «Сотрясатель»
cadia-vehicle-weapon-combibolter = Комби-болтер
cadia-vehicle-weapon-autocannon = Автопушка
cadia-vehicle-smoke-empty = Дымовые гранатомёты пусты или перезаряжаются.
cadia-vehicle-smoke-fired = Дымовая завеса! Осталось залпов: { $left }.
cadia-vehicle-destroyed-crew = МАШИНА ПОДБИТА! Вас выбросило из горящего корпуса!
cadia-vehicle-ritual-unneeded = Корпус цел — машинный дух спокоен.
cadia-vehicle-ritual-start = { $name } заводит литанию починки, умащая броню священными маслами…
cadia-vehicle-ritual-done = Дух машины умиротворён — броня стянута.
cadia-vehicle-examine-crew = Экипаж: водитель { $driver }/1, наводчик { $gunner }/1, десант { $passengers }/{ $max }.
cadia-vehicle-examine-repair = Чинится сваркой; техножрец ускоряет ремонт ритуалом с топором Омниссии.
cadia-vehicle-hud-hull = Корпус { $percent }%
cadia-vehicle-hud-crew = Водитель { $driver }  Наводчик { $gunner }  Десант { $passengers }/{ $max }
cadia-vehicle-hud-smoke = Дымовые гранатомёты: { $charges }
cadia-vehicle-hud-ready = готово
cadia-vehicle-hud-reload = перезарядка { $seconds } с
cadia-vehicle-hud-empty = ПУСТО
cadia-vehicle-hud-unloaded = НЕ ЗАРЯЖЕНО
cadia-vehicle-verb-hatch = Забраться через люк
cadia-vehicle-locked = Люк заперт. Нужен ключ.
cadia-vehicle-in-cabin = Вы в кабине «{ $vehicle }». Кресла водителя и наводчика — у носа, ключ на полу у люка.
cadia-vehicle-cabin-name = Кабина: { $vehicle }
cadia-vehicle-key-wrong = Этот ключ от другой машины.
cadia-vehicle-key-locked = «{ $vehicle }»: люк заперт.
cadia-vehicle-key-unlocked = «{ $vehicle }»: люк отперт.
cadia-vehicle-rack-empty = Боеукладка пуста.
cadia-vehicle-rack-full = В боеукладке нет места.
cadia-vehicle-hands-full = Руки заняты: снаряд не взять.
cadia-vehicle-belt-loaded = Боекомплект автоматического оружия пополнен.
cadia-vehicle-breech-full = В казённике уже есть снаряд.
cadia-vehicle-breech-loaded = Снаряд дослан, затвор закрыт.
cadia-vehicle-breech-loaded-gunner = Орудие заряжено!
cadia-vehicle-breech-empty = Казённик пуст: нужен снаряд из боеукладки.
cadia-vehicle-examine-locked = Люк заперт.
cadia-vehicle-examine-unlocked = Люк открыт.

## Кампания: памятка
cadia-guide-title = Памятка бойца
cadia-guide-task = Ваша задача
cadia-guide-goal = Цель стороны
cadia-guide-close = Понятно
cadia-goal-imperium = Удержите пилоны и цитадель Касра до конца развязки. Если всё же падут пилоны или цитадель, начнётся эвакуация: доберитесь до маяка, раненых грузят первыми. Изгнание принца демонов тоже приносит победу.
cadia-goal-chaos = Снесите все пилоны или удержите цитадель Касра без защитников рядом. Затем не дайте врагу уйти: уничтожьте маяк эвакуации и держите площадку под огнём.
cadia-tip-common-title = Что нужно знать всем
cadia-tip-common = Обоймы и снаряды берите в ящиках снабжения, лазган заряжается на зарядной станции. В окопе и за укрытием в вас труднее попасть издали. Мораль падает от потерь и страха: держитесь своих и офицеров. Знамёна секторов дают снабжение и новые точки высадки, стойте рядом без врагов, чтобы взять сектор. Боевой клич есть на панели эмоций. Погибшие возвращаются с волнами подкреплений через меню ролей. Эта памятка открывается снова кнопкой на панели действий.
cadia-tip-command-title = Командир
cadia-tip-command = Голокарта на столе и в визоре: приказы, метки и чертежи обороны. Окно отрядов: соберите бойцов, отдавайте приказы и используйте вокс-команды. Медалью на панели действий отмечайте отличившихся: имя прозвучит для всей стороны и попадёт в итоги войны.
cadia-tip-commissar-title = Комиссар
cadia-tip-commissar = Ваша аура держит мораль рядом с вами. Фуражка даёт воодушевление и казнь дрогнувшего. Медалей у вас больше, чем у других офицеров, используйте их.
cadia-tip-medic-title = Полевая медицина
cadia-tip-medic = Красные кресты на экране отмечают тяжелораненых вашей стороны, а сообщение подскажет направление и расстояние до дальних. Разложите носилки, пристегните раненого и тяните к полевому госпиталю: лёжа в палатке, он лечится сам. При эвакуации раненые грузятся первыми.
cadia-tip-engineer-title = Инженер
cadia-tip-engineer = Командир ставит чертежи на голокарте, вы строите их на месте листами стали. Сталь заказывается в депо. Сварка чинит технику.
cadia-tip-trench-title = Окопы
cadia-tip-trench = Сапёрной лопаткой копайте окоп кликом по свободной земле. Соседние участки соединяются в траншею. В окопе вы медленнее, но больше половины пуль издали проходит над головой.
cadia-tip-psyker-title = Псайкер
cadia-tip-psyker = Силы тратят варп-заряд и копят напряжение: шкалы слева внизу. Перегрев вызывает Опасности варпа, которые бьют и по вам, и по соседям. Сёстры тишины глушат силы рядом с собой.
cadia-tip-fleet-title = Флот
cadia-tip-fleet = Вы — пост флота в бункере под цитаделью Касра: отсюда наводится крейсер, что держится над полем. Посты батарей открывают голокарту: выбирайте цели и не накрывайте своих. Макроснаряды подносите к батарее из погреба. Пустотный щит цитадели во дворе наверху держит купол над сердцем Касра — не давайте ему рухнуть.
cadia-tip-astartes-title = Астартес
cadia-tip-astartes = Вы не ведаете страха, а силовой доспех заменяет обычную броню. Боевой клич и зов апотекария есть на панели эмоций. Геносемя павшего брата возвращает его место в строю.
cadia-tip-summon-title = Призыв демонов
cadia-tip-summon = Нажмите правой кнопкой по разлому, чтобы призвать демона за силу варпа. Пока стоят пилоны, призыв вдвое дороже, а демоны слабее. Принц демонов открывается после падения половины пилонов или в развязке.
cadia-tip-daemon-title = Демон
cadia-tip-daemon = Вы сильнее, когда пилоны пали. Рядом с вами враги теряют мораль, а смерть лишь возвращает вас в варп.

## Кампания: вступление
cadia-intro-1 = Кадия. Врата Ока Ужаса.
    Десять тысяч лет древние пилоны сдерживают варп, и десять тысяч лет эта планета не сдаётся.
cadia-intro-2 = Абаддон Разоритель ведёт Тринадцатый Чёрный крестовый поход.
    Небо горит, флоты Хаоса заслоняют звёзды, и каждый пилон станет полем битвы.
cadia-intro-3 = Гвардеец, ты родился с винтовкой в руках и знаешь свой долг.
    Держи рубеж. Защищай пилоны. Кадия стоит!
cadia-intro-skip = Пропустить

## Кампания: ожидание
cadia-wait-title = Вы выбыли из боя
cadia-wait-imperium = Империум
cadia-wait-chaos = Хаос
cadia-wait-seats = { $side }: свободных мест { $seats }, новая волна через { $time }
cadia-wait-closed = { $side }: набор закрыт, на этой стороне слишком много бойцов
cadia-wait-evac = { $side }: подкреплений больше не будет, идёт эвакуация
cadia-wait-hint = Вернуться в бой: меню призрачных ролей.

## Кампания: секторы
cadia-sector-examine = Сектор { $letter }. { $owner }
cadia-sector-owner-imperium = Удерживает Империум.
cadia-sector-owner-chaos = Удерживает Хаос.
cadia-sector-owner-none = Ничей.
cadia-sector-taken-ours = Сектор { $letter } взят! Он даёт снабжение и новую точку высадки.
cadia-sector-taken-theirs = Сектор { $letter } потерян!

## Кампания: падение Кадии
cadia-hud-phase-evacuation = ПАДЕНИЕ КАДИИ
cadia-hud-evac = Спасено: { $saved }, транспорт через { $time }
cadia-phase-evacuation-imperium = Кадия пала. Всем выжившим: к маяку эвакуации! Транспорты берут тех, кто рядом, раненых первыми. Выйдите из техники, подкреплений больше не будет.
cadia-phase-evacuation-chaos = Кадия пала! Никто не должен уйти. Уничтожьте маяк эвакуации и залейте площадку огнём.
cadia-evac-ship-coming = Транспорт садится у маяка через { $seconds } с! Все к площадке!
cadia-evac-direction = Маяк эвакуации { $dir }, { $meters } м
cadia-evac-empty = Транспорт ушёл пустым: на площадке никого не было.
cadia-evac-loaded = Транспорт забрал бойцов: { $count }. Всего спасено: { $total }.
cadia-evac-loaded-chaos = Транспорт ушёл, забрав беглецов: { $count }. Не дайте уйти следующему!
cadia-fall-good = Спасено защитников: { $saved }. Организованное отступление: полки Кадии продолжат войну.
cadia-fall-poor = Спасено защитников: { $saved }. Горькое бегство, но память о Кадии жива.
cadia-fall-none = Спасено защитников: { $saved }. От Кадийских полков почти ничего не осталось.

## Кампания: штормы
cadia-storm-bloodrain-imperium = Кровавый дождь! Варп сочится с неба, и бойцы теряют присутствие духа. Держитесь офицеров или уходите под броню.
cadia-storm-bloodrain-chaos = С неба льётся кровь: боги благоволят вам. Наступайте!
cadia-storm-whispers-imperium = В голосах вокса слышен шёпот. Не слушайте его и держите строй!
cadia-storm-whispers-chaos = Шёпот варпа сводит врага с ума. Пользуйтесь этим!
cadia-storm-daemonsurge-imperium = Варп прорывается у пилонов и секторов! Демоны идут прямо на позиции!
cadia-storm-daemonsurge-chaos = Завеса рвётся! Малые разломы открыты, сила варпа растёт.
cadia-whisper-1 = Тебя бросили здесь умирать...
cadia-whisper-2 = Твой Император не слышит...
cadia-whisper-3 = Брось оружие, и боль закончится...
cadia-whisper-4 = Они знают твоё имя...
cadia-whisper-5 = Обернись...
cadia-whisper-6 = Кадия уже пала...

## Кампания: итоги и медали
cadia-stats-title = [color=#e8c66a][bold]Итоги войны[/bold][/color]
cadia-stats-kills = Лучший стрелок: { $name }, врагов уничтожено: { $value }
cadia-stats-pylons = Разрушитель пилонов: { $name }, пилонов: { $value }
cadia-stats-healed = Лучший медик: { $name }, вылечено урона: { $value }
cadia-stats-life = Самый живучий: { $name }, минут в одной жизни: { $value }
cadia-stats-deaths = Чаще всех возвращался в строй: { $name }, смертей: { $value }
cadia-stats-crew = Лучший экипаж: { $vehicle }, уничтожено: { $value } ({ $crew })
cadia-stats-medals = [color=#e8c66a]Награждённые:[/color]
cadia-medal-award = { $officer } награждает медалью бойца { $soldier }!
cadia-medal-wrong-target = Наградить можно только живого бойца своей стороны.

## Кампания: панель ведущего
cadia-panel-title = Панель ведущего: Оборона Кадии
cadia-panel-no-war = Война не запущена.
cadia-panel-status = { $phase }, осталось { $time }. Завеса { $veil }. Пилоны { $alive }/{ $total }. Варп { $warp }. Горожане в убежище { $refugees }/{ $refugeesTotal }. Спасено { $saved }.
    Империум: живых игроков { $imperium }, мест { $imperiumSeats }. Хаос: живых игроков { $chaos }, мест { $chaosSeats }.
cadia-panel-war = Война
cadia-panel-start = Запустить
cadia-panel-next-phase = Следующая фаза
cadia-panel-intro = Показать вступление
cadia-panel-fall = Падение Кадии
cadia-panel-waves = Набор
cadia-panel-wave-imperium = Волна Империума
cadia-panel-wave-chaos = Волна Хаоса
cadia-panel-close-imperium = Закрыть набор Империума
cadia-panel-open-imperium = Открыть набор Империума
cadia-panel-close-chaos = Закрыть набор Хаоса
cadia-panel-open-chaos = Открыть набор Хаоса
cadia-panel-storms = Варп
cadia-panel-storm-rain = Кровавый дождь
cadia-panel-storm-whispers = Шёпот
cadia-panel-storm-surge = Прорыв демонов
cadia-panel-warp = +10 варпа
cadia-panel-win = Победа
cadia-panel-win-imperium = Империуму
cadia-panel-win-chaos = Хаосу
cadia-panel-announce-placeholder = Текст объявления
cadia-panel-announce-all = Всем
cadia-panel-announce-imperium = Империуму
cadia-panel-announce-chaos = Хаосу
cadia-panel-filter = Поиск игрока по имени
cadia-panel-remove = Убрать
cadia-panel-ban = Убрать и запретить

## Порядок на ивенте
cadia-rules-whitelist = Командная роль только для игроков из вайтлиста. Без допуска вас сразу вернёт обратно. Обратитесь к ведущему.
cadia-whitelist-denied = Эта роль только по вайтлисту. Вы возвращены в меню ролей, а место снова свободно. Обратитесь к ведущему.
cadia-afk-warning = Вы бездействуете. Через две минуты место передадут другому игроку.
cadia-afk-removed = Вы сняты с поля за бездействие. Вернуться можно через меню ролей.
cadia-ff-warning = Вы стреляете по своим! Ведущий это видит.
cadia-ff-kill = Вы убили своего! Ведущий это видит.
cadia-ff-admin = Огонь по своим: { $name } убил { $victim }. Всего убитых своих: { $kills }.
cadia-panel-friendly-fire = [по своим: убито { $kills }, урон { $damage }]

## Отряды
cadia-squad-auto-astartes = { $number }-е отделение Астартес
cadia-squad-auto-joined-leader = Вы в отряде «{ $name }». Командир: { $leader }.

## Цель на экране
cadia-objective-line = Цель: { $text }
cadia-objective-line-distance = Цель: { $text }, { $meters } м
cadia-objective-order-hold = держать позицию по приказу
cadia-objective-order-advance = наступать по приказу
cadia-objective-order-retreat = отступать по приказу
cadia-objective-evac-imperium = к маяку эвакуации
cadia-objective-evac-chaos = уничтожить маяк эвакуации
cadia-objective-orders = выполнять приказы командования
cadia-objective-prepare = готовить оборону у пилона { $number }
cadia-objective-staging = готовиться к штурму на плацдарме
cadia-objective-hold-sector = удержать сектор { $letter }
cadia-objective-take-sector = взять сектор { $letter }
cadia-objective-defend-pylon = защищать пилон { $number }
cadia-objective-attack-pylon = снести пилон { $number }

## Лента событий
cadia-feed-pylon = Пал пилон { $number }, осталось { $left }
cadia-feed-sector-imperium = Империум взял сектор { $letter }
cadia-feed-sector-chaos = Хаос взял сектор { $letter }
cadia-feed-commander-fell = Пал командир: { $name }
cadia-feed-arrived = На поле: { $name }
cadia-feed-prince = Принц демонов вышел из варпа
cadia-feed-evac = Транспорт ушёл с бойцами: { $count }, всего спасено { $total }
cadia-feed-storm-bloodrain = Варп-шторм: кровавый дождь
cadia-feed-storm-whispers = Варп-шторм: шёпот
cadia-feed-storm-daemonsurge = Варп-шторм: прорыв демонов

## Команды ведущего
cmd-cadia-whitelist-hint = ckey игрока
cmd-cadia-whitelist-list = Вайтлист, игроков { $count }: { $names }
cmd-cadia-whitelist-added = { $name } добавлен в вайтлист.
cmd-cadia-whitelist-already = { $name } уже в вайтлисте.
cmd-cadia-whitelist-removed = { $name } убран из вайтлиста.
cmd-cadia-whitelist-missing = { $name } нет в вайтлисте.
cmd-cadia-lobby-on = Оформление лобби Кадии включено: новые фон и музыка видны сразу.
cmd-cadia-lobby-off = Обычное лобби возвращено.

## Концовки
cadia-ending-held-title = КАДИЯ СТОИТ
cadia-ending-held-1 = Чёрный крестовый поход разбился о рубежи Кадии. Пилоны стоят, и Око Ужаса снова заперто.
cadia-ending-held-2 = Павшие лежат в окопах, но гвардия держит строй. Кадия стоит, и пока она стоит, стоит Империум.
cadia-ending-banished-title = ДЕМОН ИЗГНАН
cadia-ending-banished-1 = Принц демонов вырван из реальности и низвергнут обратно в варп. Его вой слышали на всём фронте.
cadia-ending-banished-2 = Без своего чудовища орды рассыпались. Разломы гаснут, и над Кадией впервые за много дней видно небо.
cadia-ending-exterminatus-title = ЭКСТЕРМИНАТУС
cadia-ending-exterminatus-1 = Инквизиция вынесла приговор. Циклонные торпеды сожгли поле боя вместе со всеми, кто на нём остался.
cadia-ending-exterminatus-2 = Хаос не получит Кадию. Никто её больше не получит. Император простит, потому что выбора не было.
cadia-ending-retreat-title = ОРГАНИЗОВАННОЕ ОТСТУПЛЕНИЕ
cadia-ending-retreat-1 = Кадия пала, но Кадийские полки живы. Транспорты вывезли защитников: { $saved }.
cadia-ending-retreat-2 = Они помнят свой дом и вернутся за него. Пока жив хотя бы один кадиец, война не окончена.
cadia-ending-bitter-title = ГОРЬКОЕ БЕГСТВО
cadia-ending-bitter-1 = Последняя «Валькирия» ушла под огнём. Удалось вывезти лишь защитников: { $saved }.
cadia-ending-bitter-2 = Те, кто остался на площадке, прикрывали взлёт до конца. Их имён никто не внесёт в списки.
cadia-ending-annihilation-title = НИКТО НЕ ВЕРНЁТСЯ
cadia-ending-annihilation-1 = Площадка пуста. Спасено защитников: { $saved }. От Кадийских полков остались только шлемы в грязи.
cadia-ending-annihilation-2 = Знамя Империума втоптано в землю. Врата Ока Ужаса открыты, и за ними больше некому стоять.
cadia-ending-triumph-title = КАДИЯ ПАЛА
cadia-ending-triumph-1 = Абаддон Разоритель стоит на руинах Касра. Десять тысяч лет ожидания окончены.
cadia-ending-triumph-2 = Пилоны лежат в пыли, и Око Ужаса заливает небо. Галактика расколота, и Тёмные боги торжествуют.

## Погода и дым
cadia-feed-weather-ash = Погода: пепел, обзор хуже
cadia-feed-weather-fog = Погода: густой туман, обзор сильно хуже
cadia-feed-weather-smog = Погода: гарь над полем, обзор хуже
cadia-depot-smoke = Дымовая граната
cadia-panel-weather = Сменить погоду

cmd-cadia-map = Боевая карта Кадии построена, поверхность на карте { $map }.
cadia-chaos-dropship-name = десантный корабль Хаоса { $number }
cadia-floor-upper = Каср, стены и крыши ({ $field })
cadia-floor-lower = Каср, катакомбы ({ $field })
cmd-cadia-map-built = Поле { $width }×{ $height }: пилонов { $pylons }, объектов { $objects }, декалей { $decals }.

## Флот Хаоса
cadia-warp-gate-verb = Шагнуть: { $target }
cadia-warp-gate-denied = Руны жгут кожу. Врата не пропускают чужих.
cadia-chaos-flagship-name = флагман Хаоса «Терзатель»
cadia-chaos-bomber-name = бомбардировщик Хаоса { $number }

## Завеса пилонов: подготовка обороны
cadia-veil-weapons = Завеса пилонов глушит оружие. Бой начнётся с обстрелом.
cadia-veil-warp = Завеса пилонов гасит варп: сила не отзывается.
cadia-veil-orbit = Завеса пилонов закрывает небо над Касром: батарея не пробьёт её до начала обстрела.
cadia-veil-sky = Завеса пилонов закрывает небо над Кадией: сквозь неё не пройти до начала обстрела.
cadia-flight-armed-bombardment = Боевым кораблям дозволено спускаться с орбиты только во время обстрела.

## Когтистые капсулы
cadia-drop-verb = Высадка: { $zone }
cadia-drop-denied = Капсула откликается только на волю слуг Тёмных богов.
cadia-drop-returning = Капсула ещё возвращается на флагман: { $seconds } с.
cadia-drop-launch = Капсула отстыковывается! Удар через пару мгновений — { $zone }.
cadia-drop-zone-north = северная пустошь
cadia-drop-zone-gallan = павший бастион Галлан
cadia-drop-zone-south = южная пустошь
cadia-drop-zone-ridge = восточный гребень

cadia-valkyrie-name = «Валькирия»

## Падение завесы
cadia-veil-siren-imperium = Воздушная тревога! Флот Абаддона перегружает пилоны — завеса падает через тридцать секунд. Всем в укрытия, расчётам ПВО к орудиям!
cadia-veil-siren-chaos = Пилоны стонут под натиском флота. Через тридцать секунд завеса падёт — к пусковым шахтам, слуги Тёмных богов!
cadia-veil-fallen-feed = Завеса пилонов пала: флот открыл огонь

## Ритуал над завесой
cadia-ritual-verb-chaos = Ритуал прорыва завесы
cadia-ritual-verb-imperium = Литания пилона
cadia-ritual-not-now = Завеса уже не держится — ритуал бесполезен.
cadia-ritual-cooldown = Варп ещё не успокоился после прошлого ритуала: { $seconds } с.
cadia-ritual-begin-chaos = { CAPITALIZE($user) } начинает ритуал прорыва. Кровь на кругу закипает.
cadia-ritual-begin-imperium = { CAPITALIZE($user) } возносит литанию у пилона. Руны на обелиске разгораются.
cadia-ritual-done-chaos-imperium = Пилоны стонут: колдуны Хаоса раскачивают завесу. Обстрел ближе на { $seconds } с.
cadia-ritual-done-chaos-chaos = Ритуал свершён: завеса дрогнула. Обстрел ближе на { $seconds } с.
cadia-ritual-done-imperium-imperium = Литания услышана: пилоны поют громче. Завеса продержится ещё { $seconds } с.
cadia-ritual-done-imperium-chaos = Жрецы Трупа-Императора укрепили пилоны. Завеса продержится ещё { $seconds } с.

## Беженцы Касра
cadia-refugee-saved = { CAPITALIZE($name) } укрывается в катакомбах.
cadia-refugee-feed = Горожане в убежище: { $saved } из { $total }
cadia-refugee-report = Завеса пала. В катакомбах укрыто { $saved } горожан из { $total }. Остальные — на улицах под огнём.
cadia-refugee-report-chaos = На улицах Касра осталось { $left } беззащитных горожан. Их страх — пища для варпа.

## Капсулы под огнём ПВО
cadia-drop-hit = Капсулу подбило ПВО! Жёсткая посадка — вы ранены.
cadia-drop-hit-feed = ПВО подбила когтистую капсулу

## Панель ведущего: завеса и капсулы
cadia-panel-veil = Завеса
cadia-panel-veil-drop = Снять завесу
cadia-panel-veil-raise = Поднять завесу
cadia-panel-veil-add = +1 мин подготовки
cadia-panel-veil-cut = −1 мин подготовки
cadia-panel-drop-recharge = Перезарядить шахты
cadia-panel-drop-pod = Пустая капсула
cadia-panel-veil-up = держится
cadia-panel-veil-down = пала

## Голоса войны
cadia-voice-abaddon-sender = Абаддон Разоритель
cadia-voice-apostle-sender = Тёмный апостол
cadia-voice-creed-sender = Лорд-кастелян Крид
cadia-voice-taunt-sender = Перехват вокса

cadia-voice-abaddon-preparation-1 = Десять тысяч лет эта скала стоит на пути. Сегодня она треснет. Ждите знака.
cadia-voice-abaddon-preparation-2 = Пусть кадийцы роют свои окопы. Я хочу, чтобы они умирали, зная, что копали себе могилы.
cadia-voice-abaddon-bombardment-1 = Завеса пала. Теперь Кадия услышит наш голос — в грохоте каждого залпа.
cadia-voice-abaddon-bombardment-2 = К шахтам, мои воины. Падайте на них, как падает небо.
cadia-voice-abaddon-bombardment-3 = Каждый пилон, что рухнет, — это дверь. Открывайте их одну за другой.
cadia-voice-abaddon-assault-1 = Они называют себя несокрушимыми. Покажите им, что значит это слово на самом деле.
cadia-voice-abaddon-assault-2 = Цитадель Касра — сердце их гордыни. Вырвите его.
cadia-voice-abaddon-assault-3 = Я ждал этого дня десять тысячелетий. Не заставляйте меня ждать ещё час.
cadia-voice-abaddon-finale-1 = Последние пилоны гаснут. Око Ужаса смотрит на вас, и оно довольно.
cadia-voice-abaddon-finale-2 = Кадия пала в тот миг, когда я решил её взять. Остальное — лишь работа клинка.
cadia-voice-abaddon-evacuation-1 = Они бегут. Пусть ни один корабль не уйдёт с этой планеты.

cadia-voice-apostle-preparation-1 = Братья, молитесь. Боги слышат, как трещит завеса, — помогите ей треснуть.
cadia-voice-apostle-preparation-2 = Кровь на кругу ещё тёплая. Колдуны, к ритуалу — каждая минута их подготовки стоит нам сотни жизней.
cadia-voice-apostle-bombardment-1 = Огонь с небес — это молитва. Произносите её громче.
cadia-voice-apostle-bombardment-2 = Не бойтесь их зениток. Тот, кто падёт в капсуле, падёт в объятия богов.
cadia-voice-apostle-assault-1 = Их жрецы поют литании. Заставьте их замолчать — и пилоны замолчат вместе с ними.
cadia-voice-apostle-assault-2 = Каждая смерть здесь — подношение. Не жалейте ни врагов, ни себя.
cadia-voice-apostle-finale-1 = Варп прорывается. Чувствуете? Это дыхание богов на вашей коже.

cadia-voice-creed-preparation-1 = Кадийцы, это ваш час. Окопы глубже, мешки выше. Завеса держится — но не вечно.
cadia-voice-creed-preparation-2 = Уводите горожан в катакомбы. Каждый спасённый — ещё одна причина держать эту стену.
cadia-voice-creed-bombardment-1 = Завеса пала. Расчёты ПВО — бить по капсулам на спуске. Остальным — в укрытия и ждать.
cadia-voice-creed-bombardment-2 = Они падают с неба прямо нам в руки. Встречайте их штыками.
cadia-voice-creed-assault-1 = Пилоны — наша жизнь. Пока стоит хоть один, Око не пройдёт. Держать их любой ценой.
cadia-voice-creed-assault-2 = Кадия стоит! Повторяйте это, пока враг не поверит.
cadia-voice-creed-assault-3 = Ультрамарины на подходе. Продержитесь ещё немного, гвардейцы.
cadia-voice-creed-finale-1 = Это последний рубеж. За нами — только Император.
cadia-voice-creed-finale-2 = Если Кадии суждено пасть, пусть враг заплатит за каждый её камень.
cadia-voice-creed-evacuation-1 = К транспортам! Раненых — первыми. Кадия жива, пока живы кадийцы.

cadia-voice-taunt-bombardment-1 = …ваш Крид лжёт вам, гвардейцы. Пилоны уже мертвы. Сложите оружие — и умрёте быстро…
cadia-voice-taunt-assault-1 = …Император не слышит вас. Его слышу я. И я говорю: Кадия падёт…
cadia-voice-taunt-assault-2 = …сколько ещё ваших братьев вы оставите в этой грязи? Спросите себя, за что…
cadia-voice-taunt-finale-1 = …посмотрите на небо, кадийцы. Оно уже наше…

## Таймер завесы на экране
cadia-veil-hud-timer = Завеса пилонов: { $time }
cadia-veil-hud-held = Завеса пилонов держится

## Абордаж флагмана
cadia-boarding-verb = Абордаж: { $deck }
cadia-boarding-cooldown = Заряды для пробоя ещё ставят: { $seconds } с.
cadia-boarding-too-far = Флагман слишком далеко. Подведите «Валькирию» к «Терзателю» на орбите.
cadia-boarding-alarm = Абордаж! Имперцы прорезали корпус — { $deck }, их { $count }. Всем к пробоине!
cadia-boarding-feed = Абордажная группа на флагмане: { $deck }
cadia-boarding-return-verb = Отход на «Валькирию»
cadia-boarding-no-ship = «Валькирии» рядом нет — отходить некуда.
cadia-boarding-deck-engine = машинное отделение
cadia-boarding-deck-guns = орудийная палуба
cadia-boarding-deck-drop = десантная палуба

## Пусковые шахты под ударом
cadia-berth-broken = Шахта разбита — капсула не выйдет, пока её не починят.
cadia-berth-broken-chaos = Пусковая шахта разбита! Еретеков — на десантную палубу.
cadia-berth-broken-feed = Пусковая шахта флагмана разбита
cadia-berth-repair-verb = Починить шахту
cadia-berth-repair-begin = { CAPITALIZE($user) } латает пусковую шахту.
cadia-berth-repaired = Шахта снова готова принять капсулу.

## Беженцы
cadia-refugee-died-feed = Погиб горожанин Касра

## Истребители флота
cadia-fighter-name = «Тандерболт» { $number }

## Итоги: ход войны
cadia-stats-war-podslanded = Когтистых капсул высажено: { $value }
cadia-stats-war-podsshotdown = Капсул подбито ПВО: { $value }
cadia-stats-war-refugeessaved = Горожан укрыто в катакомбах: { $value }
cadia-stats-war-refugeesdied = Горожан погибло: { $value }
cadia-stats-war-ritualschaos = Ритуалов прорыва завесы: { $value }
cadia-stats-war-ritualsimperium = Литаний у пилонов: { $value }
cadia-stats-war-boardings = Абордажей флагмана: { $value }
cadia-stats-war-berthswrecked = Пусковых шахт разбито: { $value }

## Реакции командиров на события
cadia-react-pylon-imperium-1 = Пилон пал. Держать оставшиеся — каждый, кто может стрелять, к обелискам!
cadia-react-pylon-imperium-2 = Мы потеряли пилон. Око смотрит пристальнее — значит, стоим твёрже.
cadia-react-pylon-chaos-1 = Ещё один пилон умолк. Слышите, как поёт варп? Дальше!
cadia-react-pylon-chaos-2 = Их обелиск рухнул. Кадия слепнет — бейте, пока она не прозрела.
cadia-react-sector-imperium-imperium-1 = Сектор наш! Закрепиться и подвезти боеприпасы.
cadia-react-sector-imperium-imperium-2 = Отличная работа, гвардейцы. Этот клочок земли снова кадийский.
cadia-react-sector-imperium-chaos-1 = Вы отдали им сектор. Вернуть его — или не возвращайтесь сами.
cadia-react-sector-chaos-imperium-1 = Сектор потерян. Перегруппироваться и отбить — враг не должен там закрепиться.
cadia-react-sector-chaos-chaos-1 = Сектор пал к нашим ногам. Знамёна — в грязь, наши — на древко.
cadia-react-sector-chaos-chaos-2 = Ещё одна земля для богов. Не останавливайтесь.
cadia-react-commander-imperium-imperium-1 = Лорд-кастелян пал… Кадия стоит! Офицеры — принять командование, держать линию!
cadia-react-commander-imperium-chaos-1 = Их кастелян мёртв. Голова отрублена — теперь тело само упадёт.
cadia-react-commander-chaos-imperium-1 = Командир еретиков повержен! Они дрогнули — вперёд, пока не опомнились!
cadia-react-commander-chaos-chaos-1 = Наш лорд пал. Пусть его кровь станет ступенью для того, кто займёт его место.
cadia-react-assault-imperium-1 = Ультрамарины на поле! Сыны Жиллимана с нами — Кадия стоит!
cadia-react-assault-imperium-2 = Адептус Астартес высадились. Покажем ангелам Императора, как держит строй гвардия.
cadia-react-assault-chaos-1 = Сыны Жиллимана пришли умирать за чужую скалу. Окажите им эту честь.
cadia-react-assault-chaos-2 = Ультрамарины. Я помню их отцов. Их кровь горчит так же.

## Граница поля
cadia-bounds-push = Буран Кадийских врат слепит и сбивает с пути — дальше не пройти. Вы возвращаетесь назад.

cadia-role-chaos-bomber-pilot = Пилот «Адского когтя»
cadia-role-chaos-bomber-pilot-desc = Предатель из флота Кадии за штурвалом бомбардировщика Хаоса.
cadia-role-chaos-bomber-pilot-brief = Дождитесь обстрела, спуститесь к полю и сбросьте бомбы на укрепления. Не подставляйтесь «Тандерболтам» и зениткам.
cadia-rank-chaos-bomber-pilot = Пилот
cadia-tip-bomber-title = Бомбардировщик
cadia-tip-bomber = Вы появляетесь в кабине «Адского когтя». Корабль ведут с консоли шаттла, пушки и бомбовые люки — с поста стрелка. Бомбы загружают в люки руками. Боевым кораблям дозволено спускаться с орбиты только во время обстрела. Шахты когтистых капсул в коридоре корабля высаживают отделение на поле, варп-врата ведут на флагман.
