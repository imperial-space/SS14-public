## Культ крови Нар'Си (перенос 1:1 из SS13 / BandaStation).

roles-antag-cultist-name = Культист
roles-antag-cultist-objective = Служите Геометру Крови. Принесите жертву и призовите Нар'Си.
objective-issuer-cult = [color=#8B0000]Нар'Си[/color]
reagent-name-unholy-water = нечестивая вода
reagent-desc-unholy-water = То, чего не должно существовать на этом плане бытия.
tiles-cult-floor = гравированный пол
alerts-cult-bloodsense-name = Чувство крови
alerts-cult-bloodsense-desc = Позволяет ощущать кровь тех, кого жаждет Нар'Си.
cult-menu-ok = Готово
cult-paranormal-sender = Центральное Командование: Отдел паранормальных явлений
cult-ghost-role-desc = Служите культу Нар'Си.
cult-ghost-role-rules = Вы — антагонист культа крови. Помогайте культу любой ценой.
cult-harvester-ghost-role = Жнец Нар'Си
cult-harvester-ghost-role-desc = Станьте оккультным жнецом и собирайте урожай для Геометра.

## Культист

imperial-cult-role-greeting = Вы — культист Нар'Си! Принесите жертву и призовите Геометра Крови.
cult-greet = [color=#960000][bold]Вы — член культа![/bold] Ритуальный кинжал позволяет чертить руны, а «Подготовить магию крови» — заготавливать заклинания.[/color]
cult-deconvert-others = Старая вера вернулась к { $target }!
cult-deconvert-self = [color=#FF0000][bold]Незнакомый белый свет вспыхивает в твоем сознании, очищая от скверны Геометра и всех твоих воспоминаний о том, что ты был её слугой.[/bold][/color]
cult-equip-item = [color=#FF5050]{ $item } был помещен в ваш { $where }.[/color]
cult-equip-backpack = рюкзак
cult-equip-pocket = карман
cult-equip-hands = руки
cult-equip-floor = пол
cult-risen = [color=#960000][font size=16]Покров слабеет с ростом культа - ваши глаза начинают светиться...[/font][/color]
cult-ascendent = [color=#960000][font size=16]Ваш культ набирает силу, и приближается красная жатва - вы не сможете долго скрывать свою истинную природу!![/font][/color]
cult-ascendent-announcement = Мы фиксируем активность из другого измерения, связаную с культом "Нар'Си" на вашей станции. Согласно нашей информации, { $percent }% экипажа станции были порабощены культом. Сотрудники службы безопасности наделены правом беспрепятственно применять летальную силу против культистов. Остальному экипажу надлежит приготовиться защищать себя и свои отделы, не ведя охоту на культистов. Погибшие члены экипажа должны быть реанимированы и деконвертированы, как только ситуация будет взята под контроль.
cult-title-master = Мастер
cult-title-acolyte = Аколит
cult-title-construct = Конструкт

## Общение

cult-communion-title = Общение
cult-communion-prompt = Что вы хотите сказать культу?
cult-communion-message = [color=#960000][italic][bold]{ $title } { $name }:[/bold] { $message }[/italic][/color]
cult-communion-message-master = [color=#960000][font size=16][bold]{ $title } { $name }:[/bold] { $message }[/font][/color]
cult-spirit-communion-message = [color=#960000][bold][italic]{ $name }: { $message }[/italic][/bold][/color]
cult-commune-converted = [color=#960000]{ $name } присоединяется к культу![/color]

## Мастер

cult-master-greet = [color=#960000][font size=16]Вы - Мастер культа. Как Мастер культа у вас громче голос при коммуникации и вы можете помечать цели, такие как локацию и не культиста, чтобы направить культ туда. Вы можете призывать живых культистов к вашей локации [bold][italic]единожды[/italic][/bold]. Используйте свои способности для победы любой ценой.[/font][/color]
cult-master-died = [color=#960000][font size=16]Мастер культа, { $name } пал в { $area }![/font][/color]
cult-mantle-title = Кому передать мантию?
cult-mantle-nobody = [color=#960000]Некому передать мантию.[/color]
cult-mantle-received = [color=#960000][font size=16]{ $name } передаёт вам мантию Мастера культа![/font][/color]
cult-mantle-passed = [color=#960000][bold]{ $old } передаёт мантию Мастера культа { $new }![/bold][/color]
cult-reckoning-veil-weak = [color=#960000][font size=16]Завеса здесь слишком слаба! Отойдите туда, где она достаточно прочна для этой магии.[/font][/color]
cult-reckoning-no-space = Вам нужно больше места, чтобы призвать культ!
cult-mark-already = [color=#960000][bold]Культ уже назначил цель![/bold][/color]
cult-mark-set = [color=#960000][font size=16][bold]{ $marker } отмечает { $target } в { $area } как главный приоритет культа. Доберитесь туда немедленно![/bold][/font][/color]
cult-mark-set-narsie = [color=#960000][font size=16][bold]{ $target } в { $area } — главный приоритет культа, отправляйтесь туда немедленно![/bold][/font][/color]
cult-mark-expired = [color=#960000][font size=16][bold]Кровавая метка закончилась![/bold][/font][/color]
cult-mark-lost = [color=#960000][font size=16][bold]Цель кровавой метки потеряна![/bold][/font][/color]
cult-pulse-invalid = [color=#960000]Вы можете переместить только культиста или постройку культа.[/color]
cult-pulse-selected = [color=#960000]Вы готовитесь переместить { $target }.[/color]
cult-pulse-too-far = [color=#960000]Перемещать можно не дальше чем на 16 тайлов![/color]
cult-pulse-blocked = [color=#960000]Это место занято.[/color]
cult-pulse-success = [color=#960000]Импульс кровавой магии проходит сквозь вас, перенося { $target } сквозь время и пространство.[/color]
cult-ghostmark-set = [color=#960000][bold]Вы отметили { $target } для культа! Метка продержится минуту.[/bold][/color]
cult-ghostmark-reset = [color=#960000][bold]Вы сбросили кровавую метку культа.[/bold][/color]
cult-ghostmark-not-ready = [color=#960000][bold]Вы ещё не готовы поставить новую кровавую метку![/bold][/color]

## Руны

cult-rune-examine = [color=#960000][bold]Название:[/bold] { $name }[/color]
cult-rune-examine-effect = [color=#960000][bold]Эффект:[/bold] { $desc }[/color]
cult-rune-examine-req = [color=#960000][bold]Нужно аколитов:[/bold] { $req }[/color]
cult-rune-examine-keyword = [color=#960000][bold]Ключевое слово:[/bold] { $keyword }[/color]
cult-rune-examine-sacrifices = [color=#960000][bold]Неоплаченных жертв:[/bold] { $count }[/color]
cult-rune-cant-understand = Вы не способны понять слова { $rune }.
cult-rune-construct-cant = Вы не способны призвать эту руну!
cult-rune-need-more = Нужно ещё { $count } культистов рядом, чтобы использовать эту руну.
cult-rune-fail = Знаки вспыхивают слабым красным светом и гаснут.
cult-rune-saps = [color=#960000][italic]{ $rune } высасывает ваши силы![/italic][/color]

cult-rune-malformed-name = искажённая руна
cult-rune-malformed-desc = бессмысленная руна, написанная тарабарщиной. Ничего хорошего от её призыва не будет.
cult-rune-offer-name = Подношение
cult-rune-offer-desc = преподносит стоящего на ней некультиста Нар'Си, обращая его или принося в жертву.
cult-rune-offer-req = 2 для обращения, 3 для живых жертв и цели жертвоприношения.
cult-rune-offer-pulse = { $rune } пульсирует кроваво-красным!
cult-rune-offer-need-two = Нужно как минимум два призывающих, чтобы обратить { $target }!
cult-rune-offer-shielded = Что-то защищает разум { $target }!
cult-rune-offer-writhes = { $target } корчится от боли, а знаки под ним пылают кроваво-красным!
cult-rune-offer-writhes-heal = { $target } корчится от боли, хотя его раны затягиваются!
cult-rune-offer-scream = [color=#960000][font size=16][italic]АААААААААААААА-[/italic][/font][/color]
cult-rune-offer-converted-1 = [color=#960000][bold][italic]Ваша кровь пульсирует. Голова раскалывается. Мир становится красным. Внезапно вы осознаёте ужасную, ужасную истину. Покров реальности сорван, и нечто злое пускает корни.[/italic][/bold][/color]
cult-rune-offer-converted-2 = [color=#960000][bold][italic]Помогайте новым товарищам в их тёмных делах. Их цель — ваша, а ваша — их. Превыше всего вы служите Геометру. Верните его.[/italic][/bold][/color]
cult-rune-offer-need-three = [color=#960000][italic]{ $target } слишком сильно связан с этим миром! Нужно три аколита![/italic][/color]
cult-rune-offer-desired = [color=#960000][font size=16]«Да! Это тот, кого я желаю! Вы хорошо потрудились.»[/font][/color]
cult-rune-offer-accept = [color=#960000][font size=16]«Я принимаю эту жертву.»[/font][/color]
cult-rune-offer-meager = [color=#960000][font size=16]«Я принимаю эту жалкую жертву.»[/font][/color]

cult-rune-empower-name = Усиление
cult-rune-empower-desc = позволяет культистам готовить больше магии крови с гораздо меньшими затратами.

cult-rune-teleport-name = Телепорт
cult-rune-teleport-desc = переносит всё, что на ней стоит, к другой выбранной руне телепорта.
cult-rune-teleport-title = Руна, к которой переместиться
cult-rune-teleport-none = Нет подходящих рун для телепортации!
cult-rune-teleport-blocked = Целевая руна заблокирована. Попытка телепортироваться туда была бы крайне неразумной.
cult-rune-teleport-crack = Раздаётся резкий хлопок врывающегося воздуха, и всё над руной исчезает!
cult-rune-teleport-boom = Раздаётся грохот вырывающегося воздуха, и над руной что-то появляется!
cult-rune-teleport-self = [color=#960000]Ваше зрение мутнеет, и вы внезапно оказываетесь в другом месте.[/color]
cult-rune-teleport-sent = [color=#960000]Вы отправляете всё, что было над руной, прочь.[/color]
cult-rune-teleport-self-fail = [color=#960000]Ваше зрение на мгновение мутнеет, но ничего не происходит.[/color]
cult-rune-teleport-sent-fail = [color=#960000]Вы пытаетесь отправить всё над руной прочь, но телепортация не удаётся.[/color]
cult-rune-portal-space = [bold]Разрыв в реальности открывает чёрную пустоту с точками света... что-то недавно прибыло сюда из космоса. Пустота тянет вас на { $dir }![/bold]
cult-rune-portal-lava = [bold]Разрыв в реальности открывает бурлящую реку лавы... что-то недавно прибыло сюда с Лаваленда![/bold]

cult-rune-narsie-name = Нар'Си
cult-rune-narsie-desc = разрывает барьеры измерений, призывая Геометра. Требует 9 призывающих.
cult-rune-narsie-wrong-place = [color=#960000][font size=16]Геометра можно призвать только там, где завеса тонка — в { $spots }![/font][/color]
cult-rune-narsie-already = Нар'Си уже на этом плане!

cult-rune-revive-name = Воскрешение
cult-rune-revive-desc = требует мёртвого, бездушного или неактивного культиста на руне. За каждые три принесённых тёмному покровителю тела одно будет исцелено, а разум пробуждён.
cult-rune-revive-none = [color=#960000][italic]На руне нет мёртвых культистов![/italic][/color]
cult-rune-revive-title = Кого воскресить?
cult-rune-revive-moved = [color=#960000][italic]Культиста для воскрешения сдвинули![/italic][/color]
cult-rune-revive-more-sacrifices = Культ должен принести ещё { $count } жертв, прежде чем сможет воскресить культиста!
cult-rune-revive-arise = [color=#960000][font size=16]«PASNAR SAVRAE YAM'TOTH. Восстань.»[/font][/color]
cult-rune-revive-breath = { $target } делает глубокий вдох, его глаза сияют красным светом.
cult-rune-revive-alive = Вы внезапно пробуждаетесь из пустоты. Вы живы!
cult-rune-revive-twitch = { $target } дёргается.
cult-rune-revive-ghost-role = Неактивный культист { $name }
cult-rune-revive-ghost-role-desc = Займите тело неактивного культиста крови.

cult-rune-barrier-name = Барьер
cult-rune-barrier-desc = при призыве создаёт временную невидимую стену, преграждающую проход. Повторный призыв убирает её.
cult-rune-barrier-destroyed = { $rune } угасает, когда барьер разрушен!

cult-rune-summon-name = Призыв культиста
cult-rune-summon-desc = призывает одного культиста к руне. Требует 2 призывающих.
cult-rune-summon-title = Кого вы призываете к руне?
cult-rune-summon-none = Нет культистов для призыва!
cult-rune-summon-no-target = [color=#960000][italic]Вам нужна цель призыва![/italic][/color]
cult-rune-summon-died = [color=#960000][italic]{ $target } мёртв![/italic][/color]
cult-rune-summon-held = [color=#960000][italic]{ $target } удерживается на месте![/italic][/color]
cult-rune-summon-not-cultist = [color=#960000][italic]{ $target } не следует за Геометром![/italic][/color]
cult-rune-summon-vanish = { $target } внезапно исчезает во вспышке красного света!
cult-rune-summon-vertigo = [color=#960000][italic][bold]Сокрушительное головокружение охватывает вас, когда вас швыряет сквозь воздух![/bold][/italic][/color]
cult-rune-summon-appear = Над { $rune } сгущается туманная фигура и превращается в { $target }!

cult-rune-boil-name = Кипение крови
cult-rune-boil-desc = кипятит кровь неверных, видящих руну, нанося огромный урон. Требует 3 призывающих.
cult-rune-boil-glow = { $rune } вспыхивает ярким, пылающим оранжевым!
cult-rune-boil-veins = [color=#960000][font size=16]Ваша кровь закипает в венах![/font][/color]

cult-rune-manifest-name = Царство духов
cult-rune-manifest-desc = являет духа-слугу Геометра и позволяет вам самому вознестись духом. Призывающий не должен сходить с руны и получает урон за каждого призванного духа.
cult-rune-manifest-stand = [color=#960000][italic]Вы должны стоять на руне![/italic][/color]
cult-rune-manifest-ghost = [color=#960000][italic]Призраки не могут призывать призраков![/italic][/color]
cult-rune-manifest-title = Вы открываете связь с царством духов...
cult-rune-manifest-summon = Призвать призрака культа
cult-rune-manifest-ascend = Вознестись тёмным духом
cult-rune-manifest-not-station = [color=#960000][italic][bold]Завеса здесь недостаточно слаба, чтобы являть духов, — вы должны быть на станции![/bold][/italic][/color]
cult-rune-manifest-too-many = [color=#960000][italic]Вы поддерживаете слишком много призраков, чтобы призвать ещё![/italic][/color]
cult-rune-manifest-notify = Руна Царства духов призвана в { $area }.
cult-rune-manifest-no-spirits = [color=#960000][italic]Рядом с { $rune } нет духов![/italic][/color]
cult-rune-manifest-mist = Над { $rune } сгущается облако красного тумана, и из него выходит... человек.
cult-rune-manifest-flowing = [color=#960000][italic]Ваша кровь начинает течь в { $rune }. Вы должны оставаться на месте и в сознании, чтобы поддерживать облик призванных. Это будет медленно, но верно ранить вас...[/italic][/color]
cult-rune-manifest-servant = [color=#960000][italic][bold]Вы — слуга Геометра. Культ Нар'Си сделал вас полутелесным, и вы должны служить ему любой ценой.[/bold][/italic][/color]
cult-rune-manifest-dissolve = { $target } внезапно рассыпается костями и пеплом.
cult-rune-manifest-fades = [color=#960000][font size=16]Ваша связь с миром угасает. Ваш облик распадается.[/font][/color]
cult-rune-ascend-freeze = { $target } застывает, как статуя, светясь неземным красным.
cult-rune-ascend-self = [color=#960000]Вы видите, что лежит за гранью. Всё открыто. В этой форме ваш голос гремит громче, и вы можете отмечать цели для всего культа.[/color]
cult-rune-ascend-name = Тёмный дух { $name }
cult-rune-ascend-pulled = Призрачное щупальце обвивает { $target } и тянет обратно к руне!
cult-rune-ascend-relax = { $target } медленно расслабляется, свечение вокруг тускнеет.
cult-rune-ascend-reunited = Вы воссоединяетесь со своим телом. { $rune } отпускает вас.
cult-rune-ascend-cant-sustain = [color=#960000][italic]Ваше тело больше не может поддерживать связь![/italic][/color]

cult-rune-apoc-name = Апокалипсис
cult-rune-apoc-desc = предвестник последних дней. Его сила растёт вместе с отчаянием культа — но с риском... побочных эффектов.
cult-rune-apoc-last-site = [color=#960000][font size=16]Осталось лишь одно место ритуала — оно должно быть сохранено для последнего призыва![/font][/color]
cult-rune-apoc-wrong-place = [color=#960000][font size=16]Руна Апокалипсиса уберёт место ритуала, где можно призвать Нар'Си. Её можно начертить только в { $spots }![/font][/color]
cult-rune-apoc-shockwave = Колоссальная ударная волна энергии вырывается из руны, разрушая её!
cult-rune-apoc-announce = [color=#960000][font size=16]Руна Апокалипсиса призвана в { $area }, это место больше не подходит для призыва![/font][/color]

## Черчение

cult-dagger-examine = [color=#960000]Позволяет чертить кровавые руны культа Нар'Си. Удар по постройке культа открепляет или закрепляет её. Рунные балки рушатся с одного удара. Им можно стирать кровавые руны. Удар по другому культисту очищает его от святой воды, превращая её в нечестивую. Удар по некультисту рвёт его плоть.[/color]
cult-scribe-title = Выберите обряд для начертания
cult-scribe-keyword-title = Слова силы
cult-scribe-keyword-prompt = Ключевое слово для новой руны
cult-scribe-already = Вы уже чертите руну.
cult-scribe-unintelligible = { $item } покрыт непонятными формами и знаками.
cult-scribe-cant-now = Сейчас вы не можете начертить руну.
cult-scribe-space = Нельзя чертить руны в космосе!
cult-scribe-rune-exists = [color=#960000]Здесь уже есть руна.[/color]
cult-scribe-veil = Завеса здесь недостаточно слаба.
cult-scribe-summon-station = [color=#960000][italic]Завеса здесь недостаточно слаба для призыва культиста — вы должны быть на станции![/italic][/color]
cult-scribe-apoc-wait = [color=#960000][italic]Завеса ещё недостаточно слаба для этой руны — она станет доступна через { $time }.[/italic][/color]
cult-scribe-no-sites = На этой станции нет мест ритуала для этой руны!
cult-scribe-wrong-site = Завеса здесь недостаточно слаба — эту руну можно начертить только в { $spots }!
cult-scribe-last-site = Эту руну нельзя начертить здесь — место ритуала должно быть сохранено для последнего призыва!
cult-scribe-narsie-sacrifice = Жертвоприношение не завершено. Портал не сможет открыться, если вы попытаетесь!
cult-scribe-narsie-already = [color=#960000][font size=16]«Я уже здесь. Нет нужды призывать меня сейчас.»[/font][/color]
cult-scribe-narsie-confirm-title = Это ПОСЛЕДНИЙ шаг призыва Нар'Си: долгий, мучительный ритуал, и экипаж узнает о вас. Вы готовы к последней битве?
cult-scribe-narsie-confirm-yes = Моя жизнь за Нар'Си!
cult-scribe-narsie-confirm-no = Нет
cult-scribe-narsie-decline = [color=#960000]Вы решаете подготовиться получше, прежде чем чертить руну.[/color]
cult-narsie-scribe-announcement = Зафиксирован призыв древнего божества культистом { $name } в { $area }. Прервите ритуал любой ценой!
cult-narsie-scribe-ghosts = { $name } начал рисовать руну Нар'Си!
cult-scribe-start-others-blood = { $user } разрезает руку и начинает писать собственной кровью!
cult-scribe-start-others = { $user } начинает набрасывать странный узор!
cult-scribe-start-self-blood = Вы вскрываете руку и начинаете чертить знак Геометра.
cult-scribe-start-self = Вы начинаете чертить знак Геометра.
cult-scribe-done-others-blood = { $user } создаёт странный круг собственной кровью.
cult-scribe-done-others = { $user } создаёт странный круг.
cult-scribe-done-self = [color=#960000]Вы заканчиваете чертить тайные знаки Геометра.[/color]
cult-scribe-made = [color=#960000]Руна «{ $name }» { $desc }[/color]
cult-erase-confirm-title = Стирание руны «{ $name }» может пойти вразрез с вашими целями. Начать стирать?
cult-erase-confirm-yes = Продолжить
cult-erase-confirm-no = Отмена
cult-erase-done = Вы аккуратно стираете руну «{ $name }».

## Магия крови

cult-magic-prepare-title = Заклинание крови для подготовки
cult-magic-remove-title = Заклинание для удаления
cult-magic-remove-option = (УДАЛИТЬ ЗАКЛИНАНИЕ)
cult-magic-limit-rune = [color=#960000][italic]Нельзя хранить больше { $limit } заклинаний. [bold]Выберите заклинание для удаления.[/bold][/italic][/color]
cult-magic-limit-runeless = [color=#960000][bold][italic]Без руны усиления нельзя хранить больше { $runeless } заклинаний! Выберите заклинание для удаления.[/italic][/bold][/color]
cult-magic-carve-start = Вы начинаете вырезать неестественные символы на своей плоти!
cult-magic-already-channeling = [color=#960000][italic]Вы уже вызываете магию крови![/italic][/color]
cult-magic-prepared = Ваши раны пылают силой, вы подготовили заклинание «{ $spell }»!
cult-magic-health-cost = Наносит { $cost } урона руке за применение.
cult-magic-uses = Осталось применений: { $uses }
cult-spell-snuff = Вы гасите заклинание, сохраняя его на потом.
cult-spell-no-hand = У вас нет свободной руки для магии крови!
cult-spell-invoke = Ваши раны вспыхивают, когда вы призываете «{ $spell }».
cult-spell-no-effect = Заклинание не подействовало!
cult-spell-stun-others = { $user } поднимает руку, которая взрывается вспышкой красного света!
cult-spell-stun-self = [color=#960000][italic]Вы пытаетесь оглушить { $target } заклинанием![/italic][/color]
cult-spell-stun-success = [color=#960000][italic]В ярко-красной вспышке { $target } падает на землю![/italic][/color]
cult-spell-stun-heretic-user = Древняя сила вмешивается, когда вы касаетесь { $target }, поглощая большую часть эффекта!
cult-spell-stun-heretic-target = Когда { $user } касается вас мерзкой магией, Мансус поглощает большую часть эффекта!
cult-spell-stun-absorbed = поглощено!
cult-spell-teleport-cultists-only = Этим заклинанием можно телепортировать только культистов!
cult-spell-teleport-invalid = Нужно выбрать подходящую руну!
cult-spell-teleport-blocked = Целевая руна заблокирована. Вы не можете туда телепортироваться.
cult-spell-teleport-crack = Из руки { $user } сыплется пыль, и он исчезает с резким хлопком!
cult-spell-teleport-self = [color=#960000][italic]Вы произносите слова талисмана и оказываетесь в другом месте![/italic][/color]
cult-spell-emp-others = Рука { $user } вспыхивает ярко-синим!
cult-spell-emp-self = [color=#960000][italic]Вы произносите проклятые слова, испуская из руки ЭМИ.[/italic][/color]
cult-spell-shackles-arms = [color=#960000][italic]У жертвы недостаточно рук для оков![/italic][/color]
cult-spell-shackles-start-others = { $user } начинает сковывать { $target } тёмной магией!
cult-spell-shackles-start-target = { $user } начинает формировать вокруг ваших запястий оковы из тёмной магии!
cult-spell-shackles-done = Вы сковываете { $target }.
cult-spell-shackles-bound = { $target } уже скован.
cult-spell-shackles-fail = Вам не удаётся сковать { $target }.
cult-spell-construction-channeling = [color=#960000][italic]Вы уже вызываете искажённое строительство![/italic][/color]
cult-spell-construction-iron = Нужно { $count } стали, чтобы создать оболочку конструкта! Положите стопки на одно место или держите их в руках.
cult-spell-construction-shell = Тёмное облако исходит из вашей руки и закручивается вокруг стали, превращая её в оболочку конструкта!
cult-spell-construction-runed = Тёмное облако исходит из вашей руки и закручивается вокруг пластали, превращая её в рунный металл!
cult-spell-construction-cloud = Тёмное облако исходит из руки { $user } и закручивается вокруг { $target }!
cult-spell-construction-borg-done = Тёмное облако отступает от того, что было { $target }, открывая { $type }!
cult-spell-construction-borg-shell = Тёмное облако исходит из вашей руки и закручивается вокруг { $target }, превращая его в оболочку конструкта!
cult-spell-construction-airlock = Чёрные ленты вдруг вырываются из руки { $user } и цепляются за шлюз, искажая и оскверняя его!
cult-spell-construction-corrupt = Вы оскверняете { $target }!
cult-spell-construction-invalid = Заклинание не подействует на { $target }!
cult-spell-equipment = На { $target } внезапно появляется потусторонняя броня!
cult-spell-dagger-others = Рука { $user } на мгновение вспыхивает красным.
cult-spell-dagger-self = [color=#960000][italic]Ваша мольба о помощи услышана, и в вашей руке начинает мерцать и обретать форму свет![/italic][/color]
cult-spell-dagger-hand = В вашей руке появляется { $item }!
cult-spell-dagger-feet = { $item } появляется у ног { $user }!
cult-spell-horror-cursed = [color=#960000][bold]{ $target } проклят живыми кошмарами![/bold][/color]
cult-spell-horror-exhausted = [color=#960000]Вы исчерпали силу заклинания![/color]
cult-spell-veil-others = Из руки { $user } сыплется тонкая серая пыль!
cult-spell-veil-self = [color=#960000][italic]Вы призываете скрывающее заклинание, пряча ближайшие руны.[/italic][/color]
cult-spell-reveal-others = Из руки { $user } вырывается вспышка света!
cult-spell-reveal-self = [color=#960000][italic]Вы призываете контрзаклинание, раскрывая ближайшие руны.[/italic][/color]
cult-spell-reveal-name = Раскрыть руны
cult-spell-conceal-name = Скрыть руны
cult-conceal-fades = { $target } растворяется.
cult-conceal-appears = { $target } внезапно появляется!

## Кровавые обряды

cult-rites-title = Продвинутый кровавый обряд
cult-rites-halberd = Кровавая алебарда ({ $cost })
cult-rites-barrage = Кровавый залп ({ $cost })
cult-rites-beam = Кровавый луч ({ $cost })
cult-rites-decide-against = [color=#960000][italic]Вы решаете не проводить великий кровавый обряд.[/italic][/color]
cult-rites-need = [color=#960000][italic]Для этого обряда нужно { $cost } зарядов.[/italic][/color]
cult-rites-need-hand = [color=#960000][italic]Для этого обряда нужна свободная рука![/italic][/color]
cult-rites-halberd-hand = [color=#960000][italic]В вашей руке появляется { $item }![/italic][/color]
cult-rites-halberd-feet = { $item } появляется у ног { $user }!
cult-rites-barrage-hand = [color=#960000][bold]Ваши руки сияют силой![/bold][/color]
cult-rites-beam-hand = [color=#960000][font size=16][bold]Ваши руки сияют НЕОДОЛИМОЙ СИЛОЙ!!![/bold][/font][/color]
cult-rites-no-blood = нет крови!
cult-rites-dead = мёртв!
cult-rites-out = кровь кончилась!
cult-rites-no-healing = [color=#960000]Этому культисту не нужно лечение![/color]
cult-rites-last-blood = Вы тратите остатки кровавых обрядов, чтобы восстановить сколько можете крови!
cult-rites-blood-restored = Ваши кровавые обряды восстановили кровь { $target } до безопасного уровня!
cult-rites-blood-restored-self = Ваши кровавые обряды восстановили вашу кровь до безопасного уровня!
cult-rites-self-inefficient = [color=#960000][bold]Лечение кровью гораздо менее эффективно на себе![/bold][/color]
cult-rites-healed-full = { $target } полностью исцелён магией крови!
cult-rites-healed-partial = { $target } частично исцелён магией крови!
cult-rites-construct-full = { $target } полностью исцелён магией крови { $user }!
cult-rites-construct-partial = { $target } частично исцелён магией крови { $user }!
cult-rites-tainted = Его кровь осквернена ещё более сильной магией крови, она бесполезна для нас!
cult-rites-too-little = Ему и так не хватает крови — дальше высасывать нельзя!
cult-rites-drain-others = { $user } высасывает кровь из { $target }!
cult-rites-drain-self = [color=#960000][italic]Ваш кровавый обряд получает 50 зарядов, высосав кровь { $target }.[/italic][/color]
cult-rites-floor = [color=#960000][italic]Ваш кровавый обряд получил { $count } зарядов из крови вокруг вас![/italic][/color]
cult-purge-holywater = [color=#960000]Вы очищаете { $target } от скверны с помощью { $item }.[/color]

## Предметы

cult-blade-pickup-warning = [color=#960000][font size=16]«Я бы не советовал.»[/font][/color]
cult-blade-shove-others = Могучая сила отталкивает { $user }!
cult-blade-shove-self = [color=#960000][font size=16]«Не играй с острыми предметами. Глаз себе выколешь.»[/font][/color]
cult-parry = { $user } парирует атаку с помощью { $item }!
cult-bola-life = Бола будто оживает!
cult-whetstone-used = { $stone } уже использован.
cult-whetstone-not-sharp = { $target } нельзя заточить.
cult-whetstone-already = { $target } уже заточен.
cult-whetstone-too-sharp = { $target } слишком остр, чтобы точить дальше!
cult-whetstone-sharpen = Вы затачиваете { $target } о { $stone }. Оружие стало острее.
cult-whetstone-prefix = { $prefix } { $name }
cult-curse-shoved = Могучая сила отталкивает вас от { $orb }!
cult-curse-solid = Вы пытаетесь разбить сферу, но она остаётся твёрдой как камень!
cult-curse-exhausted = [bold]Похоже, культ исчерпал способность проклинать эвакуационный шаттл. Неразумно создавать новые сферы или продолжать пытаться разбить эту.[/bold]
cult-curse-narsie = Нар'Си уже на этом плане, конец всего не отсрочить.
cult-curse-shatter = Вы разбиваете сферу! Тёмная сущность закручивается в воздухе и исчезает.
cult-curse-left-none = [bold]Вы чувствуете, что эвакуационный шаттл больше нельзя проклясть. Неразумно создавать новые сферы.[/bold]
cult-curse-left-one = [bold]Вы чувствуете, что эвакуационный шаттл можно проклясть лишь ещё один раз.[/bold]
cult-curse-left-many = [bold]Вы чувствуете, что эвакуационный шаттл можно проклясть ещё { $count } раза.[/bold]
cult-curse-sender = Обнаружена неисправность в системе
cult-curse-announce-delay = Эвакуационный шаттл задерживается на три минуты.
cult-curse-announce-fallback = Что-то пошло ужасающе неправильно...
cult-curse-announce-1 = Заправщик только что перерезал себе горло, умоляя о смерти.
cult-curse-announce-2 = Сканирование топливного бака шаттла выявило загрязнение смесью человеческих внутренностей и зубов.
cult-curse-announce-3 = Инцидент с участием обезумевшего работника шаттла, атаковавшего коллег лазерным резаком, был разрешён местной охраной.
cult-curse-announce-4 = Инженер шаттла начала кричать «СМЕРТЬ НЕ КОНЕЦ» и рвать провода, пока дуговой разряд не испепелил её.
cult-curse-announce-5 = Инженера шаттла заметили в кабине, перед смертью лихорадочно раскладывающего свои внутренности на полу в виде руны.
cult-curse-announce-6 = Инспектор шаттла начал безумно смеяться в радиоэфир, а затем бросился в турбину двигателя.
cult-curse-announce-7 = Труп неустановленного работника шаттла был найден в главном отсеке, изувеченный до неузнаваемости, с как минимум пятью разными источниками крови на месте.
cult-curse-announce-8 = Диспетчер шаттла был найден мёртвым с кровавыми символами, вырезанными на коже.
cult-curse-announce-9 = Уборщик шаттла был замечен моющим окна собственной кровью.
cult-curse-announce-10 = Навигационная программа шаттла была заменена файлом с двумя словами: ОНО ИДЁТ.
cult-curse-announce-11 = Транспондер шаттла передаёт закодированное сообщение «БОЙСЯ ДРЕВНЕЙ КРОВИ» вместо стандартного идентификационного сигнала.
cult-shifter-uses = [color=#960000]Осталось применений: { $uses }.[/color]
cult-shifter-drained = [color=#960000]Похоже, он истощён.[/color]
cult-shifter-dull = { $item } тускл и неподвижен в ваших руках.
cult-shifter-flicker = { $item } выскальзывает из ваших рук — ваша связь с этим измерением слишком сильна!
cult-shifter-failed = телепортация не удалась!
cult-halberd-far = [color=#960000]Алебарда слишком далеко![/color]
cult-halberd-pulled = Невидимая сила вырывает кровавую алебарду из рук { $target }!
cult-halberd-catch = { $target } ловит { $item } на лету!
cult-halberd-bounce = { $item } отскакивает от { $target }, будто отражённый невидимой силой!
cult-halberd-shatter = { $item } разбивается и тает обратно в кровь!
cult-beam-exhausted = [color=#960000][italic]Вы исчерпали силу этого заклинания![/italic][/color]
cult-armor-burn = Броня обжигает вашу кожу нечестивым жаром!
cult-armor-wound = Руны брони рвут вашу плоть!

## Постройки

cult-structure-secured = { $structure } закреплён на полу.
cult-structure-unsecured = { $structure } не закреплён.
cult-structure-stability = [color=#960000]Стабильность: [bold]{ $percent }%[/bold].[/color]
cult-structure-cooldown = [color=#960000][italic]Магия в { $structure } слишком слаба, она будет готова через [bold]{ $time }[/bold].[/italic][/color]
cult-structure-cant-touch = Вы совершенно уверены, что знаете, для чего это нужно, и не можете к этому прикоснуться.
cult-structure-anchor-first = [color=#960000][italic]Сначала закрепите { $structure } на полу.[/italic][/color]
cult-structure-produces = [color=#960000][italic]{ $structure } производит { $item }.[/italic][/color]
cult-structure-secure = Вы закрепляете { $structure } на полу.
cult-structure-unsecure = Вы открепляете { $structure } от пола.
cult-altar-tip = Позволяет создавать древние точила, оболочки конструктов и фляги нечестивой воды.
cult-altar-success = [color=#960000][italic]Вы преклоняете колени перед { $structure }, и ваша вера вознаграждается: { $item }![/italic][/color]
cult-altar-break = Алтарь разбивается, оставляя лишь вой проклятых!
cult-archives-tip = Позволяет создавать повязки фанатика, сферы проклятия шаттла и сдвигатели завесы.
cult-archives-success = [color=#960000][italic]Вы призываете { $item } из { $structure }![/italic][/color]
cult-archives-break = Книги и тома архивов сгорают в пепел, когда стол разбивается!
cult-forge-tip = Позволяет создавать закалённую нарсийскую броню и древние длинные мечи.
cult-forge-success = [color=#960000][italic]Вы работаете у { $structure }, и тёмное знание направляет ваши руки, создавая { $item }![/italic][/color]
cult-forge-break = Кузня разлетается на осколки с воющим криком!
cult-pylon-break = Кроваво-красный кристалл падает на пол и разбивается!
cult-option-whetstone = Древнее точило
cult-option-whetstone-desc = Точило, увеличивающее урон мечей и кинжалов. Одно применение.
cult-option-shell = Оболочка конструкта
cult-option-shell-desc = Оболочка, которая с тенью из камня душ породит конструкта.
cult-option-flask = Фляга нечестивой воды
cult-option-flask-desc = Фляга, глоток из которой лечит все виды урона и потерю крови.
cult-option-blindfold = Повязка фанатика
cult-option-blindfold-desc = Повязка, не ослепляющая культистов: показывает здоровье, даёт ночное зрение и защищает от вспышек.
cult-option-curse = Сфера проклятия шаттла
cult-option-curse-desc = Хрупкая сфера, которую можно разбить, чтобы задержать вызванный шаттл. Всего 3 раза.
cult-option-veil = Сдвигатель завесы
cult-option-veil-desc = Палочка, переносящая владельца — и того, кого он тащит, — вперёд. 4 применения.
cult-option-armor = Закалённая нарсийская броня
cult-option-armor-desc = Прочная броня, выдерживающая космос.
cult-option-blade = Древний длинный меч
cult-option-blade-desc = Мощный клинок, рассекающий самую прочную броню.
cult-runed-metal-title = Рунный металл
cult-runed-metal-forbidden = Лишь обладатель запретного знания может надеяться обработать этот металл...
cult-runed-metal-not-enough = Нужно { $cost } листов рунного металла!
cult-runed-metal-solid-ground = Нужна твёрдая поверхность!
cult-runed-metal-occupied = Здесь уже что-то стоит!
cult-recipe-pylon = Пилон ({ $cost })
cult-recipe-pylon-desc = Лечит и восстанавливает кровь ближайших культистов и конструктов, превращает пол в гравированный.
cult-recipe-altar = Алтарь ({ $cost })
cult-recipe-altar-desc = Создаёт древние точила, оболочки конструктов и фляги нечестивой воды.
cult-recipe-archives = Архивы ({ $cost })
cult-recipe-archives-desc = Создаёт повязки фанатика, сферы проклятия шаттла и сдвигатели завесы. Светится.
cult-recipe-forge = Демоническая кузня ({ $cost })
cult-recipe-forge-desc = Создаёт закалённую нарсийскую броню и древние длинные мечи. Светится.
cult-recipe-door = Рунная дверь ({ $cost })
cult-recipe-door-desc = Слабая дверь, оглушающая некультистов при касании.
cult-recipe-girder = Рунная балка ({ $cost })
cult-recipe-girder-desc = Слабая балка, мгновенно разрушаемая ритуальными кинжалами.
cult-girder-strike-others = { $user } бьёт { $target } с помощью { $item }!
cult-girder-strike-self = Вы разрушаете { $target }.
cult-girder-need-sheet = Нужен хотя бы один лист рунного металла, чтобы построить рунную стену!
cult-girder-plating-others = { $user } начинает обшивать { $target } рунным металлом...
cult-girder-plating-self = Вы начинаете строить рунную стену...
cult-girder-plated-others = { $user } обшивает { $target } рунным металлом.
cult-girder-plated-self = Вы строите рунную стену.

## Конструкты и камень душ

cult-construct-choice-title = Выберите конструкта
cult-construct-name-juggernaut = Джаггернаут
cult-construct-name-wraith = Призрак
cult-construct-name-artificer = Ремесленник
cult-construct-name-harvester = Жнец
cult-construct-name-proteon = Протеон
cult-construct-name-shade = Тень
cult-construct-tip-juggernaut = Медленный, но очень прочный, создаёт временные стены.
cult-construct-tip-wraith = Высокий урон, проходит сквозь стены, но хрупок.
cult-construct-tip-artificer = Создаёт новые оболочки и камни душ, а также укрепления.
cult-construct-playstyle-generic = [bold]Вы — конструкт культа.[/bold]
cult-construct-playstyle-juggernaut = [bold]Вы — Джаггернаут. Хоть вы и медленны, ваша оболочка выдерживает сильные удары, создаёт щитовые стены, рвёт врагов и стены и даже отражает энергетическое оружие.[/bold]
cult-construct-playstyle-wraith = [bold]Вы — Призрак. Хоть вы и хрупки, вы быстры, смертоносны и можете проходить сквозь стены. Ваши атаки сокращают перезарядку фазового сдвига, а смертельные — ещё сильнее.[/bold]
cult-construct-playstyle-artificer = [bold]Вы — Ремесленник. Вы невероятно слабы и хрупки, но можете строить укрепления, использовать магическую ракету и чинить союзных конструктов, теней и себя (нажав на них). Кроме того — и это важнее всего — вы можете создавать новых конструктов, производя камни душ для захвата душ и оболочки для этих камней.[/bold]
cult-construct-playstyle-harvester = [bold]Вы — Жнец. Вы не можете напрямую убивать людей, но ваши атаки отсекают конечности: приведите тех, кто цепляется за этот мир иллюзий, к Геометру, чтобы они узнали Истину.[/bold]
cult-construct-playstyle-proteon = [bold]Вы — Протеон. Слабый конструкт, рыщущий в поисках добычи.[/bold]
cult-construct-dented = Он слегка помят.
cult-construct-severely-dented = [bold]Он сильно помят![/bold]
cult-construct-death = { $target } рассыпается грудой обломков.
cult-shade-death = { $target } исчезает с воплем.
cult-construct-repair-start = { $user } начинает чинить вмятины { $target }.
cult-construct-repair-done = Вмятины { $target } починены.
cult-construct-repair-full = { $target } не нуждается в починке.
cult-construct-imbue-failed = [bold]Наполнение оболочки не удалось![/bold] Душа уже покинула смертную оболочку. Вы пытаетесь вернуть её...
cult-shade-name = Тень { $name }
cult-soulstone-wracked = [bold]Ваше тело пронзает невыносимая боль![/bold]
cult-soulstone-dread-pickup = Ощущение подавляющего ужаса охватывает вас, когда вы поднимаете { $stone }. Будет мудро поскорее от него избавиться.
cult-soulstone-examine-1 = [color=#960000]Камень душ, используемый для захвата души мёртвых людей или освобождённых теней.[/color]
cult-soulstone-examine-2 = [color=#960000]Пойманную душу можно поместить в оболочку конструкта или выпустить из камня тенью.[/color]
cult-soulstone-spent = [color=#960000]Этот осколок истощён — теперь это просто жуткий камень.[/color]
cult-soulstone-no-power = В { $stone } не осталось силы.
cult-soulstone-brethren = [color=#960000][font size=16]«Ну же, не захватывай душу своего собрата.»[/font][/color]
cult-soulstone-holy-burn = [bold]Святая магия { $stone } обжигает вашу руку![/bold]
cult-soulstone-sacrifice-target = [color=#960000][bold]«Эта душа — моя.»[/bold] [font size=16]ПРИНЕСИТЕ ЕГО В ЖЕРТВУ![/font][/color]
cult-soulstone-capture-failed = [bold]Захват не удался![/bold]
cult-soulstone-kill-first = Сначала убейте или покалечьте жертву!
cult-soulstone-fled = Душа уже покинула смертную оболочку. Вы пытаетесь вернуть её...
cult-soulstone-capture-success = [bold]Захват успешен![/bold] Душа { $target } вырвана из тела и заключена в { $stone }.
cult-soulstone-captured-cult = [bold]Ваша душа захвачена! Теперь вы связаны волей культа. Помогите ему достичь целей любой ценой.[/bold]
cult-soulstone-captured-master = [bold]Ваша душа захвачена! Теперь вы связаны волей { $master }. Помогите ему достичь целей любой ценой.[/bold]
cult-soulstone-full = { $stone } полон! Освободите душу, чтобы освободить место.
cult-soulstone-shade-captured = Ваша душа захвачена { $stone }. Его тайные энергии восстанавливают вашу эфирную форму.
cult-soulstone-shade-success = [bold]Захват успешен![/bold] Душа { $target } захвачена и помещена в { $stone }.
cult-soulstone-released-cult = [bold]Вы освобождены из темницы, но всё ещё связаны волей культа. Помогите ему достичь целей любой ценой.[/bold]
cult-soulstone-released-master = [bold]Вы освобождены из темницы, но всё ещё связаны волей { $master }. Помогите ему достичь целей любой ценой.[/bold]
cult-shell-examine = [color=#960000]Оболочка конструкта для связанных душ из камня душ. Поместив камень с душой, можно создать Ремесленника, Призрака или Джаггернаута.[/color]
cult-shell-dread = Ощущение подавляющего ужаса охватывает вас, когда вы пытаетесь поместить { $stone } в оболочку. Будет мудро поскорее от него избавиться.
cult-shell-empty = [bold]Создание не удалось![/bold] { $stone } пуст! Убейте кого-нибудь!

## Нар'Си

cult-narsie-risen = [color=#960000][font size=24][bold]NAR'SIE HAS RISEN[/bold][/font][/color]
cult-narsie-risen-ghosts = Нар'Си восстала! Обратитесь к Геометру, чтобы получить новую оболочку для своей души.
cult-narsie-fall = [color=#960000][font size=24][bold]{ $line }[/bold][/font][/color]
cult-narsie-lost-interest = [color=#960000]NAR'SIE HAS LOST INTEREST IN YOU.[/color]
cult-narsie-hungers = [color=#960000]NAR'SIE HUNGERS FOR YOUR SOUL.[/color]
cult-narsie-chosen = [color=#960000]NAR'SIE HAS CHOSEN YOU TO LEAD HER TO HER NEXT MEAL.[/color]
cult-narsie-mesmerize = [color=#960000]Вы чувствуете, как сознательная мысль мгновенно рассыпается, когда вы смотрите на { $narsie }...[/color]
cult-narsie-end-1 = Внимание, это приоритетное оповещение. Сектор Эпсилон Эридани был подвергнут вторжению древней богоподобной враждебной сущности, у нас есть подтвержденная информация о массовых порабощениях по всему сектору. Положению присвоен сценарий «ПОЛНОЕ ИСТРЕБЛЕНИЕ». Приказ на принятие мер получен и авторизован. Ожидаемое время готовности: 60 секунд.
cult-narsie-end-1-fail = Доклад статуса? Мы обнаружили аномалию, но она почти сразу же пропала.
cult-narsie-end-2 = Моделирование беспричинного изменения пространства завершено. Начинаем развертывание.
cult-narsie-end-2-fail = Моделирование прервано, датчики сообщают о нормализации пространственного явления. Хорошая работа, экипаж.
cult-narsie-end-3 = Тревога! Обнаружен запуск орбитального эммитера. Протоколы безопасности станции обнулены, возобновление нево@#$%^-...
cult-narsie-end-3-fail = Явление нормализовано! Отменить развертывание!
cult-narsie-security-sender = Оповещение безопасности

## Цели и итоги

cult-objective-sacrifice = Принесите в жертву { $target } с помощью руны Подношения, когда цель на ней, а вокруг три аколита.
cult-objective-sacrifice-free = Свободная цель
cult-objective-summon = Призовите Нар'Си, призвав руну «Нар'Си». Призыв возможен только в { $spots } — там, где завеса слаба для ритуала.
cult-objective-target-format = { $name }, { $job }
cult-objective-unknown = неизвестный
cult-objective-anywhere = любом месте
cult-objective-and = и
cult-sacrifice-target-changed = [color=#960000][bold]Цель жертвоприношения ускользнула от нас! Новая цель — { $target }.[/bold][/color]
cult-roundend-narsie-killed = [color=red][font size=16]Нар'Си был убит! Культ больше не угрожает вселенной![/font][/color]
cult-roundend-win = [color=green][font size=16]Культ смог призвать своего бога! Нар'Си погасил ещё один факел в этой пустоте![/font][/color]
cult-roundend-loss = [color=red][font size=16]Экипаж смог остановить культ! Тёмные речи и ересь не идут ни в какое сравнение с лучшими из Нанотрейзен![/font][/color]
cult-roundend-sacrifice-done = Жертвоприношение ({ $target }): [color=green]успех[/color]
cult-roundend-sacrifice-failed = Жертвоприношение ({ $target }): [color=red]провал[/color]
cult-roundend-summon-done = Призыв Нар'Си: [color=green]успех[/color]
cult-roundend-summon-failed = Призыв Нар'Си: [color=red]провал[/color]
cult-roundend-size = Наибольший размер культа: { $count }.
cult-roundend-cultists = Культистами были:
cult-roundend-cultist-entry = - [color=White]{ $name }[/color] ([color=gray]{ $user }[/color])
cult-roundend-master-entry = - [color=#960000]Мастер[/color] [color=White]{ $name }[/color] ([color=gray]{ $user }[/color])

## Совместимость со старым правилом и целями (Content.Server/GameTicking/Rules/CultRuleSystem.cs)

cult-commune-can-summon = [ОБЩИНА] Все жертвы принесены. Начинайте ритуал вызова!
cult-commune-sacrifice-wrong-target = [ОБЩИНА] Эта жертва не была отмечена Нар'Си. Финальный ритуал не приблизился.
cult-commune-sacrifices-count = [ОБЩИНА] Жертвоприношений: { $done }/{ $needed }.
cult-commune-sacrifices-required-first = [ОБЩИНА] Сначала принесите все жертвы!
cult-narsie-unknown-location = неизвестный маяк
cult-objective-sacrifice-targets-desc = Принесите отмеченную душу Нар'Си на руне Подношения.
cult-objective-sacrifice-targets-fallback-job = неизвестная должность
cult-objective-sacrifice-targets-fallback-name = Жертвы для Нар'Си
cult-objective-sacrifice-targets-name = Принести в жертву { $target1 } ({ $job1 }) и { $target2 } ({ $job2 })
cult-objective-sacrifice-targets-none-name = Жертвы не требуются
cult-objective-sacrifice-targets-single-name = Принести в жертву { $target1 } ({ $job1 })
cult-objective-summon-narsie-desc = Призовите Нар'Си на руне Нар'Си.
cult-objective-summon-narsie-name-beacons = Воплотить Нар'Си у одного из маяков: { $beacons }
cult-roundend-lose = Культ разгромлен. Принесено жертв: { $sacrifices }/{ $required }.

## Старые действия (Resources/Prototypes/Actions/cult.yml)

cult-action-commune-desc = Передай сообщение всем культистам. Слышно в радиусе.
cult-action-stun-desc = В руке появляется заряд тёмной энергии. Бьёт цель с ног на 3 сек, замолкает на 6 сек. Стоит 10 HP.
cult-action-shackles-desc = Сковывает оглушённую цель на 45 сек. Стоит 4 заряда.
cult-action-teleport-desc = Телепортируется к ближайшей руне телепортации. Стоит 7 HP.
cult-action-emp-desc = Крупный ЭМИ-импульс. Не задевает культистов. Стоит 4 заряда.
cult-action-twisted-construction-desc = Превращает металл в рунный, а шлюз — в рунную дверь. Стоит 12 HP.
cult-action-summon-dagger-desc = Призывает новый ритуальный кинжал в руку. Стоит 1 заряд.
cult-action-summon-equipment-desc = Призывает полный боевой комплект культиста.
cult-action-conceal-presence-desc = Скрывает/показывает руны и структуры в радиусе 4 тайлов. Стоит 10 зарядов.
cult-action-construct-juggernaut-shell-desc = Создаёт пустую оболочку джаггернаута у ваших ног.
cult-action-construct-wraith-shell-desc = Создаёт пустую оболочку фантома у ваших ног.
cult-action-construct-artificer-shell-desc = Создаёт пустую оболочку созидателя у ваших ног.
cult-action-construct-soulstone-desc = Создаёт новый камень души для оживления оболочки.
cult-action-construct-pylon-desc = Воздвигает пилон Нар'Си рядом с вами.
cult-action-construct-floor-desc = Превращает соседний тайл в рунный пол Нар'Си.
cult-action-construct-heal-desc = Восстанавливает прочность и плоть союзника культа рядом с вами.
cult-action-construct-wall-desc = Воздвигает короткоживущий силовой барьер Нар'Си.
cult-action-blood-magic-desc = Открывает окно выбора заклинания. Подготовка занимает 10 секунд и стоит 20 HP. Лимит: 1 заклинание (4 с руной усиления). У каждого подготовленного заклинания свои использования.
cult-action-dark-spirit-return-desc = Покинуть духовную форму и вернуться в тело. Получите 100 ед. стамина-урона.
cult-action-recall-blood-spear-desc = Возвращает призванное копьё крови в вашу руку.
cult-action-blood-rites-desc = Материализует тёмную энергию обряда. Повторное нажатие убирает её. Внутри доступны: сбор, лечение, перезарядка, сфера и копьё.
ent-ActionCultCommune = Общение
    .desc = { cult-action-commune-desc }
ent-ActionCultStun = Оглушение
    .desc = { cult-action-stun-desc }
ent-ActionCultShackles = Теневые оковы
    .desc = { cult-action-shackles-desc }
ent-ActionCultTeleport = Телепортация
    .desc = { cult-action-teleport-desc }
ent-ActionCultEmp = ЭМИ
    .desc = { cult-action-emp-desc }
ent-ActionCultTwistedConstruction = Искажённое строительство
    .desc = { cult-action-twisted-construction-desc }
ent-ActionCultSummonDagger = Призыв кинжала
    .desc = { cult-action-summon-dagger-desc }
ent-ActionCultSummonEquipment = Призыв снаряжения
    .desc = { cult-action-summon-equipment-desc }
ent-ActionCultConcealPresence = Маскировка присутствия
    .desc = { cult-action-conceal-presence-desc }
ent-ActionCultBloodRites = Обряды крови
    .desc = { cult-action-blood-rites-desc }
ent-ActionCultBloodMagic = Кровавая магия
    .desc = { cult-action-blood-magic-desc }
ent-ActionCultDarkSpiritReturn = Вернуться в тело
    .desc = { cult-action-dark-spirit-return-desc }
ent-ActionCultDarkSpiritCommune = Общение
    .desc = { cult-action-commune-desc }
ent-ActionCultRecallBloodSpear = Возврат копья крови
    .desc = { cult-action-recall-blood-spear-desc }
ent-ActionCultConstructCreateJuggernautShell = Слепить оболочку джаггернаута
    .desc = { cult-action-construct-juggernaut-shell-desc }
ent-ActionCultConstructCreateWraithShell = Слепить оболочку фантома
    .desc = { cult-action-construct-wraith-shell-desc }
ent-ActionCultConstructCreateArtificerShell = Слепить оболочку созидателя
    .desc = { cult-action-construct-artificer-shell-desc }
ent-ActionCultConstructCreateSoulStone = Сформировать камень души
    .desc = { cult-action-construct-soulstone-desc }
ent-ActionCultConstructCreatePylon = Воздвигнуть пилон
    .desc = { cult-action-construct-pylon-desc }
ent-ActionCultConstructCreateFloor = Запятнать пол
    .desc = { cult-action-construct-floor-desc }
ent-ActionCultConstructHealAlly = Починить союзника
    .desc = { cult-action-construct-heal-desc }
ent-ActionCultConstructCreateWall = Воздвигнуть барьер
    .desc = { cult-action-construct-wall-desc }

## Дополнения BandaStation

cult-master-announce = [color=#960000][font size=16]{ $name } ваш Мастер культа! Выполняйте приказы Мастера насколько это в ваших силах![/font][/color]
cult-equip-help = Это поможет вам организовать культ на станции. Используйте их с пользой, и запомните - вы не одни.
cult-mindshield-resist = Вы чувствуете, что что-то вмешивается в ваше ментальное состояние, но вы сопротивляетесь этому!
cult-shade-in-stone = Вы не можете вызывать руны, находясь в камне душ!
cult-shade-too-weak = Вы не накопили достаточно сил для вызова рун. Вам нужно некоторое время с момента выхода из камня душ!
cult-curse-omfg-sender = Центральное Командование: Транспортный Департамент Нанотрейзен
cult-curse-omfg-1 = ОХУЕТЬ ЧТО ТОЛЬКО ЧТО ПРОИЗОШЛО В НАШЕМ ДОКЕ ШАТТЛОВ
cult-curse-omfg-2 = ЁБ ТВОЮ МАТЬ НАШ ШАТТЛ-ДОК - ЭТО ЗОНА БОЕВЫХ ДЕЙСТВИЙ
cult-curse-omfg-3 = ГОСПОДИ ИИСУСЕ ХРИСТЕ ОСТАВЬТЕ НАШИХ РАБОТНИКОВ ШАТТЛОВ В ПОКОЕ
cult-curse-omfg-4 = ЧТО ЗА ХУЙНЯ
cult-curse-omfg-5 = ОТСТАНЬТЕ ОТ РАБОТНИКОВ ШАТТЛОВ ЁБАНЫЕ ТВАРИ
cult-curse-omfg-6 = СТОЙТЕ СТОП СТОПСТОПСТОПСТОП
cult-curse-omfg-7 = ПАМАГИТЕ
cult-curse-omfg-fallback = ОТЪЕБИТЕСЬ ОТ НАС!
cult-illusion-death = { $target } растворяется в воздухе! Это была подделка!
cult-mirror-betrayed = Вас предаёт «вы сами»!
cult-mirror-shatter = Сила удара { $item } разбивает зеркальный щит!
cult-mirror-restored = [color=#960000][italic]Иллюзии щита восстановили полную силу![/italic][/color]
cult-harvester-knockdown = { $user } сбивает { $target } с ног!
cult-harvester-bring = [color=#960000][font size=16]«Приведи { $target } ко мне.»[/font][/color]
cult-seek-master-none = [color=#960000][italic]Вам некого искать![/italic][/color]
cult-seek-master-on = [color=#960000][italic]Теперь вы отслеживаете своего хозяина.[/italic][/color]
cult-seek-master-off = [color=#960000][italic]Вы больше не отслеживаете своего хозяина.[/italic][/color]
cult-seek-prey-narsie = [color=#960000][italic]Теперь вы отслеживаете Нар'Си — возвращайтесь собирать урожай![/italic][/color]
cult-seek-prey-done = [color=#960000][italic]Нар'Си завершила свою жатву![/italic][/color]
cult-seek-prey-on = [color=#960000][italic]Теперь вы отслеживаете свою добычу, { $target } — пожните { $target }![/italic][/color]
cult-blessed-block = Святые энергии преграждают вам путь!
cult-blessing-name = святое благословение
cult-portal-storm-announcement = Обнаружена внушительная блюспейс аномалия на встречном с станцией курсе. Приготовьтесь к удару.
