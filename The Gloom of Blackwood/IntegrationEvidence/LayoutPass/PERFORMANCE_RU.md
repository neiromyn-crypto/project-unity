# STEP 5 — Performance

PASS: заметного ухудшения Scene View не обнаружено.

| Замер | Before | Final after |
|---|---:|---:|
| Median repaint interval, ms | 6.180 | 6.187 |
| P95, ms | 7.075 | 7.277 |
| Mean, ms | 6.218 | 6.184 |

Тот же sweep: 15 warmup + 80 samples, 1370×659, D3D12, RTX4050 Laptop. Исходные CSV, raw Profiler и screenshots: ../Performance/step5-before-* и step5-after-*. Это замер редактора, а не standalone FPS или гарантия производительности на других устройствах.

Промежуточный вариант дал 7.119 ms median / 8.151 P95. Причина: повторное использование тяжёлого User_pad для двух новых декоративных оснований; Editor Stats вырос с примерно 24.25 до 33.71 млн отрисованных треугольников (глобальный счётчик с проходами, не уникальная геометрия). Этот вариант убран. Финальные основания используют общий mesh из 40 треугольников и два существующих материала; тяжёлая площадка больше не дублируется. Промежуточные измерения сохранены в added-pad-performance.txt и added-pad-samples.csv, итоговый PASS опирается на финальный вариант.

Forest inventory: без изменения бюджета — 81 renderer и 1 002 604 triangle instances с учётом меню и всех LOD. Новые cameras/lights/shaders/particles не добавлялись. Три малые ночи завершены в Play Mode без exceptions. Grounding использует кэшированные ссылки, полёт — существующий fixed simulation tick. В Update нет нового Find/GetComponent. Mesh основания освобождается при уничтожении World.
