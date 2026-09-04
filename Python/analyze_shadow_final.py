import pandas as pd
import os

print("=" * 110)
print("1. VERIFICATION DU FICHIER DES OUTCOMES (shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv)")
print("=" * 110)

outcomes_path = 'shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv'
if os.path.exists(outcomes_path):
    print(f"Taille: {os.path.getsize(outcomes_path)} bytes, mtime: {os.path.getmtime(outcomes_path)}")
    try:
        df_outcomes = pd.read_csv(outcomes_path, sep=';', on_bad_lines='skip')
        d24_outcomes = df_outcomes[df_outcomes['entry_time'].str.startswith('2026-08-24')]
        print(f"Total des trades émis le 24/08: {len(d24_outcomes)}")
        print(d24_outcomes.to_string())
    except Exception as e:
        print("Erreur lecture outcomes:", e)

print("\n" + "=" * 110)
print("2. ANALYSE DU JOURNAL SNIPER SUR LA SESSION DU 24/08 (16H30 A 18H30)")
print("=" * 110)

journal_path = 'shadow/AuctionMarketCorePro_journal_sniper.csv'
if os.path.exists(journal_path):
    try:
        df_sniper = pd.read_csv(journal_path, sep=';', on_bad_lines='skip')
        d24 = df_sniper[(df_sniper['time'] >= '2026-08-24 16:30:00') & (df_sniper['time'] <= '2026-08-24 18:30:00')]
        print(f"Total évaluations 16h30-18h30 le 24/08: {len(d24)}\n")
        
        for idx, r in d24.iterrows():
            side_label = "LONG " if r['side'] == 'LONG' else "SHORT"
            grade_label = str(r['grade'])
            print(f"[{r['time']}] {side_label} | {r['setup']:20s} | Score: {r['score']:5.1f} | Grade: {grade_label:10s} | Gated: {r['gated']} ({r['gate_failed']})")
            print(f"   Scores: N1={r['N1']:4.1f} N2={r['N2']:4.1f} N3={r['N3']:4.1f} N4={r['N4']:4.1f} | Pen={r['penalty']:4.1f} | RR={r['rr']}")
            print(f"   Detail: {r['detail']}")
            print("-" * 100)
    except Exception as e:
        print("Erreur lecture journal:", e)
