import os
import pandas as pd
import numpy as np

def run_deep_analysis():
    csv_path = os.path.expanduser("~/Documents/NinjaTrader 8/shadow/swing_trades.csv")
    if not os.path.exists(csv_path):
        print("ERROR: File not found:", csv_path)
        return

    df = pd.read_csv(csv_path)
    print("=" * 80)
    print("SWING V3 VALIDATION TEST DEEP ANALYSIS")
    print("=" * 80)
    print(f"Total Rows: {len(df):,}")
    print("Status counts:\n", df['Status'].value_counts())

    closed = df[df['Status'] == 'CLOSED'].copy()
    closed['EntryTimeUtc'] = pd.to_datetime(closed['EntryTimeUtc'])
    closed['ExitTimeUtc'] = pd.to_datetime(closed['ExitTimeUtc'])
    closed['DurationMinutes'] = (closed['ExitTimeUtc'] - closed['EntryTimeUtc']).dt.total_seconds() / 60.0
    closed = closed.sort_values('EntryTimeUtc').reset_index(drop=True)

    # 1. Global Metrics
    total_trades = len(closed)
    wins = closed[closed['RealizedR'] > 0]
    losses = closed[closed['RealizedR'] < 0]
    bes = closed[closed['RealizedR'] == 0]

    n_wins = len(wins)
    n_losses = len(losses)
    n_bes = len(bes)
    win_rate = (n_wins / total_trades) * 100 if total_trades > 0 else 0

    net_usd = closed['RealizedUSD'].sum()
    gross_profit = wins['RealizedUSD'].sum()
    gross_loss = abs(losses['RealizedUSD'].sum())
    pf_usd = gross_profit / gross_loss if gross_loss > 0 else 999.0

    net_r = closed['RealizedR'].sum()
    gross_win_r = wins['RealizedR'].sum()
    gross_loss_r = abs(losses['RealizedR'].sum())
    pf_r = gross_win_r / gross_loss_r if gross_loss_r > 0 else 999.0

    avg_win_usd = wins['RealizedUSD'].mean() if n_wins > 0 else 0
    avg_loss_usd = abs(losses['RealizedUSD'].mean()) if n_losses > 0 else 0
    payoff_usd = avg_win_usd / avg_loss_usd if avg_loss_usd > 0 else 0

    avg_win_r = wins['RealizedR'].mean() if n_wins > 0 else 0
    avg_loss_r = abs(losses['RealizedR'].mean()) if n_losses > 0 else 0
    payoff_r = avg_win_r / avg_loss_r if avg_loss_r > 0 else 0

    exp_usd = net_usd / total_trades if total_trades > 0 else 0
    exp_r = net_r / total_trades if total_trades > 0 else 0

    # Drawdown
    closed['CumUSD'] = closed['RealizedUSD'].cumsum()
    closed['PeakUSD'] = closed['CumUSD'].cummax()
    closed['DrawdownUSD'] = closed['CumUSD'] - closed['PeakUSD']
    max_dd_usd = closed['DrawdownUSD'].min()

    closed['CumR'] = closed['RealizedR'].cumsum()
    closed['PeakR'] = closed['CumR'].cummax()
    closed['DrawdownR'] = closed['CumR'] - closed['PeakR']
    max_dd_r = closed['DrawdownR'].min()

    # Consecutive Streaks
    streaks = []
    current_streak = 0
    current_type = None
    for r in closed['RealizedR']:
        t = 'W' if r > 0 else ('L' if r < 0 else 'BE')
        if t == current_type:
            current_streak += 1
        else:
            if current_type is not None:
                streaks.append((current_type, current_streak))
            current_type = t
            current_streak = 1
    streaks.append((current_type, current_streak))
    max_win_streak = max([s[1] for s in streaks if s[0] == 'W'], default=0)
    max_loss_streak = max([s[1] for s in streaks if s[0] == 'L'], default=0)

    print("\n--- GLOBAL METRICS ---")
    print(f"Period: {closed['EntryTimeUtc'].min()} to {closed['ExitTimeUtc'].max()}")
    print(f"Closed Trades: {total_trades} | Wins: {n_wins} ({win_rate:.2f}%) | Losses: {n_losses} | BE: {n_bes}")
    print(f"Net Realized PnL: ${net_usd:,.2f} USD | Net R: {net_r:+.2f} R")
    print(f"Profit Factor USD: {pf_usd:.2f} | Profit Factor R: {pf_r:.2f}")
    print(f"Gross Profit: ${gross_profit:,.2f} | Gross Loss: ${gross_loss:,.2f}")
    print(f"Avg Win: ${avg_win_usd:,.2f} ({avg_win_r:+.2f} R) | Avg Loss: ${avg_loss_usd:,.2f} ({avg_loss_r:+.2f} R)")
    print(f"Payoff Ratio (Win/Loss): {payoff_usd:.2f} (USD), {payoff_r:.2f} (R)")
    print(f"Expectancy: ${exp_usd:,.2f} / trade | {exp_r:+.3f} R / trade")
    print(f"Max Drawdown: ${max_dd_usd:,.2f} USD | {max_dd_r:+.2f} R")
    print(f"Max Win Streak: {max_win_streak} | Max Loss Streak: {max_loss_streak}")

    # 2. BY SYMBOL
    print("\n--- PERFORMANCE BY SYMBOL ---")
    sym_summary = []
    for sym, sg in closed.groupby('Symbol'):
        s_tot = len(sg)
        s_wins = len(sg[sg['RealizedR'] > 0])
        s_wr = (s_wins / s_tot) * 100
        s_usd = sg['RealizedUSD'].sum()
        s_r = sg['RealizedR'].sum()
        s_gp = sg[sg['RealizedUSD'] > 0]['RealizedUSD'].sum()
        s_gl = abs(sg[sg['RealizedUSD'] < 0]['RealizedUSD'].sum())
        s_pf = s_gp / s_gl if s_gl > 0 else 999.0
        
        # Drawdown per symbol
        sg_sorted = sg.sort_values('EntryTimeUtc').copy()
        sg_sorted['CumUSD'] = sg_sorted['RealizedUSD'].cumsum()
        sg_dd = (sg_sorted['CumUSD'] - sg_sorted['CumUSD'].cummax()).min()
        
        sym_summary.append({
            'Symbol': sym,
            'Trades': s_tot,
            'Wins': s_wins,
            'Losses': len(sg[sg['RealizedR'] < 0]),
            'WinRate': s_wr,
            'NetUSD': s_usd,
            'NetR': s_r,
            'GrossProfit': s_gp,
            'GrossLoss': s_gl,
            'ProfitFactor': s_pf,
            'MaxDD_USD': sg_dd
        })
    df_sym = pd.DataFrame(sym_summary).sort_values('NetUSD', ascending=False)
    print(df_sym.to_string(index=False))

    # 3. BY SETUP TYPE
    print("\n--- PERFORMANCE BY SETUP TYPE ---")
    setup_summary = []
    for st, sg in closed.groupby('SetupType'):
        s_tot = len(sg)
        s_wins = len(sg[sg['RealizedR'] > 0])
        s_wr = (s_wins / s_tot) * 100
        s_usd = sg['RealizedUSD'].sum()
        s_r = sg['RealizedR'].sum()
        s_gp = sg[sg['RealizedUSD'] > 0]['RealizedUSD'].sum()
        s_gl = abs(sg[sg['RealizedUSD'] < 0]['RealizedUSD'].sum())
        s_pf = s_gp / s_gl if s_gl > 0 else 999.0
        setup_summary.append({
            'SetupType': st,
            'Trades': s_tot,
            'Wins': s_wins,
            'WinRate': s_wr,
            'NetUSD': s_usd,
            'NetR': s_r,
            'PF': s_pf,
            'ExpUSD': s_usd / s_tot,
            'ExpR': s_r / s_tot
        })
    df_setup = pd.DataFrame(setup_summary).sort_values('NetUSD', ascending=False)
    print(df_setup.to_string(index=False))

    # 4. CROSS SYMBOL x SETUP
    print("\n--- CROSS SYMBOL x SETUP (Net USD & Win Rate) ---")
    pivot_usd = closed.pivot_table(index='SetupType', columns='Symbol', values='RealizedUSD', aggfunc='sum', fill_value=0)
    print("Net Realized USD:")
    print(pivot_usd.to_string())

    pivot_r = closed.pivot_table(index='SetupType', columns='Symbol', values='RealizedR', aggfunc='sum', fill_value=0)
    print("\nNet Realized R:")
    print(pivot_r.to_string())

    pivot_count = closed.pivot_table(index='SetupType', columns='Symbol', values='RealizedR', aggfunc='count', fill_value=0)
    print("\nTrade Count:")
    print(pivot_count.to_string())

    # 5. BY DIRECTION
    print("\n--- PERFORMANCE BY DIRECTION ---")
    for d, dg in closed.groupby('Direction'):
        d_tot = len(dg)
        d_wins = len(dg[dg['RealizedR'] > 0])
        d_wr = (d_wins / d_tot) * 100
        d_usd = dg['RealizedUSD'].sum()
        d_r = dg['RealizedR'].sum()
        d_gp = dg[dg['RealizedUSD'] > 0]['RealizedUSD'].sum()
        d_gl = abs(dg[dg['RealizedUSD'] < 0]['RealizedUSD'].sum())
        d_pf = d_gp / d_gl if d_gl > 0 else 999.0
        print(f"Direction {d:5s}: Trades={d_tot:4d} | WinRate={d_wr:5.1f}% | Net USD=${d_usd:+10,.2f} | Net R={d_r:+6.2f}R | PF={d_pf:4.2f}")

    # 6. BY EXIT REASON
    print("\n--- EXIT REASONS BREAKDOWN ---")
    exit_summary = []
    for er, eg in closed.groupby('ExitReason'):
        e_tot = len(eg)
        e_usd = eg['RealizedUSD'].sum()
        e_r = eg['RealizedR'].sum()
        e_wins = len(eg[eg['RealizedR'] > 0])
        e_wr = (e_wins / e_tot) * 100
        avg_dur = eg['DurationMinutes'].mean()
        exit_summary.append({
            'ExitReason': er,
            'Trades': e_tot,
            'PctOfTotal': (e_tot / total_trades) * 100,
            'WinRate': e_wr,
            'NetUSD': e_usd,
            'NetR': e_r,
            'AvgDurationMin': avg_dur
        })
    df_exit = pd.DataFrame(exit_summary).sort_values('Trades', ascending=False)
    print(df_exit.to_string(index=False))

    # 7. MONTHLY BREAKDOWN
    print("\n--- MONTHLY BREAKDOWN ---")
    closed['Month'] = closed['EntryTimeUtc'].dt.to_period('M')
    month_summary = []
    for m, mg in closed.groupby('Month'):
        m_tot = len(mg)
        m_wins = len(mg[mg['RealizedR'] > 0])
        m_wr = (m_wins / m_tot) * 100
        m_usd = mg['RealizedUSD'].sum()
        m_r = mg['RealizedR'].sum()
        m_gp = mg[mg['RealizedUSD'] > 0]['RealizedUSD'].sum()
        m_gl = abs(mg[mg['RealizedUSD'] < 0]['RealizedUSD'].sum())
        m_pf = m_gp / m_gl if m_gl > 0 else 999.0
        month_summary.append({
            'Month': str(m),
            'Trades': m_tot,
            'WinRate': m_wr,
            'NetUSD': m_usd,
            'NetR': m_r,
            'ProfitFactor': m_pf
        })
    df_month = pd.DataFrame(month_summary)
    print(df_month.to_string(index=False))

if __name__ == '__main__':
    run_deep_analysis()
