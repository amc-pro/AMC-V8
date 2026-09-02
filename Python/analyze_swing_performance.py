import os
import csv
import pandas as pd
from datetime import datetime

def analyze_swing():
    swing_csv = "c:/AMC-Pro/AMC-V8/shadow/SWING/GC/swing_trades_GC.csv"
    if not os.path.exists(swing_csv):
        swing_csv = os.path.expanduser("~/Documents/NinjaTrader 8/shadow/swing_trades.csv")

    if not os.path.exists(swing_csv):
        print(f"Erreur: Aucun fichier de trades swing trouvé ({swing_csv}).")
        return

    df = pd.read_csv(swing_csv)
    print("==========================================================================")
    print("           ANALYSE DE PERFORMANCE SHADOW SWING PRO - GC (GOLD)            ")
    print("==========================================================================")
    print(f"Source du journal: {swing_csv}")
    print(f"Total des enregistrements: {len(df)}")
    
    closed = df[df['Status'] == 'CLOSED'].copy()
    print(f"Trades clôturés évalués: {len(closed)}")
    
    if len(closed) == 0:
        print("Aucun trade clôturé à analyser.")
        return

    # Statistiques globales
    wins = closed[closed['RealizedR'] > 0]
    losses = closed[closed['RealizedR'] < 0]
    breakevens = closed[closed['RealizedR'] == 0]
    
    win_rate = (len(wins) / len(closed)) * 100.0 if len(closed) > 0 else 0.0
    total_r = closed['RealizedR'].sum()
    total_usd = closed['RealizedUSD'].sum()
    gross_profit = wins['RealizedUSD'].sum()
    gross_loss = abs(losses['RealizedUSD'].sum())
    profit_factor = (gross_profit / gross_loss) if gross_loss > 0 else (999.0 if gross_profit > 0 else 0.0)
    avg_trade_usd = total_usd / len(closed) if len(closed) > 0 else 0.0
    avg_win_usd = wins['RealizedUSD'].mean() if len(wins) > 0 else 0.0
    avg_loss_usd = losses['RealizedUSD'].mean() if len(losses) > 0 else 0.0

    print(f"\n--- MÉTRIQUES CLÉS GLOBALES ---")
    print(f"  * Win Rate         : {win_rate:.2f}% ({len(wins)}W / {len(losses)}L / {len(breakevens)}BE)")
    print(f"  * R-Multiple Total : {total_r:+.2f} R")
    print(f"  * PnL Réalisé      : ${total_usd:+,.2f}")
    print(f"  * Profit Factor    : {profit_factor:.2f}")
    print(f"  * Gain Moyen / Win : ${avg_win_usd:,.2f}")
    print(f"  * Perte Moyenne    : ${avg_loss_usd:,.2f}")
    print(f"  * Espérance / Trade: ${avg_trade_usd:+,.2f}")

    # Breakdown par Setup Type
    print("\n--- PERFORMANCE PAR FAMILLE DE SETUP ---")
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

    # Breakdown par Tier de Score
    print("\n--- PERFORMANCE PAR TIER DE QUALITÉ SWING ---")
    for tier, grp in closed.groupby('Tier'):
        t_wins = grp[grp['RealizedR'] > 0]
        t_losses = grp[grp['RealizedR'] < 0]
        t_wr = (len(t_wins) / len(grp)) * 100.0 if len(grp) > 0 else 0.0
        t_r = grp['RealizedR'].sum()
        t_usd = grp['RealizedUSD'].sum()
        print(f"  [{tier:12s}] {len(grp):3d} trades | WR: {t_wr:5.1f}% | R: {t_r:+6.1f}R | PnL: ${t_usd:+10,.2f}")

    # Génération du fichier stats GC standardisé
    stats_out = "c:/AMC-Pro/AMC-V8/shadow/SWING/GC/AuctionMarketCorePro_journal_stats_GC.csv"
    os.makedirs(os.path.dirname(stats_out), exist_ok=True)
    with open(stats_out, "w", encoding="utf-8", newline="") as f:
        f.write("Famille;Wins;Losses;Timeouts;SumR\n")
        for st in setup_stats:
            f.write(f"{st['Famille']};{st['Wins']};{st['Losses']};{st['Timeouts']};{st['SumR']:.4f}\n")
    print(f"\nFichier de statistiques mis à jour : {stats_out}")

if __name__ == "__main__":
    analyze_swing()
