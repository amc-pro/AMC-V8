import pandas as pd
import glob, os

print("=" * 110)
print("RECHERCHE DES PRIX DU MNQ SUR LA JOURNEE DU 24/08/2026")
print("=" * 110)

# Check CSV historical data if available
hist_files = glob.glob('csv/*.csv') + glob.glob('historical*/*.csv') + glob.glob('tests_and_data/*.csv')
print("Fichiers historiques trouves:", hist_files)

# Check shadow sniper journal
df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper.csv', sep=';', on_bad_lines='skip')
d24 = df[df['time'].str.startswith('2026-08-24')]
print(f"Total entrees dans journal sniper le 24/08: {len(d24)}")

print("\n--- EXTRAIT DES SIGNAUX ET PRIX LE 24/08 (16H00 A 18H30) ---")
for idx, r in d24.iterrows():
    if '16:30:00' <= r['time'][11:] <= '18:30:00':
        print(f"[{r['time']}] {r['side']:5s} | {r['setup']:20s} | Entry={r['entry']:9.2f} | Stop={r['stop']:9.2f} | Target={r['target1']:9.2f}")
