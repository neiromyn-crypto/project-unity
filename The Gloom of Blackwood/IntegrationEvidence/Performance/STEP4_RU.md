# STEP 4 performance

Before: step4-before-summary.txt, after: step4-after-summary.txt. Median 7.087 → 6.198 ms; P95 8.517 → 7.046 ms. PASS: заметного ухудшения Scene View не обнаружено. 15 warmup + 80 samples, 1370×659, D3D12 RTX4050 Laptop; профили .raw, Stats CSV и screenshots сохранены. Repaint interval включает работу редактора и планирование ОС: не standalone FPS. Forest inventory неизменен. Новая платформа использует 31 856 tris вместо исходных 1 968 376. Три малые ночи проверены в Play Mode без исключений; массовые волны не тестировались и не вводились.
