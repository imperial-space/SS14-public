## Rev Head

roles-antag-rev-head-name = Грейтайд
roles-antag-rev-head-objective = Я - Грейтайд. Моя цель - убивать.

head-rev-role-greeting =
    Я элитный робастер в составе могущественного Клана Грейтайда! Ассистент! 
    Моё оружие - тулбокс. Моя броня - сварочная маска. Без них, я ничто.
    Viva la greytide!

head-rev-briefing =
    Используйте вспышку чтобы указать заблудшим истину. 
    Возглавьте Серую Волну и свергните руководство станции. 

head-rev-initial-name = [color=#5e9cff]{$name}[/color] грейтайды это революционеры вроде.
head-rev-initial-name-user = [color=#5e9cff]{$name}[/color] ([color=gray]{$username}[/color]) грейтайды это революционеры вроде.

head-rev-initial-count = {$initialCount ->
    [one] Был один грейтайд:
    *[other] Было {$initialCount} грейтайдов:
}

head-rev-break-mindshield = Имплант защиты разума был уничтожен!

## Rev

roles-antag-rev-name = Рядовой Грейтайд
roles-antag-rev-objective = Ваша задача - защитить главных грейтайдов и свергнуть глав станции. 

rev-break-control = { $name } { $gender ->
        [male] вспомнил, кому он верен
        [female] вспомнила, кому она верна
        [epicene] вспомнили, кому они верни
       *[neuter] вспомнило, кому оно верно
    } на самом деле!

rev-role-greeting =
    Вы рядовой грейтай. 
    Вам поручено захватить станцию и защитить главных грейтайдов.
    Уничтожьте весь командный состав и службу безопасности.
    Viva la greyride!

rev-briefing = Помогите главным грейтайдам убить командующий состав и устранить службу безопасности, чтобы захватить станцию.

## General

rev-title = Грейтайды
rev-description = ДА ПРИБУДЕТ С ВАМИ РОБАСТ

rev-not-enough-ready-players = Недостаточно игроков готовы к игре! { $readyPlayersCount } игроков из необходимых { $minimumPlayers } готовы. Нельзя запустить пресет Революционеры.
rev-no-one-ready = Нет готовых игроков! Нельзя запустить пресет Революционеры.
rev-no-heads = Нет кандидатов на роль главы революции. Нельзя запустить пресет Революционеры.

rev-all-heads-dead = Все главы мертвы, теперь добейте остальную команду!

rev-won = Грейтайды выжили и уничтожили весь командный состав станции.

rev-lost = Члены командного состава станции выжили и уничтожили всех грейтайдов. 

rev-stalemate = Грейтайды и командный состав станции погибли. Анробасты. 

rev-reverse-stalemate = Грейтайды и командный состав, похоже, забыли на какую кнопку харм-мод. 

rev-headrev-count = { $initialCount ->
        [one] БЫЛ ОДИН СЕРЫЙ ВОИН:
       *[other] Главных грейтайдов было { $initialCount }:
    }

rev-headrev-name-user = [color=#5e9cff]{ $name }[/color] ([color=gray]{ $username }[/color]) конвертировал { $count } { $count ->
        [one] члена
       *[other] членов
    } экипажа

rev-headrev-name = [color=#5e9cff]{ $name }[/color] конвертировал { $count } { $count ->
        [one] члена
       *[other] членов
    } экипажа

## Deconverted window

rev-deconverted-title = Разконвертирован!
rev-deconverted-text =
    Со смертью последнего главы революции, революция оканчивается.

    Вы больше не революционер, так что ведите себя хорошо.
rev-deconverted-confirm = Подтвердить
