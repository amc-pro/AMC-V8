import pandas as pd
import numpy as np

# Load outcomes
df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';')

total_trades = len(df)
wins = df[df['outcome'].str.startswith('TARGET')]
losses = df[df['outcome'] == 'STOP']

win_count = len(wins)
loss_count = len(losses)
win_rate = (win_count / total_trades) * 100 if total_trades > 0 else 0

total_r = df['r_multiple'].sum()
gross_profit_r = wins['r_multiple'].sum()
gross_loss_r = abs(losses['r_multiple'].sum())
profit_factor = (gross_profit_r / gross_loss_r) if gross_loss_r > 0 else np.nan
expectancy_r = total_r / total_trades if total_trades > 0 else 0
avg_win_r = wins['r_multiple'].mean() if win_count > 0 else 0
avg_loss_r = losses['r_multiple'].mean() if loss_count > 0 else 0

print("=" * 80)
print(f"RAPPORT GLOBAL DE PERFORMANCE (SCALPING PRO)")
print("=" * 80)
print(f"Nombre total de trades : {total_trades}")
print(f"Trades Gagnants (TP)   : {win_count} ({win_rate:.1f}%)")
print(f"Trades Perdants (SL)   : {loss_count} ({100 - win_rate:.1f}%)")
print(f"Gain Brut (R)          : +{gross_profit_r:.2f} R")
print(f"Perte Brute (R)        : -{gross_loss_r:.2f} R")
print(f"R-Multiple Net Total   : {total_r:+.2f} R")
print(f"Profit Factor          : {profit_factor:.2f}")
print(f"Espérance / Trade      : {expectancy_r:+.3f} R")
print(f"Gain Moyen (Win)       : +{avg_win_r:.2f} R")
print(f"Perte Moyenne (Loss)   : {avg_loss_r:.2f} R")

# Breakdown by day
print("\n" + "-" * 80)
print("PERFORMANCE PAR JOUR")
print("-" * 80)
df['date'] = df['entry_time'].apply(lambda x: str(x).split(' ')[0])
for date, grp in df.groupby('date'):
    d_total = len(grp)
    d_wins = len(grp[grp['outcome'].str.startswith('TARGET')])
    d_losses = len(grp[grp['outcome'] == 'STOP'])
    d_wr = (d_wins / d_total) * 100 if d_total > 0 else 0
    d_r = grp['r_multiple'].sum()
    print(f"Date {date:10s} | Trades: {d_total:2d} | Win: {d_wins:2d} ({d_wr:4.1f}%) | Net R: {d_r:+6.2f} R")

# Breakdown by setup
print("\n" + "-" * 80)
print("PERFORMANCE PAR TYPE DE SETUP")
print("-" * 80)
for setup, grp in df.groupby('setup'):
    s_total = len(grp)
    s_wins = len(grp[grp['outcome'].str.startswith('TARGET')])
    s_losses = len(grp[grp['outcome'] == 'STOP'])
    s_wr = (s_wins / s_total) * 100 if s_total > 0 else 0
    s_r = grp['r_multiple'].sum()
    s_gp = grp[grp['outcome'].str.startswith('TARGET')]['r_multiple'].sum()
    s_gl = abs(grp[grp['outcome'] == 'STOP']['r_multiple'].sum())
    s_pf = (s_gp / s_gl) if s_gl > 0 else np.inf
    print(f"{setup:20s} | Trades: {s_total:2d} | Win: {s_wins:2d} ({s_wr:4.1f}%) | Net R: {s_r:+6.2f} R | PF: {s_pf:4.2f}")

# Breakdown by direction
print("\n" + "-" * 80)
print("PERFORMANCE PAR DIRECTION (LONG vs SHORT)")
print("-" * 80)
for side, grp in df.groupby('side'):
    sd_total = len(grp)
    sd_wins = len(grp[grp['outcome'].str.startswith('TARGET')])
    sd_losses = len(grp[grp['outcome'] == 'STOP'])
    sd_wr = (sd_wins / sd_total) * 100 if sd_total > 0 else 0
    sd_r = grp['r_multiple'].sum()
    print(f"{side:5s} | Trades: {sd_total:2d} | Win: {sd_wins:2d} ({sd_wr:4.1f}%) | Net R: {sd_r:+6.2f} R")

# Breakdown by score range
print("\n" + "-" * 80)
print("PERFORMANCE PAR TRANCHE DE SCORE")
print("-" * 80)
bins = [45, 50, 60, 70, 100]
labels = ['45-50 (Faible)', '50-60 (Moyen)', '60-70 (Fort)', '70+ (Très Fort)']
df['score_bin'] = pd.cut(df['score'], bins=bins, labels=labels, right=False)
for b, grp in df.groupby('score_bin', observed=False):
    b_total = len(grp)
    if b_total == 0: continue
    b_wins = len(grp[grp['outcome'].str.startswith('TARGET')])
    b_wr = (b_wins / b_total) * 100 if b_total > 0 else 0
    b_r = grp['r_multiple'].sum()
    print(f"{b:20s} | Trades: {b_total:2d} | Win: {b_wins:2d} ({b_wr:4.1f}%) | Net R: {b_r:+6.2f} R")
