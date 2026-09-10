ASSET_INTEGRATION_FIXED — STEP 5B, 10.09.2026 — PASS.
Ветка codex/blackwood-integration-v2; сцена BlackwoodCoast; текущий шаг завершён.
Core asset: Assets/GameArt/comandpunktforoperator/comandpunktforoperator.fbx, оригинальные 1 968 376 tris/UV; повреждённая reduced-копия больше не используется.
Visual: равномерный wrapper scale ≈2.31, ширина4.4 cells, высота2.28, глубина3.37; исходный FBX scale100 сохранён.
Материал: отдельный существующий Core_0 URP adapter, исходная albedo texture, Opaque, alpha1; никаких runtime material/property-block изменений при уроне. Исходники GameArt/Enemy не изменены, SHA256 проверены.
Структура: OperatorCoreRoot → Visual / OperatorAnchor / DamageCollider / AttackPoints / Effects; прежние apron/overlay удалены.
DamageCollider: отдельный BoxCollider trigger4.4×2.28×3.37, local center(0,1.14,0); ни Renderer, ни материала.
OperatorAnchor y≈0.956 — высота центральной поверхности по оригинальным треугольникам; стопы привязаны к ней только до Death.
Operator: tactical_operator_rig_biped_Animation_all_frame_rate_60.fbx; Short_Breathe_and_Look_Around → dying_backwards; visual scale1.3 (menu1.6). Нулевых gameplay clips нет.
AttackPoints: 10 невидимых дочерних anchors; радиусы X3.1/Z2.8; deterministic reservation, обход по коротким сегментам, ожидание при заполнении и повторное занятие освободившегося места.
Интеграционный тест12 врагов: 10 атакующих + 2 ожидающих; минимальное расстояние финальных позиций1.7305. Точки окружают узел с четырёх сторон; урон по существующему interval.
Walker → Z_Walk_InPlace / Z_Attack / Z_FallingBack; восстановленный Human Avatar сохранён.
Runner → Running / Right_Hand_Sword_Slash / Dead; собственный Generic rig.
Brute → Walking / Step_in_High_Kick / Fall_Dead_from_Abdominal_Injury; собственный Human Avatar.
Special → Walking / Right_Hand_Sword_Slash / Shot_and_Fall_Forward; собственный Generic rig. В волны не добавлялся.
Menu anchors: brute(114,.02,11.8), walker(119,.02,13), runner(124,.02,11.8). TOLSTYAK scale1.25, Idle_9; только3 menu enemies. Оператор на полном Core, корпуса сзади/сбоку.
Drone Visual: catalog.source.dronePrefab → CoastWorld.Place → CoastDronePresentation.Visual; реальный enabled MeshRenderer проверен. Старт теперь из сохранённых координат; Pad перенесён к берегу(27.2,1.2).
Проверен путь: spawn → взлёт → ПКМ → перемещение модели → focus → возврат к Pad/idle в воздухе. Маркер остаётся вспомогательным.
TEST: 52 integration checks + 13 Core regression PASS; Move/Attack/Death проверены по движению костей, смерть не выходит в Move; три ночи и HP300→100→Defeat работают.
0 compile errors, 0 runtime errors/exceptions. Пустые служебные импортированные clips не назначаются и не удалялись; прежний CS0618 в CoastVerification.cs вне изменённого кода остаётся.
PERFORMANCE Scene View: median6.169→6.719ms (+0.550ms), P957.530→7.590ms; тот же sweep15+80, 1370×659/D3D12/RTX4050 Laptop. Это Editor repaint, не standalone FPS.
Промежуточный замер7.521/P959.902 сохранён в first-performance*; окончательный замер проведён после финальной проверки при спокойном редакторе. Raw/CSV: Performance/step5b-*.
Лес не переделывался; его инвентаризация81 renderer/1 002 604 tris(all LOD/menu) прежняя. Полный Core дороже прежних31 856 tris; тени этой модели отключены, геометрия не упрощается.
Ограничения: высокополигональный исходник Core; условные slash/kick attack-клипы. Сложное взаимное уклонение толпы не входит в этот шаг.
Доказательства: AssetIntegration/play-checks.txt, core-regression.txt, geometry.txt, animation-audit.txt, source-integrity.json, скриншоты.
NEXT: ОСТАНОВЛЕНО после STEP5B. DRONE GRENADE + ENEMY CROWD COMBAT — только следующий отдельный шаг.

