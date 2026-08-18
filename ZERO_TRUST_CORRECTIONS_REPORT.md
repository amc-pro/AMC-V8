# AMC PRO V8.0 — Zero-Trust Corrections Applied

## Scope
Corrections applied to the uploaded `SniperMarketCorePro_V7.8(2).zip`.

### P0
- Risk Engine: invalid TickSize / PointValue / TickValue / risk distance now rejects instead of falling back.
- Position sizing: no more `return 1` fallback; invalid sizing returns 0 and rejects the signal.
- Risk validation: final stop bounds are rechecked after pip cap and net R:R is validated after execution costs.
- JSON export: invalid financial contracts are not exported; `risk_valid=true` is emitted only after validation; `position_size` can no longer silently become 1.
- Global signal sequence: sequence is unique across instrument instances.
- TCP bridge: shared server is reference-counted across indicator instances; one instrument can no longer stop the bridge used by another.
- MT5 receiver: strict source geometry validation and post-conversion geometry validation.
- MT5 receiver: broker `StopsLevel` / `FreezeLevel` validation.
- MT5 receiver: invalid broker `SYMBOL_POINT` is rejected instead of defaulted.

### P1
- Footprint validation changed from `one boolean proof = valid` to an evidence threshold of 0.30.
- MT5 JSON string parsing now scans escaped quotes/backslashes instead of using the first quote as terminator.
- Timestamp contract now contains UTC ISO-8601 plus `timestamp_epoch`; MT5 age validation uses UTC epoch.
- MT5 lot sizing no longer falls back to broker metadata or minimum lot when market metadata is invalid.
- Risk-based sizing refuses to round a lot upward to the broker minimum when doing so could exceed the requested risk budget.

## Validation performed
- Structural brace-count checks on modified C# files.
- Diff review against the original uploaded archive.
- Static checks for the removed financial fallbacks.
- Contract-level review of the NT8 → JSON → MT5 validation path.

## Important limitation
The uploaded project depends on NinjaTrader runtime assemblies and MetaTrader 5 terminal APIs. Those runtimes/compilers are not available in this execution environment, so a true NinjaTrader compile and MetaEditor compile cannot be claimed here.

## Required final validation in your environment
1. Compile the NinjaScript project in NinjaTrader 8.
2. Compile `AMCPro_MT5_Receiver.mq5` in MetaEditor.
3. Run Strategy Analyzer / replay tests.
4. Test multi-instrument TCP with NQ + MNQ + MGC + MCL.
5. Test malformed JSON, duplicate sequence, stale timestamp, invalid TickSize/TickValue, broker StopsLevel/FreezeLevel, and both BUY/SELL geometry.
6. Run SIM before any live deployment.
