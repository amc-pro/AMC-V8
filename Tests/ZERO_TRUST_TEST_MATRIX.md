# Zero-Trust Contract Test Matrix

These tests are intentionally deterministic and should be executed in NinjaTrader/MetaEditor environments.

| Test | Input | Expected |
|---|---|---|
| TickSize invalid | TickSize=0/NaN/Infinity | REJECT, no export |
| PointValue invalid | PointValue<=0/NaN/Infinity | REJECT, position size 0 |
| Risk distance invalid | <=0/NaN/Infinity | REJECT |
| Position sizing too small | calculated qty < 1 | REJECT |
| JSON sizing | lastPositionSize=0 | REJECT, never `position_size=1` |
| R:R net | R:R after execution cost < MinRiskReward | REJECT |
| BUY geometry | SL < Entry < TP1 <= TP2 | ACCEPT |
| SELL geometry | TP2 <= TP1 < Entry < SL | ACCEPT |
| Converted BUY | final SL/TP violate geometry | REJECT |
| Converted SELL | final SL/TP violate geometry | REJECT |
| Broker constraints | distance < max(StopsLevel, FreezeLevel) | REJECT |
| Timestamp | missing/invalid epoch | REJECT |
| Timestamp stale | age > MaxSignalAgeSec | REJECT |
| JSON escaping | signal contains escaped quote | Parse correctly |
| Duplicate sequence | same sequence twice | Execute at most once |
| Multi-instance TCP | NQ + MNQ + MGC | One shared bridge; no instance stops it |
| Footprint | one weak proof only | REJECT |
| Footprint | evidence >= 0.30 | ACCEPT |

## Production gate

A live deployment should not be approved until:
- NinjaTrader compilation passes with zero errors.
- MetaEditor compilation passes with zero errors.
- The tests above pass in SIM/replay.
- No duplicate execution occurs through TCP + file fallback.
- Futures → CFD conversion is validated per broker and instrument.
