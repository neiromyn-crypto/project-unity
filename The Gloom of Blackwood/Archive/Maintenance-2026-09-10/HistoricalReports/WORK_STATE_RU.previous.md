Фаза 1 завершена и проверена 09.09.2026; фазы 2–9 не начаты.
Исходная точка: commit f05141c; существующая архитектура DawnGuard/BlackwoodV2 сохранена.
Сцена: Assets/DawnGuard/BlackwoodV2/Generated/UserMap/BlackwoodUserMap.unity.
Камера: orthographicSize 9.6 → 7.6 (+26.3% по ширине объектов), elevation 38°, focus (8,0,7.8).
Земля: ClearingGround.mat + BlackwoodClearing.shader; слабая детализация исходной текстуры и рабочая поляна вокруг базы.
Окружение: 28 сосен в нерегулярных группах, 12 камней по краям; 13 препятствий ядра сохранены; декор не пересекает поле.
Исправлена обязательная зависимость BlackwoodPanel от CanvasRenderer, устраняющая повторяющийся MissingComponentException.
Unity 6000.5.4f1: 0 compile errors; Play PASS, 0 runtime errors/exceptions; проверены день/ночь, две стратегии, Animator и выбор клеток.
Кадры и проверки: IntegrationEvidence/play-20260909-145350/ (1600×900 и 1950×900); снимок сцены до правок: IntegrationEvidence/Phase1/.
Assets/GameArt, Assets/Enemy/TOLSTYAK, каталог, баланс, AI и рабочие сохранения этой фазой не изменялись.
Остановлено после Фазы 1; следующий этап — только после подтверждения пользователя. Android на устройстве не проверялся.
