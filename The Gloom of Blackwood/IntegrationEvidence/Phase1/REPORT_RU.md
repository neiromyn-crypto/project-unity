Фаза 1 — композиция существующей BlackwoodUserMap.

Объекты стали крупнее за счёт камеры, без изменения сетки и размеров построек. Низкоконтрастная земля выделяет рабочую зону центральной базы; группы сосен и камней заменяют равномерные ряды. Исходные пользовательские модели и материалы не изменены. Освещение, экономика, AI, волны и логика интерфейса сохранены.

Проверено в открытом Unity 6000.5.4f1: компиляция без ошибок; меню → день → первая ночь → второй день для пулемёта и трёх стен. Play-проход проверяет анимацию атаки, разрушение стены, паузу, смерть и пул. Все проверки пройдены; новый проход не содержит runtime errors/exceptions. В обеих пропорциях экрана крайние клетки находятся между панелями, обратная проекция на сетку даёт исходную точку с точностью <0.01. Декор проверен по Renderer.bounds: не пересекает игровое поле. Проверки Android и следующих фаз не выполнялись.

Изменённые файлы относительно f05141c:

- Assets/DawnGuard/BlackwoodV2/Generated/UserMap/BlackwoodUserMap.unity
- Assets/DawnGuard/BlackwoodV2/Generated/UserMap/ClearingGround.mat (+ .meta)
- Assets/DawnGuard/BlackwoodV2/Art/BlackwoodClearing.shader (+ .meta)
- Assets/DawnGuard/BlackwoodV2/Runtime/BlackwoodRoot.cs — параметры композиции в сцене.
- Assets/DawnGuard/BlackwoodV2/Runtime/BlackwoodWorld.cs — применение параметров камеры.
- Assets/DawnGuard/BlackwoodV2/Runtime/BlackwoodPanel.cs — обязательный CanvasRenderer; исправление существующей повторяющейся ошибки.
- Assets/DawnGuard/BlackwoodV2/Runtime/BlackwoodPlayVerification.cs — контроль UI-компонентов, крайних клеток и runtime errors.
- Assets/DawnGuard/BlackwoodV2/Editor/BlackwoodCompositionPass.cs (+ .meta) — воспроизводимое редактирование существующей сцены.
- Assets/DawnGuard/BlackwoodV2/Editor/BlackwoodLocalAutomation.cs — команда compose.
- IntegrationEvidence/WORK_STATE_RU.md, Phase1/, play-20260909-145350/, play-result.txt — состояние и реальные результаты.

Кадры — рендеры реальной сцены и UI в Play Mode через URP SingleCameraRequest, не концепты: ../play-20260909-145350/day-16x9.png, night-16x9.png, day-19_5x9.png, night-19_5x9.png. Меню и дополнительный кадр атаки находятся там же. Профиль теста изолирован в этой папке.

Текущая сцена уже сохранена. Пункт 8 Dawn Guard повторно применяет эту композицию; для обычной игры достаточно открыть сохранённую сцену и нажать Play. Пункт 6 повторно не запускать: он создаёт новую карту.
