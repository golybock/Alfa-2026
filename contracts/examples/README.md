# Fixtures для интерфейса и контрактов

Все company ID, публикации, банковские фрагменты, числа и UUID здесь синтетические. Это примеры формы, не реальные события/прогоны/доказательства. `example.invalid` намеренно не является источником; live-режим эти данные не использует.

| Сценарий | Файлы |
| --- | --- |
| Доступ к двум компаниям, ceo/cfo | me.json |
| Компания A: готовая/готовится/пустая лента | feed-ready.json, feed-pending.json, feed-empty.json |
| Компания A: CFO/CEO, тот же факт/score | insight.json, insight-ceo.json |
| Позиция банка отсутствует, actions скрыт | insight-no-bank.json |
| Компания B: депозит вместо кредита, другая связь с событием | feed-company-b.json, insight-company-b.json, insight-company-b-ceo.json |
| Чат: ответ/отказ | chat-answer.json, chat-refusal.json |
| Job: queued/done/failed | job-queued.json, job-done.json, job-failed.json |
| PDF: список, OCR готов, ручной review ещё нет | documents-list.json, document.json |
| Сохранение правки страницы, заголовок If-Match: fixture-v1 | page-review.json |
| Минимальный выход извлечения | news-extraction.json |

У ролей A одинаковые event/score/evidence, разные ID карточек и заголовки. B использует тот же внешний факт, но иной синтетический продукт/канал. Это тест рендера, не доказательство реальной персонализации: её проверяем на CLIENT-данных в A04/A10/K04.

Для арифметики примера принят src=1 и ageDays=0: только product → base=.20; без банка score=.19; с подходящим банковским фрагментом .24. Вклад product=.20 — вклад в base, не итоговый score. Разницу двух компаний нельзя оценивать по одной этой искусственной фикстуре.

Ссылка PNG в document.json — endpoint-заглушка. Для mock-режима Даня добавляет статическое изображение страницы/явный placeholder; в live этот URL обслуживает защищённый API. Значения 8–9% в графике — только иллюстрация формы review.