import pandas as pd
df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';', on_bad_lines='skip')
d24 = df[df['entry_time'].str.startswith('2026-08-24')]
print("=" * 110)
print(f"TRADES EMIS SUR LA JOURNEE DU 24/08 (TOTAL: {len(d24)})")
print("=" * 110)
print(d24.to_string())
