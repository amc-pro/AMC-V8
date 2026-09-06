import pandas as pd

df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';')

print("=" * 80)
print("SIMULATION DE FILTRES QUALITE :")
print("=" * 80)

# Scénario 1: Tous les trades (Actuel)
tot_r = df['r_multiple'].sum()
wr = (len(df[df['r_multiple'] > 0]) / len(df)) * 100
print(f"1. TOUS TRADES (Actuel)       : {len(df):2d} trades | Win: {wr:4.1f}% | Net R: {tot_r:+6.2f} R")

# Scénario 2: Filtrer les RETEST_FVG faibles ou garder seulement FINISHED_AUCTION + DELTA_FLIP
df_of = df[df['setup'].isin(['FINISHED_AUCTION', 'DELTA_FLIP', 'CUM_DELTA_DIV'])]
tot_of = df_of['r_multiple'].sum()
wr_of = (len(df_of[df_of['r_multiple'] > 0]) / len(df_of)) * 100
print(f"2. SETUPS ORDER FLOW PURS    : {len(df_of):2d} trades | Win: {wr_of:4.1f}% | Net R: {tot_of:+6.2f} R")

# Scénario 3: Seuil Score >= 50 (Exclure la zone grise 45-50 sauf si Macro)
df_50 = df[df['score'] >= 50.0]
tot_50 = df_50['r_multiple'].sum()
wr_50 = (len(df_50[df_50['r_multiple'] > 0]) / len(df_50)) * 100
print(f"3. SCORE >= 50               : {len(df_50):2d} trades | Win: {wr_50:4.1f}% | Net R: {tot_50:+6.2f} R")

# Scénario 4: FINISHED_AUCTION ONLY
df_fa = df[df['setup'] == 'FINISHED_AUCTION']
tot_fa = df_fa['r_multiple'].sum()
wr_fa = (len(df_fa[df_fa['r_multiple'] > 0]) / len(df_fa)) * 100
print(f"4. FINISHED_AUCTION SEUL     : {len(df_fa):2d} trades | Win: {wr_fa:4.1f}% | Net R: {tot_fa:+6.2f} R")
