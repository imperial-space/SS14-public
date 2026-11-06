# Морф (Paradise, miniantags/morph)

morph-name = морф

ghost-role-information-morph-name = Морф
ghost-role-information-morph-description = Вы жаждете живых существ и стремитесь размножаться. Подстерегайте ничего не подозревающую добычу, принимая облик предметов вокруг.
ghost-role-information-morph-rules = Вы антагонист. Ешьте экипаж и размножайтесь.

roles-antag-morph-name = Морф
roles-antag-morph-objective = Ешьте живых существ и размножайтесь.
objective-issuer-morph = Голод

morph-greeting =
    Вы — морф.
    Вы жаждете живых существ и стремитесь размножаться. Добейтесь этого, подстерегая ничего не подозревающую добычу с помощью своих способностей.
    Как мерзость, созданная в основном из клеток генокрада, вы можете принять облик чего угодно поблизости с помощью Мимикрии.
    Превращение не останется незамеченным для стоящих рядом наблюдателей.
    В облике вы двигаетесь медленнее и наносите меньше урона. Кроме того, любой в трёх клетках, кто вас осмотрит, заметит что-то неладное.
    Зато из облика можно Подготовить засаду: первый удар нанесёт много урона, а если жертва сама вас коснётся — ещё больше.
    Атакуйте любой предмет или мёртвое существо, чтобы съесть его: существа лечат вас и пополняют запас еды, а предметы его отнимают.

# Еда
morph-food-left = У вас осталось еды: { $amount }.
morph-not-enough-food = Для этой способности нужно хотя бы { $cost } еды!
morph-eat-too-disgusting = Вы не можете заставить себя есть ещё больше гадости. Сначала съешьте что-нибудь живое.
morph-eat-start = Вы начинаете есть { $target }.
morph-eat-start-disgusting = Вы начинаете есть { $target }... мерзость...
morph-eat-start-others = { CAPITALIZE($morph) } начинает есть { $target }!
morph-swallow = { CAPITALIZE($morph) } проглатывает { $target } целиком!

# Мимикрия
morph-mimic-no-forms = Нет доступных обликов. Сначала запомните облики других предметов этой способностью.
morph-mimic-invalid = Этот облик принять нельзя.
morph-mimic-known = { CAPITALIZE($target) } — уже известный облик.
morph-mimic-forgetting = Вы начинаете забывать облик { $form }, чтобы запомнить новый.
morph-mimic-remember-start = Вы начинаете запоминать облик { $target }.
morph-mimic-remembered = Вы запомнили облик { $target }.
morph-mimic-take-start = Вы начинаете превращаться в { $form }.
morph-lose-focus = Вы теряете концентрацию.
morph-take-form-others = { CAPITALIZE($old) } вдруг выкручивается и меняет форму, становясь копией { $new }!
morph-take-form-self = Вы выкручиваете своё тело и принимаете облик { $new }.
morph-restore-others = { CAPITALIZE($old) } вдруг схлопывается внутрь себя, растекаясь кучей зелёной плоти!
morph-restore-self = Вы возвращаетесь в своё обычное тело.
morph-death-dissolve = { CAPITALIZE($morph) } выкручивается и растекается кучей зелёной плоти!
morph-form-window-title = Мимикрия
morph-form-original = Истинный облик

# Засада
morph-ambush-need-disguise = Подготовить засаду можно только в облике!
morph-ambush-already = Вы уже готовы!
morph-ambush-start = Вы начинаете готовить засаду.
morph-ambush-need-stay-morphed = Чтобы подготовить засаду, нужно оставаться в облике!
morph-ambush-need-stay-still = Чтобы подготовить засаду, нужно стоять неподвижно!
morph-ambush-ready = Вы готовы напасть из засады на ничего не подозревающую цель. Следующая атака нанесёт куда больше урона и собьёт цель с ног! Движение собьёт концентрацию. Неподвижность сделает маскировку безупречной.
morph-ambush-perfect = Вы довели маскировку до совершенства — теперь вас не отличить от настоящего облика!
morph-ambush-moved = Вы сошли с места засады!
morph-ambush-faded = Принимая истинный облик, вы упускаете момент для засады.
morph-ambush-leap = { CAPITALIZE($morph) } внезапно бросается на { $target }!
morph-ambush-touched-hand = Что-то тут не так... на ощупь оно... СКОЛЬЗКОЕ?!
morph-ambush-touched-item = Вы пытаетесь применить { $item }... но это выглядит иначе, чем...

# Шлюзы
morph-airlock-true-form = Проходить сквозь шлюзы можно только в истинном облике!
morph-airlock-bolted = { CAPITALIZE($airlock) } заблокирован болтами! Не получается создать щель, чтобы пролезть.
morph-airlock-start-others = { CAPITALIZE($morph) } начинает протискиваться в { $airlock }!
morph-airlock-start-self = Вы пытаетесь раздвинуть { $airlock } настолько, чтобы пролезть.
morph-airlock-stay-true-form = Чтобы пройти через { $airlock }, нужно оставаться в истинном облике!
morph-airlock-stay-still = Чтобы пройти через { $airlock }, нужно стоять неподвижно!
morph-airlock-pass-others = { CAPITALIZE($morph) } на миг приоткрывает { $airlock } и проскальзывает внутрь!
morph-airlock-pass-self = Вы проскальзываете в щель { $airlock }.

# Вентиляции
morph-vent-none = Рядом нет заваренных вентиляций!
morph-vent-start = Вы начинаете отрыгивать кислотную рвоту!
morph-vent-cancel = Вы глотаете кислоту обратно.

# Размножение
morph-reproduce-floor = Делиться можно только на полу!
morph-reproduce-start = Вы делитесь надвое!

# Осмотр
morph-examine-wrong = [color=red]Выглядит не совсем так, как должно...[/color]
morph-examine-food = Запас еды: { $amount }.
morph-examine-dead-contents = Челюсти { $morph } можно разжать ломом, чтобы достать съеденное.
morph-examine-dead-empty = [color=red]Внутри { $morph }, похоже, ничего не осталось![/color]
morph-jaws-empty = Внутри { $morph } ничего не осталось!
morph-jaws-pry-start = { CAPITALIZE($user) } начинает разжимать огромные челюсти морфа!
