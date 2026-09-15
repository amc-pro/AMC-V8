import pandas as pd
import numpy as np

# Load files
outcomes = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';')
outcomes['entry_time'] = pd.to_datetime(outcomes['entry_time'])
outcomes['exit_time'] = pd.to_datetime(outcomes['exit_time'])
outcomes['duration_min'] = (outcomes['exit_time'] - outcomes['entry_time']).dt.total_seconds() / 60.0
outcomes['date'] = outcomes['entry_time'].dt.date
outcomes['hour'] = outcomes['entry_time'].dt.hour
outcomes['is_win'] = outcomes['r_multiple'] > 0

journal = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper.csv', sep=';')
journal['time'] = pd.to_datetime(journal['time'])

merged = pd.merge(outcomes, journal, left_on=['entry_time', 'setup', 'side'], right_on=['time', 'setup', 'side'], how='left')

print("==================================================")
print("             1. GLOBAL PERFORMANCE SUMMARY        ")
print("==================================================")
total_trades = len(outcomes)
wins = (outcomes['r_multiple'] > 0).sum()
losses = (outcomes['r_multiple'] < 0).sum()
breakeven = (outcomes['r_multiple'] == 0).sum()
win_rate = (wins / total_trades) * 100
total_r = outcomes['r_multiple'].sum()
gross_profit_r = outcomes[outcomes['r_multiple'] > 0]['r_multiple'].sum()
gross_loss_r = abs(outcomes[outcomes['r_multiple'] < 0]['r_multiple'].sum())
profit_factor = gross_profit_r / gross_loss_r if gross_loss_r > 0 else np.nan
expectancy_r = total_r / total_trades
avg_win_r = outcomes[outcomes['r_multiple'] > 0]['r_multiple'].mean()
avg_loss_r = abs(outcomes[outcomes['r_multiple'] < 0]['r_multiple'].mean())

print(f"Total Trades Logged       : {total_trades}")
print(f"Wins                      : {wins} ({win_rate:.2f}%)")
print(f"Losses                    : {losses} ({(losses/total_trades)*100:.2f}%)")
print(f"BE / Scratch / SessionEnd : {breakeven} ({(breakeven/total_trades)*100:.2f}%)")
print(f"Gross Profit (R)          : +{gross_profit_r:.3f} R")
print(f"Gross Loss (R)            : -{gross_loss_r:.3f} R")
print(f"Net Total R               : +{total_r:.3f} R")
print(f"Profit Factor (PF)        : {profit_factor:.2f}")
print(f"Expectancy / Trade        : +{expectancy_r:.3f} R")
print(f"Avg Win                   : +{avg_win_r:.3f} R")
print(f"Avg Loss                  : -{avg_loss_r:.3f} R")
print(f"Payoff Ratio (W/L)        : {avg_win_r/avg_loss_r:.2f}")
print(f"Avg Trade Duration        : {outcomes['duration_min'].mean():.1f} min")
print(f"  - Avg Win Duration      : {outcomes[outcomes['is_win']]['duration_min'].mean():.1f} min")
print(f"  - Avg Loss Duration     : {outcomes[~outcomes['is_win']]['duration_min'].mean():.1f} min")

print("\n==================================================")
print("             2. DRAWDOWN & EQUITY CURVE           ")
print("==================================================")
outcomes['cum_r'] = outcomes['r_multiple'].cumsum()
outcomes['peak_r'] = outcomes['cum_r'].cummax()
outcomes['drawdown_r'] = outcomes['cum_r'] - outcomes['peak_r']
max_dd = outcomes['drawdown_r'].min()
print(f"Max Drawdown (R)          : {max_dd:.3f} R")
print(f"Max Peak Equity (R)       : +{outcomes['peak_r'].max():.3f} R")
print(f"Current Equity (R)        : +{outcomes['cum_r'].iloc[-1]:.3f} R")

# Consecutive streaks
streaks = []
cur_streak = 0
for r in outcomes['r_multiple']:
    if r > 0:
        if cur_streak > 0:
            cur_streak += 1
        else:
            cur_streak = 1
    elif r < 0:
        if cur_streak < 0:
            cur_streak -= 1
        else:
            cur_streak = -1
    else:
        cur_streak = 0
    streaks.append(cur_streak)
print(f"Max Consecutive Wins      : {max(streaks)}")
print(f"Max Consecutive Losses    : {abs(min(streaks))}")

print("\n==================================================")
print("             3. BREAKDOWN BY DATE                 ")
print("==================================================")
for d, g in outcomes.groupby('date'):
    w = (g['r_multiple'] > 0).sum()
    l = (g['r_multiple'] < 0).sum()
    be = (g['r_multiple'] == 0).sum()
    tot_r = g['r_multiple'].sum()
    wr = (w / len(g)) * 100
    print(f"Date {d} | Trades: {len(g):2d} | W: {w:2d} | L: {l:2d} | BE: {be:1d} | WR: {wr:5.1f}% | Net R: {tot_r:+6.2f} R")

