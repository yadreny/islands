---
tags:
  - reference
  - draft
  - islands
  - generation
---

# Форма острова и shape API

## Внешний контракт

```text
IslandShapeRequest
- preset
- targetArea
- targetMaxElevation
- aspectPercent
- reliefComplexityPercent
- direction
- seed
```

## Семантика полей

- `preset` — архетип формы острова.
- `targetArea` — итоговая площадь острова в игровых единицах.
- `targetMaxElevation` — итоговая максимальная высота острова в игровых единицах.
- `aspectPercent` — усиление типовой вытянутости пресета.
- `reliefComplexityPercent` — усиление типовой сложности рельефа.
- `direction` — направление главной оси острова.
- `seed` — сид генерации.

## Внутренности пресета

```text
IslandPreset
- id
- displayName
- recommendedArea
- recommendedMaxElevation
- recommendedAspectRatio
- recommendedReliefComplexity
- footprintFill
- massLayoutType
- reliefProfile
```

## Что означают скрытые поля

- `recommendedArea` — опорная площадь архетипа.
- `recommendedMaxElevation` — опорная максимальная высота.
- `recommendedAspectRatio` — типовая вытянутость.
- `recommendedReliefComplexity` — типовая дробность рельефа.
- `footprintFill` — насколько суша заполняет габарит острова.
- `massLayoutType` — тип большой массы: `SingleCore`, `DoubleCore`, `Arc`, `ContinentalBlock`, `BrokenBlock`, `Ring`, `DrownedRelief`.
- `reliefProfile` — тип вертикального рисунка: `CentralCone`, `TwinPeaks`, `ArcRidge`, `MultiRidge`, `BrokenMassif`, `DrownedHills`, `LowRing`.

## Базовые формулы

```text
finalArea = targetArea
finalMaxElevation = targetMaxElevation
finalAspectRatio = 1 + (recommendedAspectRatio - 1) * aspectPercent / 100
finalReliefComplexity = recommendedReliefComplexity * reliefComplexityPercent / 100
```

## Стартовый набор пресетов

- Young Volcanic
- Old Volcanic
- Twin Volcanic
- Arc Island
- Compact Continental
- Rugged Continental
- Drowned Hills
- Reef-Fringed High Island
- Atoll
- Broken Remnant

## Принципы

- Площадь и высота задаются сразу в игровых единицах.
- Вытянутость и сложность рельефа модифицируют характер пресета, а не заменяют его.
- Пресет отвечает за характер формы, а не за политический или исторический слой острова.

## Связанная заметка

- [[01 Генератор островов/06 Генерация формы острова]]