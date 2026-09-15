import os
import csv
import sys
import argparse
import pandas as pd
from datetime import datetime

def analyze_dataset(df_subset, symbol_name):
    print(f"\n==========================================================================")
    print(f"       ANALYSE DE PERFORMANCE SHADOW SWING PRO - {symbol_name.upper()}     ")
    print(f"==========================================================================")
    
    closed = df_subset[df_subset['Status'] == 'CLOSED'].copy()
    total_trades = len(closed)
    print(f"Total signaux bruts émis : {len(df_subset)}")
    print(f"Trades clôturés évalués : {total_trades}")
    
    if total_trades == 0:
        print(f"Aucun trade clôturé pour {symbol_name}.")
        return None

    wins = closed[closed['RealizedR'] > 0]
    losses = closed[closed['RealizedR'] < 0]
    breakevens = closed[closed['RealizedR'] == 0]
    
    win_rate = (len(wins) / total_trades) * 100.0
    total_r = closed['RealizedR'].sum()
    total_usd = closed['RealizedUSD'].sum()
    gross_profit = wins['RealizedUSD'].sum()
    gross_loss = abs(losses['RealizedUSD'].sum())
    profit_factor = (gross_profit / gross_loss) if gross_loss > 0 else (999.0 if gross_profit > 0 else 0.0)
    avg_trade_usd = total_usd / total_trades
    avg_win_usd = wins['RealizedUSD'].mean() if len(wins) > 0 else 0.0
    avg_loss_usd = losses['RealizedUSD'].mean() if len(losses) > 0 else 0.0

    print(f"\n--- MÉTRIQUES GLOBALES ({symbol_name}) ---")
    print(f"  * Win Rate         : {win_rate:.2f}% ({len(wins)}W / {len(losses)}L / {len(breakevens)}BE)")
    print(f"  * R-Multiple Total : {total_r:+.2f} R")
    print(f"  * PnL Réalisé Net  : ${total_usd:+,.2f}")
    print(f"  * Profit Factor    : {profit_factor:.2f}")
    print(f"  * Gain Moyen / Win : ${avg_win_usd:,.2f}")
    print(f"  * Perte Moyenne    : ${avg_loss_usd:,.2f}")
    print(f"  * Espérance / Trade: ${avg_trade_usd:+,.2f}")

    # Setups breakdown
    print(f"\n--- PERFORMANCE PAR SETUP ---")
    setup_stats = []
    for setup, grp in closed.groupby('SetupType'):
        s_wins = grp[grp['RealizedR'] > 0]
        s_losses = grp[grp['RealizedR'] < 0]
        s_be = grp[grp['RealizedR'] == 0]
        s_wr = (len(s_wins) / len(grp)) * 100.0 if len(grp) > 0 else 0.0
        s_r = grp['RealizedR'].sum()
        s_usd = grp['RealizedUSD'].sum()
        s_gp = s_wins['RealizedUSD'].sum()
        s_gl = abs(s_losses['RealizedUSD'].sum())
        s_pf = (s_gp / s_gl) if s_gl > 0 else (999.0 if s_gp > 0 else 0.0)

        setup_stats.append({
            'Famille': setup,
            'Trades': len(grp),
            'Wins': len(s_wins),
            'Losses': len(s_losses),
            'Timeouts': len(s_be),
            'WinRate': s_wr,
            'SumR': s_r,
            'ProfitFactor': s_pf,
            'RealizedUSD': s_usd
        })
        print(f"  [{setup:20s}] {len(grp):3d} trades | WR: {s_wr:5.1f}% | R: {s_r:+6.1f}R | PnL: ${s_usd:+10,.2f} | PF: {s_pf:4.2f}")

    # Tier breakdown
    print(f"\n--- PERFORMANCE PAR TIER ---")
    for tier, grp in closed.groupby('Tier'):
        t_wins = grp[grp['RealizedR'] > 0]
        t_wr = (len(t_wins) / len(grp)) * 100.0 if len(grp) > 0 else 0.0
        t_r = grp['RealizedR'].sum()
        t_usd = grp['RealizedUSD'].sum()
        print(f"  [{tier:12s}] {len(grp):3d} trades | WR: {t_wr:5.1f}% | R: {t_r:+6.1f}R | PnL: ${t_usd:+10,.2f}")

    # Export des stats spécifiques de l'instrument
    stats_out = f"c:/AMC-Pro/AMC-V8/shadow/SWING/{symbol_name}/AuctionMarketCorePro_journal_stats_{symbol_name}.csv"
    os.makedirs(os.path.dirname(stats_out), exist_ok=True)
    with open(stats_out, "w", encoding="utf-8", newline="") as f:
        f.write("Famille;Wins;Losses;Timeouts;SumR\n")
        for st in setup_stats:
            f.write(f"{st['Famille']};{st['Wins']};{st['Losses']};{st['Timeouts']};{st['SumR']:.4f}\n")

    return {
        'Symbol': symbol_name,
        'Trades': total_trades,
        'WinRate': win_rate,
        'TotalR': total_r,
        'TotalUSD': total_usd,
        'ProfitFactor': profit_factor
    }

