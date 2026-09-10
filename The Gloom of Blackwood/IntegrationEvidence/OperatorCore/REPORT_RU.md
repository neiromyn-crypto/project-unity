# STEP 4 — OPERATOR CORE + ENEMY INTEGRATION

10.09.2026. PASS функционального этапа. Ветка codex/blackwood-integration-v2, сцена BlackwoodCoast.

Командный узел заменяет прежний визуальный Main Shelter в (22, 0, 5.2). OperatorCoreRoot содержит Visual, DamageZone, OperatorAnchor, HitPoints и OptionalEffects. HP хранятся только в CoastState: 300 integrity и 100 operator HP. Разрушающий удар полностью поглощается ядром, следующий повреждает оператора. При operator HP=0 — Death и Defeat; окно результатов появляется после короткой демонстрации смерти. Повторный запуск восстанавливает idle. Старое сохранение не перезаписывается: новая версия правил 3 и blackwood-operator-core-v1.json.

Враги следуют существующему полю расстояний к (21,7), затем занимают позиции перед узлом. Урон дискретный по attackInterval. Обычные здания не выбираются целью. При отсутствии пути проверяется барьер, удаление которого восстановит путь; после разрушения пересчитывается сетка. Полностью закрытые маршруты по-прежнему запрещены строительством. Прорыв учитывается один раз на врага.

| Роль / источник | HP | Скорость | Урон / интервал | Награда | Масштаб |
|---|---:|---:|---:|---:|---:|
| Walker — Zombie/FBXs/Zombie3.FBX | 48 | .85 | 10 / 1.25 | 2 | .82 |
| Runner — Cyber_Demon/Cyber_Demonn.fbx | 32 | 1.45 | 6 / .8 | 3 | .90 |
| Brute — TOLSTYAK/Mawbound Behemoth merged FBX | 200 | .60 | 24 / 2 | 12 | 1.15 |
| Special — Skeletal_Witch merged FBX | 90 | .80 | 12 / 1.5 | 8 | .90 |

CoastRules задаёт баланс, CoastCatalog — prefab и длительность смерти. Animator: Idle / Move / Attack / Death. Walker использует Z_Idle, Z_Walk_InPlace, Z_Attack, Z_FallingBack; Runner — Running, Right_Hand_Sword_Slash, Dead; Brute — Idle_9, Walking, Step_in_High_Kick, Fall_Dead_from_Abdominal_Injury; Special — Walking, Right_Hand_Sword_Slash, Shot_and_Fall_Forward. Пока нет отдельного idle у Runner/Special, используется неподвижная поза движения. Специальные способности не добавлены. Special зарегистрирован, но не включён в три ночи.

Оператор использует существующий tactical_operator: Short_Breathe_and_Look_Around и dying_backwards. Масштаб вычислен в редакторе по позе и вынесен за анимированную иерархию; runtime BakeMesh не нужен. Исправлены лишний поворот generic rig и отсутствовавшие ссылки на albedo. Исходные модели сохранены. Платформа технически упрощена из существующей модели: 1 968 376 → 31 856 треугольников; runtime использует отдельную mesh-копию и общий URP Lit материал. Модели для отдельных экземпляров не копируются. Повреждение — MaterialPropertyBlock, без VFX/lights.

Три foundation-ночи: 8 Walker; 8 Walker + 4 Runner; 8 Walker + 4 Runner + 1 Brute. Это тестовый набор, не финальный сложный баланс и не восьмидневная кампания. Экономика, workers, гранаты, FoW, дрон-комбат, география и лес не перерабатывались.

## Проверка

- 13 проверок Core: слои HP, состояния загрузки, цель/барьер и состав волн — core-checks.txt.
- 19 Play Mode проверок — play-checks.txt и play-result.txt. Все три ночи завершаются с доступной тестовой защитой, kills 8/12/13.
- Скриншоты intact / hit / vulnerable / death / defeat и enemy-roles просмотрены: персонажи стоят вертикально, оператор на платформе, HP и статус читаются.
- Unity compile: 0 errors; runtime: 0 errors/exceptions. Сохраняются 6 прежних CS0618 в старых editor/test файлах; новых предупреждений этап не добавляет.
- Проверка Scene View: median 7.087 → 6.198 ms, P95 8.517 → 7.046 ms. Одинаковый проход, 15 warmup + 80 samples, 1370×659, D3D12 RTX4050 Laptop. Это редакторный замер, не standalone FPS и не доказательство ускорения игры. Заметного ухудшения не обнаружено. Forest inventory без изменений: 81 renderer / 1 002 604 triangles включая неактивное меню и все LOD. Raw профили и CSV: ../Performance/step4-*.

## Ограничения и нужные ассеты

1. Более чистая игровая версия платформы, с крупными читаемыми деталями и damaged-вариантом: текущая импортированная поверхность слишком дробная вблизи.
2. Короткие безоружные attack-анимации для Runner/Brute/Special: сейчас используются ближайшие доступные slash/kick.
3. Один лёгкий hit/death VFX без realtime light для будущего этапа.

Ничего не скачано. STEP 4 завершён; следующие системы не начинались.
