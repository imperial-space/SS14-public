## Выживший

roles-antag-survivor-name = Выживший
# Это отсылка к Halo
roles-antag-survivor-objective = Текущая цель: Выжить

survivor-role-greeting =
    Вы Выживший.
    Прежде всего, вам нужно вернуться на ЦентКом живым.
    Соберите столько оружия, сколько нужно, чтобы гарантировать ваше выживание. Вы можете убивать других [color=red]Выживших[/color].
    Не доверяйте никому.

survivor-round-end-dead-count =
{
    $deadCount ->
        [one] [color=red]{$deadCount}[/color] выживший погиб.
        *[other] [color=red]{$deadCount}[/color] выживших погибло.
}

survivor-round-end-alive-count =
{
    $aliveCount ->
        [one] [color=yellow]{$aliveCount}[/color] выживший остался на станции.
        *[other] [color=yellow]{$aliveCount}[/color] выживших осталось на станции.
}

survivor-round-end-alive-on-shuttle-count =
{
    $aliveCount ->
        [one] [color=green]{$aliveCount}[/color] выживший выбрался живым.
        *[other] [color=green]{$aliveCount}[/color] выживших выбралось живым.
}

## Маг

objective-issuer-swf = [color=turquoise]КАЛДУНСКАЯ ОБЩИНА ПРИКОЛИСТОВ[/color]

wizard-title = Мага
wizard-description = На станции есть Мага! Никогда не знаешь, что он может сделать.

roles-antag-wizard-name = Калдун
roles-antag-wizard-objective = Все что мне дорого, это хи-хи-ха-ха..  

wizard-role-greeting =
    ТЫ МАГА!
    Между Калдунской Общиной Приколистов и Шутнярами из НТ возникли разногласия. 
    Они считают, что их шутки смешнее наших... Это мы ещё посмотрим. 

wizard-round-end-name = КАЛДУН

## TODO: Ученик Мага (Появится после релиза Wizard)
