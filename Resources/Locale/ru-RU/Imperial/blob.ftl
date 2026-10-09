## Роль и цели

roles-antag-blob-name = Блоб
roles-antag-blob-objective = Достигните критической массы: 400 тайлов блоба на станции.
objective-issuer-blob = Сознание Блоба
objective-condition-blob-infect-title = Захватить станцию
objective-condition-blob-infect-description = Заразите минимум { $count } станционных тайлов биомассой Блоба.
objective-condition-blob-takeover-title = Достигните критической массы!
objective-condition-blob-takeover-description = Разрастись до { $count } тайлов блоба на станции.

blob-briefing = Вы — Блоб. Разрастайтесь от ядра, копите ресурсы, стройте узлы, фабрики и ресурсные блобы и достигните критической массы.
blob-role-greeting-carrier = Вы — носитель Блоба. Используйте способность «Выпустить», чтобы разорваться и разместить ядро. Лучше делать это вдали от людей: против вас будет весь экипаж!
blob-round-end-agent-name = блоб
blob-round-end-victory = Блоб поглотил станцию. Критическая масса: { $count } тайлов.
blob-round-end-overmind-won = [color=#4aa34a]{ $name }[/color] поглотил станцию неудержимой волной!
blob-round-end-overmind-lost = [color=#4aa34a]{ $name }[/color] захватил { $count } тайлов на пике своего роста.

## Оверманд

blob-overmind-name = Сознание Блоба ({ $number })
blob-overmind-login = [color=#4aa34a][bold]Вы — сознание Блоба![/bold][/color]
blob-overmind-autoplace = [color=#d94b4b]Ядро будет размещено автоматически через { $time }.[/color]
blob-overmind-manual-later = [color=#d94b4b]Позже вы сможете разместить ядро вручную кнопкой «К ядру».[/color]
blob-overmind-manual-now = [color=#d94b4b]Вы можете разместить ядро вручную кнопкой «К ядру».[/color]
blob-overmind-can-place = [bold]Теперь вы можете разместить ядро блоба.[/bold]
blob-overmind-free-reroll = [bold]Вы получили ещё одну бесплатную смену штамма.[/bold]
blob-overmind-critical-no-end = Вы достигли критической массы, но что-то пошло ужасно не так и не даёт расти дальше. Остаётся лишь сражаться, пока можете...
blob-overmind-examine-strain = Его штамм — [color={ $color }]{ $name }[/color].
blob-time-minutes = { $minutes } мин. { $seconds } сек.
blob-time-seconds = { $seconds } сек.

blob-hud-power = Ресурсы: { $points }
blob-hud-core = Ядро: { $health }%
blob-hud-progress = До критической массы: { $count }/{ $win }
blob-hud-rerolls = Бесплатных смен штамма: { $count }
blob-hud-manual = До ручного размещения: { $seconds } с
blob-hud-auto = До авторазмещения: { $seconds } с

blob-announcement-outbreak = Вспышка биологической угрозы 5-го уровня зафиксирована на борту станции. Всему персоналу надлежит сдержать её распространение любой ценой!
blob-announcement-outbreak-sender = Биологическая угроза
blob-announcement-critical = Биоугроза достигла критической массы. Потеря станции неминуема.
blob-announcement-critical-sender = Биологическая тревога
blob-announcement-countermeasures = Задействованы экспериментальные, секретные и очень дорогие контрмеры, предотвратившие полную потерю станции. Изначальный провал сдерживания биоугрозы будет отмечен в отчёте о работе станции. Ожидайте дальнейших взысканий.
blob-announcement-countermeasures-sender = Аварийные контрмеры против биоугрозы
blob-victory-world = { $name } поглотил станцию неудержимой волной!

## Размещение ядра

blob-core-invalid-spot = Здесь нельзя разместить ядро!
blob-core-too-dense = Это место слишком плотное для ядра блоба!
blob-core-too-early = Ещё слишком рано размещать ядро!
blob-core-someone-close = Кто-то слишком близко, чтобы разместить ядро!
blob-core-someone-see = Отсюда кто-то может увидеть ваше ядро!
blob-core-already-blob = Здесь уже есть блоб!
blob-carrier-autopop = [color=#d94b4b]Вы автоматически разорвётесь и разместите ядро через { $time }.[/color]
blob-carrier-greet = Используйте способность «Выпустить», чтобы разместить ядро блоба! Лучше делать это вдали от людей — против вас будет весь экипаж!
blob-carrier-random-location = Ваше местоположение не подходит, а пора выпускать блоба — вас перенесло в случайное место!

## Действия

blob-cannot-afford = Не хватает ресурсов, нужно хотя бы { $cost }!
blob-need-more = нужно ещё { $amount }!
blob-no-adjacent = Рядом с целью нет блоба!
blob-already-there = Там уже есть блоб!
blob-max-tiles = достигнут предел тайлов!
blob-expand-failed = не удалось разрастись!
blob-off-station = вне станции, не засчитается!
blob-no-blob-here = Здесь нет блоба!
blob-no-blob-here-short = здесь нет блоба!
blob-need-normal = Этот блоб не подходит, найдите обычный.
blob-need-normal-short = нужен обычный блоб!
blob-must-be-on-station = Этот тип блоба можно разместить только на станции!
blob-cant-place-off-station = нельзя вне станции!
blob-need-node = Разместите этот блоб ближе к узлу или ядру!
blob-need-node-short = далеко от узла или ядра!
blob-similar-nearby = Рядом есть похожий блоб, отойдите дальше чем на { $distance } тайлов!
blob-too-close = слишком близко!
blob-upgraded = улучшено до «{ $name }»!
blob-shield-max = Этот сильный блоб уже настолько прочен, насколько возможно!
blob-shield-damaged = Этот сильный блоб слишком повреждён для изменения!
blob-shield-reflective = Вы покрываете сильный блоб отражающей слизью: теперь он отражает снаряды ценой прочности.
blob-must-be-on-factory = Вы должны быть над фабрикой!
blob-factory-sustaining = Эта фабрика уже поддерживает блоббернаута.
blob-factory-damaged = Эта фабрика слишком повреждена, чтобы поддерживать блоббернаута.
blob-blobbernaut-attempt = Вы пытаетесь создать блоббернаута.
blob-blobbernaut-no-ghost = Не удалось вдохнуть разум в блоббернаута. Ресурсы возвращены, попробуйте позже.
blob-must-be-on-node = Вы должны быть над узлом!
blob-no-core = У вас нет ядра, и скоро вы погибнете! Покойтесь с миром.
blob-cannot-relocate = Сюда нельзя перенести ядро!
blob-reroll-need = Для смены штамма нужно хотя бы { $cost } ресурсов!
blob-rally = Вы созываете споры.
blob-no-blob-there = Там нет блоба!
blob-cannot-remove = Этот блоб нельзя удалить.
blob-too-many-resources = Слишком много ресурсов, чтобы удалить этот блоб!
blob-removed-gain = Получено { $amount } ресурсов за удаление «{ $name }».
blob-resource-gained = +{ $amount }
blob-node-entry = Узел блоба №{ $index } ({ $area })
blob-node-unknown-area = неизвестно
blob-zombification-spent = Потрачено 5 ресурсов на зомбирование { $target }.

## Штаммы

blob-strain-now = Ваш штамм теперь: [color={ $color }][bold]{ $name }[/bold][/color]!
blob-strain-desc = Штамм [color={ $color }][bold]{ $name }[/bold][/color] { $desc }
blob-minion-strain-now = Штамм вашего сознания теперь: [color={ $color }][bold]{ $name }[/bold][/color]!
blob-strain-message-default = Блоб бьёт вас
blob-strain-blobbernaut-slams = бьёт
blob-strain-blobbernaut-splashes = обливает
blob-strain-blobbernaut-injects = впрыскивает
blob-strain-blobbernaut-blasts = взрывает
blob-strain-blobbernaut-emits-slime = выпускает слизь на
blob-strain-blobbernaut-stabs = протыкает
blob-strain-blobbernaut-synchronously = синхронно бьёт

blob-strain-blazing-oil-name = Пылающее масло
blob-strain-blazing-oil-desc = наносит средне-высокий урон ожогами в обход брони и поджигает цели.
blob-strain-blazing-oil-effect = также выпускает вспышки пламени при ожогах, но получает урон от воды.
blob-strain-blazing-oil-analyzer-damage = Наносит средне-высокий урон ожогами и поджигает цели.
blob-strain-blazing-oil-analyzer-effect = Выпускает огонь при ожогах, но получает урон от воды и других тушащих жидкостей.
blob-strain-blazing-oil-message = Блоб обливает вас горящим маслом
blob-strain-blazing-oil-message-living = , и вы чувствуете, как ваша кожа обугливается и плавится

blob-strain-cryogenic-poison-name = Криогенный яд
blob-strain-cryogenic-poison-desc = вводит в цели замораживающий яд: мало урона сразу, но много со временем.
blob-strain-cryogenic-poison-analyzer-damage = Вводит замораживающий яд, постепенно превращающий внутренние органы цели в лёд.
blob-strain-cryogenic-poison-message = Блоб колет вас
blob-strain-cryogenic-poison-message-living = , и вы чувствуете, как ваши внутренности застывают

blob-strain-debris-devourer-name = Пожиратель обломков
blob-strain-debris-devourer-desc = метает в цели поглощённый мусор. Без мусора наносит очень слабый урон.
blob-strain-debris-devourer-analyzer-damage = Наносит очень слабый урон и может хватать оружие ближнего боя.
blob-strain-debris-devourer-analyzer-effect = Поглощает брошенные на станции предметы и выпускает их при атаке или получении урона.
blob-strain-debris-devourer-message = Блоб бьёт вас обломками

blob-strain-distributed-neurons-name = Распределённые нейроны
blob-strain-distributed-neurons-desc = наносит средне-низкий урон токсинами и превращает бессознательные цели в зомби блоба.
blob-strain-distributed-neurons-effect = также выпускает хрупкие споры при гибели. Споры фабрик разумны.
blob-strain-distributed-neurons-short = наносит средне-низкий урон токсинами и убивает бессознательные цели. Споры фабрик разумны.
blob-strain-distributed-neurons-analyzer-damage = Наносит средне-низкий урон токсинами и убивает людей без сознания.
blob-strain-distributed-neurons-analyzer-effect = Выпускает споры при гибели. Споры фабрик разумны.
blob-strain-distributed-neurons-message = Блоб бьёт вас
blob-strain-distributed-neurons-message-living = , и вы чувствуете усталость

blob-strain-electromagnetic-web-name = Электромагнитная сеть
blob-strain-electromagnetic-web-desc = наносит высокий урон ожогами и бьёт цели ЭМИ.
blob-strain-electromagnetic-web-effect = также получает намного больше урона и выпускает ЭМИ при гибели.
blob-strain-electromagnetic-web-analyzer-damage = Наносит слабый урон ожогами и бьёт цели ЭМИ.
blob-strain-electromagnetic-web-analyzer-effect = Хрупок ко всем типам урона, особенно к механическому. При гибели выпускает небольшой ЭМИ.
blob-strain-electromagnetic-web-message = Блоб бьёт вас

blob-strain-energized-jelly-name = Заряженное желе
blob-strain-energized-jelly-desc = наносит высокий урон выносливости и средний удушьем, не давая целям дышать.
blob-strain-energized-jelly-effect = также проводит электричество, но получает урон от ЭМИ.
blob-strain-energized-jelly-analyzer-damage = Наносит высокий урон выносливости, средний удушьем и не даёт целям дышать.
blob-strain-energized-jelly-analyzer-effect = Невосприимчив к электричеству и легко его проводит, но уязвим к ЭМИ.
blob-strain-energized-jelly-message = Блоб бьёт вас

blob-strain-explosive-lattice-name = Взрывная решётка
blob-strain-explosive-lattice-desc = атакует небольшими взрывами, нанося средний урон ушибами и ожогами всем рядом. Споры взрываются при гибели.
blob-strain-explosive-lattice-effect = также устойчив к взрывам, но получает больше урона от огня и других источников энергии.
blob-strain-explosive-lattice-analyzer-damage = Наносит средний урон ушибами и ожогами в небольшом взрыве вокруг цели. Споры взрываются при гибели.
blob-strain-explosive-lattice-analyzer-effect = Очень устойчив к взрывам, но получает больше урона от огня и других источников энергии.
blob-strain-explosive-lattice-message = Блоб взрывается на вас

blob-strain-networked-fibers-name = Сетевые волокна
blob-strain-networked-fibers-desc = наносит высокий урон ушибами и ожогами и быстрее копит ресурсы, но растёт только вручную рядом с ядром или узлами.
blob-strain-networked-fibers-short = наносит высокий урон ушибами и ожогами.
blob-strain-networked-fibers-effect = при ручном росте рядом с ядром или узлом перемещает их.
blob-strain-networked-fibers-analyzer-damage = Наносит высокий урон ушибами и ожогами.
blob-strain-networked-fibers-analyzer-effect = Подвижен и быстро накапливает ресурсы.
blob-strain-networked-fibers-message = Блоб бьёт вас

blob-strain-pressurized-slime-name = Сжатая слизь
blob-strain-pressurized-slime-desc = наносит слабый урон ушибами и удушьем, высокий урон выносливости и делает пол под целями очень скользким.
blob-strain-pressurized-slime-effect = также делает пол скользким рядом с атакованными блобами.
blob-strain-pressurized-slime-analyzer-damage = Наносит слабый урон ушибами и удушьем, высокий выносливости и делает пол под целями скользким, туша их. Устойчив к ушибам.
blob-strain-pressurized-slime-analyzer-effect = При атаке или гибели смазывает пол вокруг и тушит всё на нём.
blob-strain-pressurized-slime-message = Блоб окатывает вас
blob-strain-pressurized-slime-message-living = , и вы хватаете ртом воздух

blob-strain-reactive-spines-name = Реактивные шипы
blob-strain-reactive-spines-desc = наносит высокий урон ушибами сквозь броню и биозащиту.
blob-strain-reactive-spines-effect = также отвечает на механический урон и ожоги, атакуя всё вокруг.
blob-strain-reactive-spines-analyzer-damage = Наносит высокий урон ушибами в обход брони и биозащиты.
blob-strain-reactive-spines-analyzer-effect = Получив ожоги или механический урон, яростно бьёт всё вокруг.
blob-strain-reactive-spines-message = Блоб протыкает вас

blob-strain-regenerative-materia-name = Регенеративная материя
blob-strain-regenerative-materia-desc = наносит средний урон токсинами и вводит яд, наносящий ещё урон и убеждающий цели, что они полностью здоровы. Ядро регенерирует намного быстрее.
blob-strain-regenerative-materia-analyzer-damage = Наносит средний урон токсинами и вводит яд, наносящий ещё урон и убеждающий цели, что они здоровы. Ядро регенерирует намного быстрее.
blob-strain-regenerative-materia-message = Блоб бьёт вас
blob-strain-regenerative-materia-message-living = , и вы чувствуете себя [italic]живым[/italic]

blob-strain-replicating-foam-name = Размножающаяся пена
blob-strain-replicating-foam-desc = наносит средний урон ушибами и иногда разрастается повторно при росте.
blob-strain-replicating-foam-short = наносит средний урон ушибами.
blob-strain-replicating-foam-effect = также разрастается от ожогов, но получает больше механического урона.
blob-strain-replicating-foam-analyzer-damage = Наносит средний урон ушибами.
blob-strain-replicating-foam-analyzer-effect = Разрастается от ожогов, иногда растёт повторно и хрупок к механическому урону.
blob-strain-replicating-foam-message = Блоб бьёт вас

blob-strain-shifting-fragments-name = Смещающиеся фрагменты
blob-strain-shifting-fragments-desc = наносит средний урон ушибами.
blob-strain-shifting-fragments-effect = также части блоба смещаются при атаке.
blob-strain-shifting-fragments-analyzer-damage = Наносит средний урон ушибами.
blob-strain-shifting-fragments-analyzer-effect = При атаке может сместиться от нападающего.
blob-strain-shifting-fragments-message = Блоб бьёт вас

blob-strain-synchronous-mesh-name = Синхронная сетка
blob-strain-synchronous-mesh-desc = наносит слабый урон ушибами, но каждый блоб рядом тоже бьёт цель с нарастающим уроном.
blob-strain-synchronous-mesh-effect = также распределяет урон между блобами рядом с атакованным.
blob-strain-synchronous-mesh-analyzer-damage = Наносит слабый урон ушибами, растущий с числом блобов рядом с целью.
blob-strain-synchronous-mesh-analyzer-effect = Распределяет полученный урон между всеми блобами рядом.
blob-strain-synchronous-mesh-message = Блобы бьют вас

## Структуры

blob-normal-name = обычный блоб
blob-normal-name-fragile = хрупкий обычный блоб
blob-normal-name-dead = мёртвый обычный блоб
blob-normal-desc = Плотная стена извивающихся щупалец.
blob-normal-desc-fragile = Тонкая решётка слегка подёргивающихся щупалец.
blob-normal-desc-dead = Плотная стена безжизненных щупалец.
blob-strong-name = сильный блоб
blob-strong-name-weakened = ослабленный сильный блоб
blob-strong-desc = Сплошная стена слегка подёргивающихся щупалец.
blob-strong-desc-damaged = Стена подёргивающихся щупалец.
blob-reflective-name = отражающий блоб
blob-reflective-name-weakened = ослабленный отражающий блоб
blob-reflective-desc = Сплошная стена слегка подёргивающихся щупалец с отражающим блеском.
blob-reflective-desc-damaged = Стена подёргивающихся щупалец с отражающим блеском.

blob-examine-progress = [bold]Прогресс до критической массы:[/bold] { $count }/{ $win }.
blob-examine-made-of = Похоже, он состоит из: { $chem }.
blob-chem-unknown = какой-то органической ткани
blob-attacks-you = Блоб атакует вас!
blob-retaliates = Блоб отвечает, хлеща вокруг!
blob-ruptures = Блоб лопается, обдавая всё вокруг жидкостью!
blob-spore-floats-free = Из блоба вылетает спора!
blob-blobbernaut-rips-out = Блоббернаут { $verb } фабрику и выбирается наружу!
blob-verb-rips = разрывает
blob-verb-tears = раздирает
blob-verb-shreds = раскурочивает

blob-analyzer-header = [bold]Анализатор пищит и сообщает:[/bold]
blob-analyzer-material = [bold]Материал:[/bold] [color={ $color }]{ $name }[/color].
blob-analyzer-effects = [bold]Эффекты материала:[/bold] { $text }
blob-analyzer-properties = [bold]Свойства материала:[/bold] { $text }
blob-analyzer-neutralized = [bold]Ядро блоба нейтрализовано. Критическая масса недостижима.[/bold]
blob-analyzer-type = [bold]Тип блоба:[/bold] { $name }
blob-analyzer-health = [bold]Прочность:[/bold] { $health }/{ $max }
blob-analyzer-scanner = [bold]Эффекты:[/bold] { $text }
blob-type-normal = ОБЫЧНЫЙ БЛОБ
blob-type-strong = СИЛЬНЫЙ БЛОБ
blob-type-reflective = ОТРАЖАЮЩИЙ БЛОБ
blob-type-core = ЯДРО БЛОБА
blob-type-node = УЗЕЛ БЛОБА
blob-type-factory = ФАБРИКА БЛОБА
blob-type-resource = РЕСУРСНЫЙ БЛОБ
blob-scanner-na = Нет.
blob-scanner-normal-weak = Сейчас уязвим к механическому урону.
blob-scanner-shield = Препятствует распространению изменений атмосферы.
blob-scanner-core = Направляет рост блоба, постепенно разрастается и поддерживает споры и блоббернаутов рядом.
blob-scanner-node = Постепенно разрастается и поддерживает споры и блоббернаутов рядом.
blob-scanner-factory = Каждые несколько секунд создаёт спору.
blob-scanner-factory-naut = Сейчас поддерживает блоббернаута, из-за чего хрупка и не создаёт споры.
blob-scanner-resource = Постепенно снабжает блоб ресурсами, ускоряя рост.

## Миньоны

blob-telepathy-overmind = [font size=14][color=#4aa34a][bold][Телепатия Блоба] { $name }([color={ $color }]{ $strain }[/color])[/bold] { $message }[/color][/font]
blob-telepathy-minion = [color=#4aa34a][bold][Телепатия Блоба] { $name }[/bold] { $message }[/color]
blob-minion-objective = Защищайте ядро блоба любой ценой.
blob-factory-destroyed-dying = Ваша фабрика уничтожена! Вы чувствуете, что умираете!
blob-blobbernaut-greet-1 = Вы сильны, вас трудно убить, и вы медленно восстанавливаетесь рядом с узлами и ядром, [color=#d94b4b][bold]но медленно умираете вдали от блоба[/bold][/color] или если создавшая вас фабрика уничтожена.
blob-blobbernaut-greet-2 = Вы можете [bold]телепатически[/bold] общаться с другими блоббернаутами и сознанием, просто говоря.
blob-blobbernaut-greet-strain = Реагент вашего сознания: [color={ $color }][bold]{ $name }[/bold][/color]!
blob-corpse-rises = Труп { $corpse } внезапно поднимается!
blob-zombie-bursts = { CAPITALIZE($zombie) } лопается изнутри!
blob-zombie-collapses = { CAPITALIZE($zombie) } валится на землю!
blob-spore-explodes = { CAPITALIZE($spore) } взрывается облаком газа!

blob-ghost-role-overmind-name = Сознание Блоба
blob-ghost-role-overmind-desc = Вы — сознание Блоба. Разместите ядро, разрастайтесь и поглотите станцию.
blob-ghost-role-blobbernaut-name = Блоббернаут
blob-ghost-role-blobbernaut-desc = Огромная подвижная масса блоба. Защищайте ядро и держитесь рядом с блобом.
blob-ghost-role-spore-name = Спора блоба
blob-ghost-role-spore-desc = Разумная спора блоба (распределённые нейроны). Защищайте блоб и поднимайте трупы.
blob-ghost-role-zombie-name = Зомби блоба
blob-ghost-role-zombie-desc = Труп, поднятый блобом. Защищайте ядро.
blob-ghost-role-independent-name = Свободная спора
blob-ghost-role-independent-desc = Спора, рождённая без сознания блоба. Враждебна ко всему, что ей не родня.

alerts-blobbernaut-nofactory-name = Нет фабрики
alerts-blobbernaut-nofactory-desc = Ваша фабрика уничтожена. Вы медленно умираете.

## Реагенты

reagent-name-blob-blazing-oil = пылающее масло
reagent-desc-blob-blazing-oil = Реагент, из которого состоит блоб пылающего масла.
reagent-name-blob-cryogenic-poison = криогенный яд
reagent-desc-blob-cryogenic-poison = Реагент, из которого состоит блоб криогенного яда.
reagent-name-blob-cryogenic-poison-chem = криогенный яд блоба
reagent-desc-blob-cryogenic-poison-chem = Замораживающий яд, наносящий сильный урон со временем. Блобы криогенного яда вводят его жертвам.
reagent-name-blob-debris-devourer = пожиратель обломков
reagent-desc-blob-debris-devourer = Реагент, из которого состоит блоб-пожиратель обломков.
reagent-name-blob-distributed-neurons = распределённые нейроны
reagent-desc-blob-distributed-neurons = Реагент, из которого состоит блоб распределённых нейронов.
reagent-name-blob-electromagnetic-web = электромагнитная сеть
reagent-desc-blob-electromagnetic-web = Реагент, из которого состоит блоб электромагнитной сети.
reagent-name-blob-energized-jelly = заряженное желе блоба
reagent-desc-blob-energized-jelly = Реагент, из которого состоит блоб заряженного желе.
reagent-name-blob-explosive-lattice = взрывная решётка
reagent-desc-blob-explosive-lattice = Реагент, из которого состоит блоб взрывной решётки.
reagent-name-blob-networked-fibers = сетевые волокна
reagent-desc-blob-networked-fibers = Реагент, из которого состоит блоб сетевых волокон.
reagent-name-blob-pressurized-slime = сжатая слизь
reagent-desc-blob-pressurized-slime = Реагент, из которого состоит блоб сжатой слизи.
reagent-name-blob-reactive-spines = реактивные шипы
reagent-desc-blob-reactive-spines = Реагент, из которого состоит блоб реактивных шипов.
reagent-name-blob-regenerative-materia = регенеративная материя
reagent-desc-blob-regenerative-materia = Реагент, из которого состоит блоб регенеративной материи.
reagent-name-blob-regenerative-materia-chem = регенеративная материя блоба
reagent-desc-blob-regenerative-materia-chem = Вещество, наносящее урон токсинами и убеждающее цель, что она полностью здорова.
reagent-name-blob-replicating-foam = размножающаяся пена
reagent-desc-blob-replicating-foam = Реагент, из которого состоит блоб размножающейся пены.
reagent-name-blob-shifting-fragments = смещающиеся фрагменты
reagent-desc-blob-shifting-fragments = Реагент, из которого состоит блоб смещающихся фрагментов.
reagent-name-blob-synchronous-mesh = синхронная сетка
reagent-desc-blob-synchronous-mesh = Реагент, из которого состоит блоб синхронной сетки.
reagent-name-blob-spore-toxin = споровый токсин
reagent-desc-blob-spore-toxin = Токсин из спор блоба. Наносит урон токсинами.

## Сущности

ent-BlobNormal = обычный блоб
    .desc = Плотная стена извивающихся щупалец.
ent-BlobStrong = сильный блоб
    .desc = Сплошная стена слегка подёргивающихся щупалец.
ent-BlobReflective = отражающий блоб
    .desc = Сплошная стена слегка подёргивающихся щупалец с отражающим блеском.
ent-BlobCore = ядро блоба
    .desc = Огромная пульсирующая жёлтая масса.
ent-BlobNode = узел блоба
    .desc = Большая пульсирующая жёлтая масса.
ent-BlobFactory = фабрика блоба
    .desc = Толстый шпиль из щупалец.
ent-BlobResource = ресурсный блоб
    .desc = Тонкий шпиль из слегка покачивающихся щупалец.
ent-MobBlobOvermind = Сознание Блоба
    .desc = Сознание. Оно управляет блобом.
ent-MobBlobSpore = спора блоба
    .desc = Парящая хрупкая спора.
ent-MobBlobSporeWeak = хрупкая спора блоба
    .desc = Парящая хрупкая спора.
ent-MobBlobSporeIndependent = спора блоба
    .desc = Парящая хрупкая спора.
ent-MobBlobbernaut = блоббернаут
    .desc = Огромный подвижный кусок массы блоба.
ent-SpawnPointGhostBlobbernaut = точка появления блоббернаута
ent-SpawnPointGhostBlobOvermind = точка появления сознания блоба
ent-ActionBlobJumpToCore = К ядру
    .desc = Переносит камеру к ядру блоба. До размещения — пытается разместить ядро здесь.
ent-ActionBlobJumpToNode = К узлу
    .desc = Переносит камеру к выбранному узлу блоба.
ent-ActionBlobCreateResource = Ресурсный блоб (40)
    .desc = Создаёт ресурсный блоб за 40 ресурсов. Ресурсные блобы приносят ресурсы каждые несколько секунд.
ent-ActionBlobCreateNode = Узел блоба (50)
    .desc = Создаёт узел блоба за 50 ресурсов. Узлы разрастаются и активируют ресурсные блобы и фабрики рядом.
ent-ActionBlobCreateFactory = Фабрика блоба (60)
    .desc = Создаёт фабрику за 60 ресурсов. Фабрики создают споры каждые несколько секунд.
ent-ActionBlobCreateBlobbernaut = Блоббернаут (40)
    .desc = Создаёт сильного разумного блоббернаута из фабрики за 40 ресурсов. Фабрика станет хрупкой и перестанет создавать споры.
ent-ActionBlobReadaptStrain = Сменить штамм
    .desc = Выбор нового штамма из 6 случайных за 40 ресурсов. Первая смена и одна каждые 4 минуты — бесплатны.
ent-ActionBlobRelocateCore = Перенести ядро (80)
    .desc = Меняет местами узел и ядро за 80 ресурсов.
ent-ActionBlobPop = Выпустить
    .desc = Выпустите блоба!

## Подсказка оверманда (AntagInfoBlob)

blob-overmind-help = [bold]Основы:[/bold] разрастаясь, вы атакуете людей, повреждаете объекты или создаёте обычный блоб, если клетка свободна.
    • [bold]Размещение:[/bold] ядро размещается кнопкой «Разместить ядро» на панели действий.
    • [bold]Горячие клавиши:[/bold]
    ЛКМ — разрастись
    Средняя кнопка мыши — созвать споры
    Ctrl+ЛКМ — создать сильный блоб (по сильному — отражающий)
    Alt+ЛКМ — удалить блоб
    Речь отправляет телепатическое сообщение всем блобам.
    • [bold]Структуры:[/bold] сильные блобы дорогие, но прочные, огнеупорные и держат воздух; отражающие отражают снаряды ценой прочности. Ресурсные блобы дают ресурсы, фабрики — споры; оба должны стоять рядом с узлом или ядром. Узлы растут, как ядро, и активируют ресурсные блобы и фабрики. Рост в космос проваливается в 80% случаев.
    • [bold]Миньоны:[/bold] блоббернаутов создают на фабриках — они сильны и умны, но фабрика становится хрупкой. Споры появляются сами, атакуют врагов рядом и поднимают трупы.
blob-button-place-core = Разместить ядро
blob-button-place-core-desc = Попытаться разместить ядро блоба здесь.
blob-button-jump-core = К ядру
blob-button-jump-core-desc = Переносит камеру к ядру блоба.
blob-ghost-notify-burst = [bold]Пробуждение блоба![/bold] Носитель блоба разорвался: { $area }.
blob-minion-too-full = Вы слишком сыты, чтобы съесть ещё мусора.

## Заражение спорами

blob-spore-latching = { CAPITALIZE($spore) } забирается на голову { $target }!
blob-infection-started = Что-то склизкое обволакивает вашу голову... Вы чувствуете зов Блоба!
blob-infection-greet = [color=#4aa34a][bold]Вас заразил Блоб![/bold][/color] Теперь вы его союзник: защищайте ядро и помогайте ему расти. Для связи с другими блобами используйте каналы блоба (:б, :л).
ent-ClothingHeadHelmetBlob = панцирь Блоба
    .desc = Затвердевшая масса биомассы, принявшая форму защиты для головы.
