import pandas as pd

df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper.csv', sep=';', on_bad_lines='skip')
outcomes = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';', on_bad_lines='skip')

print("=" * 100)
print(f"SHADOW JOURNAL ANALYSIS - TOTAL ENTRIES: {len(df)} | TOTAL OUTCOMES: {len(outcomes)}")
print("=" * 100)

print("\n--- TOUS LES TRADES ÉMIS DANS OUTCOMES ---")
print(outcomes.to_string())

print("\n--- TOUS LES SIGNAUX ENTRE 16:40 ET 18:30 LE 24/08/2026 ---")
d24 = df[(df['time'] >= '2026-08-24 16:40:00') & (df['time'] <= '2026-08-24 18:30:00')]
for idx, r in d24.iterrows():
    print(f"[{r['time']}] {r['setup']:20s} {r['side']:5s} | Score: {r['score']:5.1f} | Grade: {r['grade']:10s} | Gated: {r['gated']} ({r['gate_failed']})")
    print(f"  N1={r['N1']}/30  N2={r['N2']}/30  N3={r['N3']}/30  N4={r['N4']}/15  Pen={r['penalty']}  RR={r['rr']}")
    print(f"  Detail: {r['detail']}")
    print("-" * 80)

print("\n--- TOUS LES SIGNAUX ÉMIS (GRADE FORT / TRES FORT) SUR LA SESSION DU 24/08 ---")
d24_all = df[(df['time'] >= '2026-08-24') & (df['time'] < '2026-08-25')]
emitted_24 = d24_all[d24_all['grade'].isin(['FORT', 'TRES_FORT'])]
for idx, r in emitted_24.iterrows():
    print(f"[{r['time']}] {r['setup']:20s} {r['side']:5s} | Score: {r['score']:5.1f} | Grade: {r['grade']:10s}")
    print(f"  Detail: {r['detail']}")
    print("-" * 80)
