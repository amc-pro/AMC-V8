import pandas as pd

df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';')
df_df = df[df['setup'] == 'DELTA_FLIP']
print(df_df[['entry_time', 'side', 'score', 'outcome', 'r_multiple']].to_string())
