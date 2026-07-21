using System.Numerics;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

public sealed class CaptainShipMegaphoneRadialMenu : RadialMenu
{
    private readonly NetEntity _megaphone;
    private readonly CaptainShipMegaphoneClientSystem _system;

    public CaptainShipMegaphoneRadialMenu(NetEntity megaphone, CaptainShipMegaphoneClientSystem system)
    {
        _megaphone = megaphone;
        _system = system;

        // Фиксация размера окна предотвращает скачки центра при смене слоев
        var menuSize = new Vector2(400, 400);
        MinSize = menuSize;
        SetSize = menuSize;

        // Компенсация движкового инкремента (+5f за каждую кнопку)
        // Целевой итоговый радиус всех слоев = 165f
        var radius2 = 155f; // Для 2 кнопок (155 + 10 = 165)
        var radius3 = 150f; // Для 3 кнопок (150 + 15 = 165)
        var radius4 = 145f; // Для 4 кнопок (145 + 20 = 165)

        // 0. Создаем все слои с выверенным начальным радиусом
        var mainLayer = new RadialContainer { Name = "Main", InitialRadius = radius3 };

        // Навигационные слои
        var combatNavLayer = new RadialContainer { Name = "CombatNav", InitialRadius = radius3 };
        var movementNavLayer = new RadialContainer { Name = "MovementNav", InitialRadius = radius3 };
        var otherNavLayer = new RadialContainer { Name = "OtherNav", InitialRadius = radius3 };

        // Подслои "Бой"
        var combatFireLayer = new RadialContainer { Name = "CombatFire", InitialRadius = radius3 };
        var combatArtilleryLayer = new RadialContainer { Name = "CombatArtillery", InitialRadius = radius3 };
        var combatBoardingLayer = new RadialContainer { Name = "CombatBoarding", InitialRadius = radius2 }; // 2 кнопки

        // Подслои "Движение"
        var movementStateLayer = new RadialContainer { Name = "MovementState", InitialRadius = radius4 }; // 4 кнопки
        var movementDirLayer = new RadialContainer { Name = "MovementDir", InitialRadius = radius4 }; // 4 кнопки
        var movementAnchorLayer = new RadialContainer { Name = "MovementAnchor", InitialRadius = radius2 }; // 2 кнопки

        // Подслои "Другое"
        var otherEmergencyLayer = new RadialContainer { Name = "OtherEmergency", InitialRadius = radius3 };
        var otherCrewLayer = new RadialContainer { Name = "OtherCrew", InitialRadius = radius3 };
        var otherGeneralLayer = new RadialContainer { Name = "OtherGeneral", InitialRadius = radius4 }; // 4 кнопки

        // --- 1. Главный слой (3 кнопки) ---
        AddNavButton(mainLayer, "Megaphone-menu-nav-combat", combatNavLayer);
        AddNavButton(mainLayer, "Megaphone-menu-nav-movement", movementNavLayer);
        AddNavButton(mainLayer, "Megaphone-menu-nav-other", otherNavLayer);

        // --- 2. БОЙ: Навигация (3 кнопки) ---
        AddNavButton(combatNavLayer, "Megaphone-menu-nav-combat-fire", combatFireLayer);
        AddNavButton(combatNavLayer, "Megaphone-menu-nav-combat-artillery", combatArtilleryLayer);
        AddNavButton(combatNavLayer, "Megaphone-menu-nav-combat-boarding", combatBoardingLayer);

        // 2.1 Огонь (3 кнопки)
        AddOrderButton(combatFireLayer, "Megaphone-menu-combat-fire-left");
        AddOrderButton(combatFireLayer, "Megaphone-menu-combat-fire-right");
        AddOrderButton(combatFireLayer, "Megaphone-menu-combat-fire-forward");

        // 2.2 Артиллерия (3 кнопки)
        AddOrderButton(combatArtilleryLayer, "Megaphone-menu-combat-prepare-cannons");
        AddOrderButton(combatArtilleryLayer, "Megaphone-menu-combat-load-grapeshot");
        AddOrderButton(combatArtilleryLayer, "Megaphone-menu-combat-load-cannonballs");

        // 2.3 Абордаж (2 кнопки)
        AddOrderButton(combatBoardingLayer, "Megaphone-menu-combat-board-attack");
        AddOrderButton(combatBoardingLayer, "Megaphone-menu-combat-board-prepare");

        // --- 3. ДВИЖЕНИЕ: Навигация (3 кнопки) ---
        AddNavButton(movementNavLayer, "Megaphone-menu-nav-movement-state", movementStateLayer);
        AddNavButton(movementNavLayer, "Megaphone-menu-nav-movement-dir", movementDirLayer);
        AddNavButton(movementNavLayer, "Megaphone-menu-nav-movement-anchor", movementAnchorLayer);

        // 3.1 Состояние парусов (4 кнопки)
        AddOrderButton(movementStateLayer, "Megaphone-menu-movement-sails-lower");
        AddOrderButton(movementStateLayer, "Megaphone-menu-movement-sails-raise");
        AddOrderButton(movementStateLayer, "Megaphone-menu-movement-sails-with-wind");
        AddOrderButton(movementStateLayer, "Megaphone-menu-movement-sails-against-wind");

        // 3.2 Направление (4 кнопки)
        AddOrderButton(movementDirLayer, "Megaphone-menu-movement-sails-forward");
        AddOrderButton(movementDirLayer, "Megaphone-menu-movement-sails-backward");
        AddOrderButton(movementDirLayer, "Megaphone-menu-movement-sails-leftward");
        AddOrderButton(movementDirLayer, "Megaphone-menu-movement-sails-rightward");

        // 3.3 Якорь (2 кнопки)
        AddOrderButton(movementAnchorLayer, "Megaphone-menu-movement-anchor-drop");
        AddOrderButton(movementAnchorLayer, "Megaphone-menu-movement-anchor-raise");

        // --- 4. ДРУГОЕ: Навигация (3 кнопки) ---
        AddNavButton(otherNavLayer, "Megaphone-menu-nav-other-emergency", otherEmergencyLayer);
        AddNavButton(otherNavLayer, "Megaphone-menu-nav-other-crew", otherCrewLayer);
        AddNavButton(otherNavLayer, "Megaphone-menu-nav-other-general", otherGeneralLayer);

        // 4.1 ЧС (3 кнопки)
        AddOrderButton(otherEmergencyLayer, "Megaphone-menu-other-attention");
        AddOrderButton(otherEmergencyLayer, "Megaphone-menu-other-repair-ship");
        AddOrderButton(otherEmergencyLayer, "Megaphone-menu-other-pump-water");

        // 4.2 Экипаж (3 кнопки)
        AddOrderButton(otherCrewLayer, "Megaphone-menu-other-call-assistant");
        AddOrderButton(otherCrewLayer, "Megaphone-menu-other-call-all");
        AddOrderButton(otherCrewLayer, "Megaphone-menu-other-to-positions");

        // 4.3 Общее (4 кнопки)
        AddOrderButton(otherGeneralLayer, "Megaphone-menu-other-leave-ship");
        AddOrderButton(otherGeneralLayer, "Megaphone-menu-other-stand-down");
        AddOrderButton(otherGeneralLayer, "Megaphone-menu-other-do-it");
        AddOrderButton(otherGeneralLayer, "Megaphone-menu-other-good-job");

        // --- ДОБАВЛЕНИЕ СЛОЕВ В МЕНЮ ---
        AddChild(mainLayer);

        AddChild(combatNavLayer);
        AddChild(combatFireLayer);
        AddChild(combatArtilleryLayer);
        AddChild(combatBoardingLayer);

        AddChild(movementNavLayer);
        AddChild(movementStateLayer);
        AddChild(movementDirLayer);
        AddChild(movementAnchorLayer);

        AddChild(otherNavLayer);
        AddChild(otherEmergencyLayer);
        AddChild(otherCrewLayer);
        AddChild(otherGeneralLayer);
    }

    private void AddNavButton(RadialContainer container, string text, RadialContainer targetLayer)
    {
        var button = CreateBaseButton(Loc.GetString(text));
        button.TargetLayer = targetLayer;
        container.AddChild(button);
    }

    private void AddOrderButton(RadialContainer container, string command)
    {
        var button = CreateBaseButton(Loc.GetString(command));
        button.OnButtonUp += _ =>
        {
            _system.SendOrderToServer(_megaphone, command);
            Close();
        };
        container.AddChild(button);
    }

    private RadialMenuButtonWithSector CreateBaseButton(string text)
    {
        var button = new RadialMenuButtonWithSector
        {
            DrawBackground = true,
            DrawBorder = true,
            BackgroundColor = Color.FromHex("#7e7e7e33"),
            HoverBackgroundColor = Color.FromHex("#7e7e7e33"),
            MouseFilter = MouseFilterMode.Stop
        };

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Align = Label.AlignMode.Center,
            MouseFilter = MouseFilterMode.Ignore
        };

        button.AddChild(label);
        return button;
    }
}
