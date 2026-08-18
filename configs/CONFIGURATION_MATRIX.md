# AMC PRO V8.0 — Configuration Matrix

The project now contains one XML configuration per **instrument × preset**.

## Instruments
NQ, MNQ, ES, MES, GC, MGC, CL, MCL

## Presets
- `SCANNER`: discovery/research only
- `STANDARD`: general trading / benchmark
- `SCALPING`: intraday scalping
- `SCALPING_PRO`: premium live mode
- `SNIPER`: exceptional high-conviction setups

## Important
The files retain the project's existing instrument-specific calibration. The `TradingPreset` field selects the corresponding preset, and the existing `ApplyTradingPreset()` logic in the engine applies the preset-specific thresholds. This avoids inventing a second configuration schema that could drift from the actual NinjaTrader properties.

`ScalpingProAdaptive` is not present.