def main():
    parser = argparse.ArgumentParser(description="Analyse des performances Swing Multi-Actifs")
    parser.add_argument("--symbol", type=str, default=None, help="Symbole spécifique (GC, NQ, ES, CL, MNQ)")
    parser.add_argument("--reset", action="store_true", help="Réinitialise les fichiers de journaux pour un nouveau test vierge de 100 jours")
    args = parser.parse_args()

    nt8_file = os.path.expanduser("~/Documents/NinjaTrader 8/shadow/swing_trades.csv")

    if args.reset:
        print("Réinitialisation des journaux Shadow Swing...")
        header = "TradeId,SignalId,Symbol,Direction,SetupType,Tier,Status,EntryTimeUtc,ExitTimeUtc,EntryPrice,ExitPrice,StopPrice,TP1,TP2,InitialContracts,RemainingContracts,RealizedR,RealizedUSD,ExitReason,Notes\n"
        if os.path.exists(nt8_file):
            with open(nt8_file, "w", encoding="utf-8") as f:
                f.write(header)
            print(f"  * Réinitialisé : {nt8_file}")
        
        for sym in ["GC", "NQ", "ES", "CL", "MNQ"]:
            repo_file = f"c:/AMC-Pro/AMC-V8/shadow/SWING/{sym}/swing_trades_{sym}.csv"
            os.makedirs(os.path.dirname(repo_file), exist_ok=True)
            with open(repo_file, "w", encoding="utf-8") as f:
                f.write(header)
            print(f"  * Réinitialisé : {repo_file}")
        print("Prêt pour la nouvelle campagne de test 100 jours !")
        return

    # Chargement global
    df_all = None
    if os.path.exists(nt8_file):
        df_all = pd.read_csv(nt8_file)

    if df_all is None or len(df_all) == 0:
        # Fallback sur les fichiers par symbole
        frames = []
        for sym in ["GC", "NQ", "ES", "CL", "MNQ"]:
            p = f"c:/AMC-Pro/AMC-V8/shadow/SWING/{sym}/swing_trades_{sym}.csv"
            if os.path.exists(p):
                try:
                    df_sym = pd.read_csv(p)
                    if len(df_sym) > 0:
                        frames.append(df_sym)
                except:
                    pass
        if frames:
            df_all = pd.concat(frames, ignore_index=True)

    if df_all is None or len(df_all) == 0:
        print("Aucune donnée de trade Swing trouvée.")
        return

    # Normalisation du symbole
    df_all['Symbol'] = df_all['Symbol'].fillna('GC').astype(str).str.upper()

    if args.symbol:
        sym = args.symbol.upper()
        df_sub = df_all[df_all['Symbol'] == sym]
        analyze_dataset(df_sub, sym)
    else:
        # Analyse de chaque symbole présent
        symbols = df_all['Symbol'].unique()
        master_summary = []
        for sym in sorted(symbols):
            df_sub = df_all[df_all['Symbol'] == sym]
            res = analyze_dataset(df_sub, sym)
            if res:
                master_summary.append(res)
        
        if len(symbols) > 1:
            print("\n==========================================================================")
            print("                 MASTER RÉCAPITULATIF MULTI-ACTIFS SWING                  ")
            print("==========================================================================")
            print(f"{'SYMBOLE':8s} | {'TRADES':6s} | {'WIN RATE':8s} | {'TOTAL R':9s} | {'PNL NET ($)':13s} | {'PF':5s}")
            print("-" * 65)
            for m in master_summary:
                print(f"{m['Symbol']:8s} | {m['Trades']:6d} | {m['WinRate']:7.1f}% | {m['TotalR']:+8.2f}R | ${m['TotalUSD']:+12,.2f} | {m['ProfitFactor']:4.2f}")
            print("-" * 65)
            tot_trades = sum(m['Trades'] for m in master_summary)
            tot_r = sum(m['TotalR'] for m in master_summary)
            tot_usd = sum(m['TotalUSD'] for m in master_summary)
            print(f"{'TOTAL':8s} | {tot_trades:6d} | {'--':8s} | {tot_r:+8.2f}R | ${tot_usd:+12,.2f} |")

if __name__ == "__main__":
    main()
