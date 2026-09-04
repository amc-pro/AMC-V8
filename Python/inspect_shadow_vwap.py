import pandas as pd

df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper.csv', sep=';', on_bad_lines='skip')

print(f"Total entries: {len(df)}")
recent = df[df['time'] >= '2026-08-25'].copy()
print(f"Entries for 2026-08-25: {len(recent)}")

print("\n--- EMITTED TRADES (GRADE FORT / TRES FORT) ON 2026-08-25 ---")
emitted = recent[recent['grade'].isin(['FORT', 'TRES_FORT'])]
for idx, r in emitted.iterrows():
    print(f"Time: {r['time']} | Setup: {r['setup']} {r['side']} | Score: {r['score']} | Grade: {r['grade']} | Entry: {r['entry']} | Stop: {r['stop']}")
    print(f"  N1: {r['N1']} | N2: {r['N2']} | N3: {r['N3']} | N4: {r['N4']} | Pen: {r['penalty']}")
    print(f"  Detail: {r['detail']}")
    print("-" * 80)

print("\n--- ALL ENTRIES ON 2026-08-25 WITH HIGH N2 (N2 >= 15) ---")
high_n2 = recent[recent['N2'] >= 15.0]
for idx, r in high_n2.iterrows():
    print(f"Time: {r['time']} | Setup: {r['setup']} {r['side']} | Score: {r['score']} | Gated: {r['gated']} ({r['gate_failed']}) | N2: {r['N2']}")
    print(f"  Detail: {r['detail']}")
    print("-" * 80)

outcomes = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';', on_bad_lines='skip')
print("\n--- ALL OUTCOMES ON 2026-08-25 ---")
print(outcomes[outcomes['entry_time'] >= '2026-08-25'].to_string())
