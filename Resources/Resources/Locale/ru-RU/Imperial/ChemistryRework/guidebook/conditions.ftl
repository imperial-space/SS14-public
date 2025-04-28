reagent-effect-condition-guidebook-total-by-groups-damage =
    { $max ->
        [2147483648] тело имеет по крайней мере { NATURALFIXED($min, 2) } общего урона по следующим группам: { $groups } и типам: { $types }
       *[other]
            { $min ->
                [0] имеет не более { NATURALFIXED($max, 2) } общего урона по следующим группам: { $groups } и типам: { $types }
               *[other] имеет между { NATURALFIXED($min, 2) } и { NATURALFIXED($max, 2) } общего урона
            }
    }
