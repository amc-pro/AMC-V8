# AMC PRO V8.0 — Final Trading Engine Cleanup

## Change
Removed the `ScalpingProAdaptive` trading preset from the engine.

## What changed
- Removed `ScalpingProAdaptive` from `SniperMarketPreset`.
- Removed the preset dispatch branch from `ApplyTradingPreset`.
- Removed `ApplyScalpingProAdaptivePreset()`.
- `IsScalpingPro` now matches only `ScalpingPro`.
- Unified `WeightedScoreModel` to a single deterministic weighting:
  - Structure 30%
  - Footprint 30%
  - Volume 15%
  - Momentum 15%
  - Context 10%
- Removed the adaptive scoring branch (`35/10/20/20/15`).
- Updated README references so the retired preset is no longer advertised.

## Deliberately preserved
The word `Adaptive` still exists in other independent microstructure calibration features such as adaptive movement/absorption thresholds and adaptive volume-profile calibration. These are **not** the `ScalpingProAdaptive` trading preset and were not removed.

## Validation
- No `ScalpingProAdaptive` reference remains in the source tree.
- C# brace balance checked for modified core files.
- The ZIP contains the complete source/config/test tree.

## Important
A NinjaTrader/MetaEditor build cannot be executed in this environment because the proprietary NinjaTrader assemblies and MetaEditor toolchain are not installed. Final compilation should therefore be performed in the target NinjaTrader/MetaEditor environment before live deployment.