print("\n==================================================")
print("             4. BREAKDOWN BY SETUP                ")
print("==================================================")
setup_stats = outcomes.groupby('setup').agg(
    Trades=('r_multiple', 'count'),
    Wins=('is_win', 'sum'),
    Losses=('r_multiple', lambda x: (x < 0).sum()),
    WinRate=('is_win', lambda x: x.mean() * 100),
    Total_R=('r_multiple', 'sum'),
    Avg_R=('r_multiple', 'mean'),
    PF=('r_multiple', lambda x: (x[x > 0].sum() / abs(x[x < 0].sum())) if abs(x[x < 0].sum()) > 0 else np.nan)
).sort_values(by='Total_R', ascending=False)
print(setup_stats.to_string())

print("\n==================================================")
print("             5. DELTA_FLIP DEEP DIVE              ")
print("==================================================")
df_trades = merged[merged['setup'] == 'DELTA_FLIP']
for idx, r in df_trades.iterrows():
    status = 'WIN' if r['r_multiple'] > 0 else ('LOSS' if r['r_multiple'] < 0 else 'BE')
    print(f"{r['entry_time']} | {r['side']:5s} | Score:{r['score_x']:4.1f} | N1:{r['N1']:4.1f} N2:{r['N2']:4.1f} N3:{r['N3']:4.1f} N4:{r['N4']:4.1f} | DayType:{str(r['daytype']):10s} | HTF:{r['htf_aligned']} | {status:4s} ({r['r_multiple']:+5.2f} R)")

print("\n==================================================")
print("             6. FINISHED_AUCTION DEEP DIVE        ")
print("==================================================")
fa_trades = merged[merged['setup'] == 'FINISHED_AUCTION']
print(fa_trades.groupby(['side', 'grade_x']).agg(
    Trades=('r_multiple', 'count'),
    Wins=('is_win', 'sum'),
    Losses=('r_multiple', lambda x: (x < 0).sum()),
    WinRate=('is_win', lambda x: x.mean() * 100),
    Total_R=('r_multiple', 'sum')
))

print("\n==================================================")
print("             7. SIMULATION: WHAT IF DELTA_FLIP EXCLUDED OR FILTERED?")
print("==================================================")
without_df = outcomes[outcomes['setup'] != 'DELTA_FLIP']
gp_no_df = without_df[without_df['r_multiple'] > 0]['r_multiple'].sum()
gl_no_df = abs(without_df[without_df['r_multiple'] < 0]['r_multiple'].sum())
pf_no_df = gp_no_df / gl_no_df
print(f"WITHOUT DELTA_FLIP:")
print(f"  Trades: {len(without_df)} | Wins: {(without_df['r_multiple']>0).sum()} ({(without_df['r_multiple']>0).mean()*100:.1f}%) | Net R: {without_df['r_multiple'].sum():+.3f} R | PF: {pf_no_df:.2f}")

# What if only DELTA_FLIP with score >= 55?
df_filtered = outcomes[(outcomes['setup'] != 'DELTA_FLIP') | (outcomes['score'] >= 55)]
gp_filt = df_filtered[df_filtered['r_multiple'] > 0]['r_multiple'].sum()
gl_filt = abs(df_filtered[df_filtered['r_multiple'] < 0]['r_multiple'].sum())
pf_filt = gp_filt / gl_filt
print(f"\nWITH DELTA_FLIP SCORE >= 55:")
print(f"  Trades: {len(df_filtered)} | Wins: {(df_filtered['r_multiple']>0).sum()} ({(df_filtered['r_multiple']>0).mean()*100:.1f}%) | Net R: {df_filtered['r_multiple'].sum():+.3f} R | PF: {pf_filt:.2f}")

# What if only DELTA_FLIP with score >= 60?
df_filtered60 = outcomes[(outcomes['setup'] != 'DELTA_FLIP') | (outcomes['score'] >= 70)]
gp_filt60 = df_filtered60[df_filtered60['r_multiple'] > 0]['r_multiple'].sum()
gl_filt60 = abs(df_filtered60[df_filtered60['r_multiple'] < 0]['r_multiple'].sum())
pf_filt60 = gp_filt60 / gl_filt60
print(f"\nWITH DELTA_FLIP SCORE >= 70:")
print(f"  Trades: {len(df_filtered60)} | Wins: {(df_filtered60['r_multiple']>0).sum()} ({(df_filtered60['r_multiple']>0).mean()*100:.1f}%) | Net R: {df_filtered60['r_multiple'].sum():+.3f} R | PF: {pf_filt60:.2f}")
