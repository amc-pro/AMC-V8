import pandas as pd

df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper.csv', sep=';', on_bad_lines='skip')

print("=" * 100)
print("ANALYSE DES SIGNAUX LONG ENTRE 17H00 ET 19H00 LE 24/08/2026 (POST-MISE À JOUR)")
print("=" * 100)

d24_longs = df[(df['time'] >= '2026-08-24 17:00:00') & (df['time'] <= '2026-08-24 19:00:00') & (df['side'] == 'LONG')]
print(f"Total signaux LONG trouvés: {len(d24_longs)}")

for idx, r in d24_longs.iterrows():
    print(f"\n[{r['time']}] {r['setup']} LONG | Score: {r['score']} | Grade: {r['grade']} | Gated: {r['gated']} ({r['gate_failed']})")
    print(f"  N1={r['N1']}/30  N2={r['N2']}/30  N3={r['N3']}/30  N4={r['N4']}/15  Pen={r['penalty']}  RR={r['rr']}")
    print(f"  DETAIL:")
    for part in str(r['detail']).split(' | '):
        print(f"    - {part}")

print("\n" + "=" * 100)
print("TRADES ÉMIS LE 24/08 ENTRE 16H30 ET 19H00 DANS OUTCOMES")
print("=" * 100)
outcomes = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';', on_bad_lines='skip')
d24_outcomes = outcomes[(outcomes['entry_time'] >= '2026-08-24 16:30:00') & (outcomes['entry_time'] <= '2026-08-24 19:00:00')]
print(d24_outcomes.to_string())
