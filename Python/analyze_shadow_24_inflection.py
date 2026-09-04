import pandas as pd

df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper.csv', sep=';', on_bad_lines='skip')
d24 = df[(df['time'] >= '2026-08-24 16:40:00') & (df['time'] <= '2026-08-24 18:25:00')]

print("=" * 110)
print(f"ANALYSE COMPLÈTE DE LA FENÊTRE 16H40 - 18H25 LE 24/08 ({len(d24)} ÉVÉNEMENTS)")
print("=" * 110)

for idx, r in d24.iterrows():
    side_icon = "LONG " if r['side'] == 'LONG' else "SHORT"
    grade_icon = "FORT" if r['grade'] in ['FORT', 'TRES_FORT'] else "C   "
    print(f"[{r['time']}] {side_icon} | {r['setup']:20s} | Score: {r['score']:5.1f} | {grade_icon} | Gated: {r['gated']} ({r['gate_failed']})")
    print(f"   Scores: N1={r['N1']:4.1f} N2={r['N2']:4.1f} N3={r['N3']:4.1f} N4={r['N4']:4.1f} | Pen={r['penalty']:4.1f} | RR={r['rr']}")
    print(f"   Detail: {r['detail']}")
    print("-" * 100)
