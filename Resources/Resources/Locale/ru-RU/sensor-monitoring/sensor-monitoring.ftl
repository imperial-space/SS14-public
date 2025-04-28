sensor-monitoring-window-title = Консоль мониторинга экипажа
sensor-monitoring-value-display =
    { $unit ->
        [PressureKpa] { PRESSURE($value) }
        [PowerW] { POWERWATTS($value) }
        [EnergyJ] { POWERJOULES($value) }
        [TemperatureK] { TOSTRING($value, "N3") } K
        [Ratio] { NATURALPERCENT($value) }
        [Moles] { TOSTRING($value, "N3") } моль
       *[Other] { $value }
    }
