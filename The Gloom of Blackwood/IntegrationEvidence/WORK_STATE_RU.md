Актуальный этап: STEP 3A — Drone Readability + Camera Focus, 10.09.2026 — PASS.
Visual дрона увеличен на 30%, локально поднят на 0.25; root/скорость/логика/коллизии не менялись.
Дрон: source.dronePrefab → BlackwoodV2/Generated/UserMap/User_drone.prefab → CoastWorld.Place → CoastDronePresentation.
Кольцо: 96 треугольников, один прозрачный URP Unlit material, y=0.035; overhead — billboard diamond + amber accent.
Space (Input System) плавно фокусирует существующую камеру; WASD/drag/zoom/Home и bounds сохранены.
Offscreen-стрелка создаётся один раз в существующем Canvas; меню/пауза/service скрывают её. Подсказки HUD разнесены.
Unity Compile PASS, Play Mode PASS: X/Z на четырёх участках, Space через Input System, bounds, день/ночь/max zoom, UI CanvasRenderer, 0 exceptions.
Scene View: медиана 6.995 → 7.116 мс, P95 8.089 → 8.121 мс; без заметного ухудшения, не standalone FPS.
Доказательства: DroneReadability/*; Performance/step3-*-summary.txt и samples.csv.
Recon scaffold пропущен как необязательный. Fog of War/гранаты/экономика/лес не менялись.
Пользователь во время завершения сменил следующую задачу на STEP 4 — OPERATOR CORE + ENEMY INTEGRATION.
Актуальная версия: BlackwoodLTD / береговой рубеж, функциональная альфа.
Последний этап: ШАГ 2 — LARGE MAP / MAZE GREYBOX / CAMERA, 10.09.2026 — PASS.
Логическое поле 44×36, единичный масштаб клеток сохранён. SOUTH — берег, Main Shelter x=22/z=5.2, цель пути (21,7), Drone Pad рядом (25.5,1).
NORTH (21,34), WEST (0,26), EAST (42,21): пути 41/40/35 шагов, footprint 2×2. 278 клеток скальных гряд; альтернативные коридоры проверены перекрытием целых проходов. Три площадки для будущих kill-zones отмечены в отчёте.
Камера: размер 8–13, ограниченный focus, WASD/стрелки/перетаскивание; колесо — масштаб, Home — лагерь. CoastCameraRig отделён от будущей видимости; Fog of War не добавлен.
Лес не переоптимизировался: те же mesh/material/LOD, 24 individual + 132 кроны в 6 кластерах карты; 40 крон в 3 кластерах меню. Только размещение/границы секторов адаптированы к карте. Коллайдеров/добавленных lights/materials/shaders нет.
UnityStats.frameTime заменён на FrameTimingManager.GetLatestTimings; при отсутствии измерения — NaN. 0 compile errors, целевой warning устранён. Сохраняются 5 прежних obsolete warnings в редакторе BlackwoodV2, вне этого этапа.
Performance при прежнем протоколе Scene View: медиана 7,163 → 7,044 мс, P95 7,997 → 8,013 мс. 10-секундная проверка движения камеры в Play Mode — без ошибок/исключений. Это не замер FPS standalone.
Баланс, waves, экономика, новые workers/towers/сапёры/гранаты/разведка не реализовывались и не перенастраивались. Координаты попадания в лагерь адаптированы к его позиции, правила урона прежние. Существующие работники только визуально смещены вместе с береговыми объектами.
Новая карта использует blackwood-coast-map2-v1.json; старое blackwood-coast-v1.json сохранено и не мигрируется. Версия правил 2 обозначает несовместимую географию.
Доказательства: LargeMap/REPORT_RU.md, LargeMap/checks.txt, LargeMap/play-smoke.txt, LargeMap/maze-routes.svg, Performance/step2-*. Результаты ниже относятся к предшествующему forest pass.
После отдельного commit остановлено. Следующие системы не начинались.

Последний завершённый проход (10.09.2026): PERFORMANCE PASS леса BlackwoodCoast.
24 индивидуальных дерева с 3 LOD; 132 дальние кроны карты в 6 кластерах; 40 крон меню в 3 кластерах.
Один общий материал URP Lit с instancing; тени только у 8 ближних LOD0; коллайдеров нет; лес исключён из NavMesh.
Scene View, одинаковый проход камеры: медиана 90,316 → 7,006 мс, P95 94,847 → 7,974 мс. Это замер редактора, не FPS сборки.
11 проверок леса пройдены; 0 compile errors. Gameplay в этом проходе не изменялся (проверены SHA256 Core).
Profiler, полная инвентаризация, ограничения UnityStats и сравнение кадров: Performance/REPORT_RU.md.
Остановлено после performance pass по запросу пользователя. Общая оптимизация остальных моделей, камни, дрон и дальнейшие фазы не выполнялись.
Сцена: Assets/DawnGuard/BlackwoodLTD/Scenes/BlackwoodCoast.unity.
Старый текст «завершена только фаза 1» перенесён в Archive/Maintenance-2026-09-10/HistoricalReports/WORK_STATE_RU.previous.md.

Порядок запуска: ../README_RU.md.
Фактическое состояние и план: ../Design/Blackwood_LTD_Concept_2026-09-09/STATUS_AND_ROADMAP_RU_2026-09-10.md.
Текущие отчёты новой версии: Coast/core-checks.txt, Coast/play-result.txt, Coast/rollback-check.txt.
Отчёты в корне IntegrationEvidence относятся к предыдущей V2, если в них явно не указано иное.


## NEXT THREAD
1. Сцена: Assets/DawnGuard/BlackwoodLTD/Scenes/BlackwoodCoast.unity; ветка codex/blackwood-integration-v2.
2. Работают меню, день/ночь, строительство, турели, враги, пути и большая береговая карта.
3. Forest pass: 24 ближних дерева с LOD, 132 кроны/6 кластеров карты, 40 крон/3 кластера меню; instancing; тени лишь у 8 LOD0; без Collider/NavMesh.
4. Карта 44×36; SOUTH берег; camp x22/z5.2, goal (21,7); камера size8–13, focus x4..40/z5..33.
5. Дрон: Visual ×1.3 и +0.25y; кольцо у земли; overhead diamond; Space focus; HUD offscreen arrow.
6. Код: Runtime/CoastDronePresentation.cs, CoastWorld.cs, CoastCameraRig.cs, CoastRoot.cs, CoastHud.cs (всё в BlackwoodLTD).
7. Проверки: CoastVerification.cs, команда drone-checks через IntegrationEvidence/Coast/command.txt; тестовые сохранения в Temp.
8. Не сделаны: Recon/Fog of War/scaffold, граната, новые workers, экономика, старт MainShelter+DronePad.
9. Новый запрос пользователя: сначала STEP 4 — Operator Core, Operator HP, новые враги и foundation Night 1–3.
10. После STEP 4 прежняя очередь: DRONE RECON + LIGHTWEIGHT FOG OF WAR.
11. После Recon: START PROGRESSION — только MainShelter + DronePad.
12. После этого: DRONE GRENADE SYSTEM.
