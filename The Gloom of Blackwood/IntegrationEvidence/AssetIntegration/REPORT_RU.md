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
