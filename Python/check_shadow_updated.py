import pandas as pd
import sys

print("=" * 110)
print("1. VERIFICATION DU FICHIER DES OUTCOMES (TRADES EMIS ET RESULTATS)")
print("=" * 110)
try:
    df_outcomes = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';', on_bad_lines='skip')
    print(f"Total des trades emis: {len(df_outcomes)}")
    print(df_outcomes.to_string())
except Exception as e:
    print("Erreur lecture outcomes:", e)

print("\n" + "=" * 110)
print("2. ANALYSE DE LA SESSION DU 24/08 DANS LE JOURNAL SNIPER (16H00 A 19H00)")
print("=" * 110)
try:
    df_sniper = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper.csv', sep=';', on_bad_lines='skip')
    d24 = df_sniper[(df_sniper['time'] >= '2026-08-24 16:30:00') & (df_sniper['time'] <= '2026-08-24 18:30:00')]
    print(f"Total evaluations 16h30-18h30 le 24/08: {len(d24)}")
    
    for idx, r in d24.iterrows():
        side_label = "LONG " if r['side'] == 'LONG' else "SHORT"
        grade_label = r['grade']
        print(f"[{r['time']}] {side_label} | {r['setup']:20s} | Score: {r['score']:5.1f} | Grade: {grade_label:10s} | Gated: {r['gated']} ({r['gate_failed']})")
        print(f"   Scores: N1={r['N1']:4.1f} N2={r['N2']:4.1f} N3={r['N3']:4.1f} N4={r['N4']:4.1f} | Pen={r['penalty']:4.1f} | RR={r['rr']}")
        print(f"   Detail: {r['detail']}")
        print("-" * 100)
except Exception as e:
    print("Erreur lecture sniper journal:", e)
