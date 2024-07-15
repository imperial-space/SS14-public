whitelist-not-whitelisted = Вас нет в вайтлисте.

# proper handling for having a min/max or not
whitelist-playercount-invalid = {$min ->
    [0] Вайтлист для этого сервера применяется только для числа игроков ниже {$max}.
    *[other] Вайтлист для этого сервера применяется только для числа игроков выше {$min} {$max ->
        [2147483647] -> так что, возможно, вы сможете присоединиться позже.
       *[other] ->  и ниже {$max} игроков, так что, возможно, вы сможете присоединиться позже.
    }
}
whitelist-not-whitelisted-rp = Вас нет в вайтлисте. Чтобы попасть в вайтлист, посетите наш Discord (ссылку можно найти по адресу: нету).

cmd-whitelistadd-desc = Добавить игрока с указанным юзернеймом в вайтлист.
cmd-whitelistadd-help = whitelistadd <username>
cmd-whitelistadd-existing = {$username} уже в вайтлисте!
cmd-whitelistadd-added = {$username} добавлен в вайтлист
cmd-whitelistadd-not-found = Пользователь '{$username}' не найден
cmd-whitelistadd-arg-player = [player]

cmd-whitelistremove-desc = Удалить игрока с указанным юзернеймом из вайтлиста.
cmd-whitelistremove-help = whitelistremove <username>
cmd-whitelistremove-existing = {$username} не в вайтлисте!
cmd-whitelistremove-removed = Пользователь {$username} удалён из вайтлиста
cmd-whitelistremove-not-found = Пользователь '{$username}' не найден
cmd-whitelistremove-arg-player = [player]

cmd-kicknonwhitelisted-desc = Кикнуть с сервера всех пользователей не из вайтлиста.
cmd-kicknonwhitelisted-help = kicknonwhitelisted

ban-banned-permanent = Вы получили перманентный бан.
ban-banned-permanent-appeal = Вы получили перманентный бан.
ban-expires = Вы получили бан на {$duration} минут, и он истечёт {$time} по UTC (для москосвкого времени добавьте 3 часа).
ban-banned-1 = Вам, или другому пользователю этого компьютера или соединения, запрещено здесь играть.
ban-banned-2 = Причина бана: "{$reason}"
ban-banned-3 = Попытки обойти этот бан, например, путём создания нового аккаунта, будут фиксироваться.

soft-player-cap-full = Сервер заполнен!
panic-bunker-account-denied = Этот сервер находится в режиме "Бункер". В данный момент новые подключения не принимаются. Повторите попытку позже
panic-bunker-account-denied-reason = Этот сервер находится в режиме "Бункер", и вам было отказано в доступе. Причина: "{$reason}"
panic-bunker-account-reason-account = Ваш аккаунт должен быть старше {$minutes} минут
panic-bunker-account-reason-overall = Необходимо минимальное отыгранное время {$hours} часов

ban-you-can-appeal = Вы можете обжаловать бан, для этого откройте соотвествующий тикет в канале "поддержка" в нашем Discord