## История предыдущих этапов

Актуальный этап: STEP 5 — BASE LAYOUT + MENU FIX + ENEMY ANIMATION + DRONE RESTORE, 10.09.2026 — PASS.
Ветка codex/blackwood-integration-v2; сцена BlackwoodCoast. Узел увеличен до визуальных габаритов 5.8×2.2, оператор scale1.4; лёгкое общее основание 40 tris. HP 300/100 и defeat сохранены. DamageZone/подход врагов согласованы по z8.1.
База разнесена: казарма/оружейная слева, lab/power справа, Pad (26.8,5.8); сервисные клики обновлены. Карта 44×36: центральный выступ и два обхода, все три фронта доступны; пути 47/40/35 шагов. Оптимизация леса сохранена.
Меню: оператор на узле, четыре ходящих врага у леса, стопы на земле. Walker Human Avatar восстановлен из имеющегося rig description; проверено реальное движение костей всех 4 ролей и Death. Grounding не вмешивается в смерть.
Дрон: взлёт с .45 до2.6, ПКМ по полю — полёт, Space — фокус; WASD/drag — камера. Команда ограничена картой и сохраняется. Пауза останавливает полёт. Ремонт/строительство используют те же координаты дрона.
Правила version4; save blackwood-layout-v1.json. Предыдущий operator-core-v1 сохранён без миграции из-за изменённых препятствий.
36 Play checks + 13 Core regression PASS; 0 compile errors, 0 runtime exceptions. Прежний CS0618 в CoastVerification.cs не относится к изменённому коду.
Scene View median 6.180 → 6.187 ms; P95 7.075 → 7.277 ms. Промежуточное утяжеление от копий pad выявлено и устранено; детали LayoutPass/PERFORMANCE_RU.md, raw/CSV Performance/step5-*.
Отчёт, скриншоты, проверки: LayoutPass/REPORT_RU.md. Нужны clean/damaged платформа, безоружные attack clips, лёгкий core hit VFX — пакеты не скачивались.
NEXT: ОСТАНОВЛЕНО после STEP5. Следующий этап только по отдельному запросу; гранаты/экономика/FoW/новые враги/рабочие не начинались.

## История предыдущих этапов

Актуальный этап: STEP 4 — OPERATOR CORE + ENEMY INTEGRATION, 10.09.2026 — PASS.
Ветка codex/blackwood-integration-v2; сцена BlackwoodCoast. Рабочий узел (22,0,5.2): ядро 300 HP → оператор 100 HP → Death/Defeat. При открытом пути враги идут к узлу; при блокировке атакуют восстанавливающий путь барьер.
4 роли подключены к существующим моделям/анимациям через CoastRules и CoastCatalog. Три тестовые ночи: 8 / 12 / 13 врагов; Special только зарегистрирован. Новые saves: blackwood-operator-core-v1.json, rulesVersion 3.
Оператор breathing idle, смерть только при поражении; двойной HP HUD, vulnerable status и дешёвый hit/damaged tint. Платформа — упрощённая копия имеющегося mesh, исходник сохранён. Нормализация персонажей выполняется в редакторе.
13 Core + 19 Play checks PASS; 0 compile errors, 0 runtime exceptions. 6 старых obsolete warnings вне изменённого кода остаются.
Scene View: median 7.087 → 6.198 ms, P95 8.517 → 7.046 ms; тот же протокол, не standalone FPS. Лес и карта сохранены.
Подробности, соответствие ассетов/клипов, ограничения: OperatorCore/REPORT_RU.md. Доказательства: OperatorCore/* и Performance/step4-*.
NEXT: остановиться; следующий этап только по отдельному запросу. Нужны чистая/damaged платформа, безоружные attack clips, лёгкий hit/death VFX. Текущий набор волн — foundation, не финальный баланс.

## История предыдущих этапов (следующие указания ниже исторические)

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
