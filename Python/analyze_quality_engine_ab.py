import os
import sys
import argparse
import pandas as pd
import numpy as np

if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

def compute_drawdown(series):
    """Calcule le Max Drawdown absolu ($ ou R) sur une serie de PnL cumule."""
    cum = series.cumsum()
    peak = np.maximum.accumulate(cum)
    dd = peak - cum
    return dd.max() if len(dd) > 0 else 0.0

def evaluate_run(csv_path, symbol):
    if not os.path.exists(csv_path):
        return None
    try:
        df = pd.read_csv(csv_path)
    except Exception as e:
        print(f"Erreur lecture {csv_path}: {e}")
        return None

    closed = df[df['Status'] == 'CLOSED'].copy()
    total_trades = len(closed)
    if total_trades == 0:
        return {
            'Trades': 0, 'WinRate': 0.0, 'TotalR': 0.0, 'TotalUSD': 0.0,
            'ProfitFactor': 0.0, 'MaxDD_USD': 0.0, 'MaxDD_R': 0.0, 'AvgTrade_USD': 0.0
        }

    wins = closed[closed['RealizedR'] > 0]
    losses = closed[closed['RealizedR'] < 0]
    be = closed[closed['RealizedR'] == 0]

    win_rate = (len(wins) / total_trades) * 100.0
    total_r = closed['RealizedR'].sum()
    total_usd = closed['RealizedUSD'].sum()

    gross_profit = wins['RealizedUSD'].sum()
    gross_loss = abs(losses['RealizedUSD'].sum())
    pf = (gross_profit / gross_loss) if gross_loss > 0 else (999.0 if gross_profit > 0 else 0.0)

    max_dd_usd = compute_drawdown(closed['RealizedUSD'])
    max_dd_r = compute_drawdown(closed['RealizedR'])
    avg_trade_usd = total_usd / total_trades

    return {
        'Trades': total_trades,
        'Wins': len(wins),
        'Losses': len(losses),
        'BE': len(be),
        'WinRate': win_rate,
        'TotalR': total_r,
        'TotalUSD': total_usd,
        'ProfitFactor': pf,
        'MaxDD_USD': max_dd_usd,
        'MaxDD_R': max_dd_r,
        'AvgTrade_USD': avg_trade_usd,
        'Df': closed
    }

def print_comparison(sym, res_a, res_b):
    print("\n" + "=" * 85)
    print(f"       ANALYSE COMPARATIVE A/B QUALITY ENGINE -- INSTRUMENT : {sym}")
    print("=" * 85)

    if not res_a or res_a['Trades'] == 0:
        print(f"[!] Donnees manquantes pour le Run A (Baseline) sur {sym}.")
    if not res_b or res_b['Trades'] == 0:
        print(f"[!] Donnees manquantes pour le Run B (QualityEngine) sur {sym}.")
    if (not res_a or res_a['Trades'] == 0) and (not res_b or res_b['Trades'] == 0):
        return

    print(f"{'METRIQUE':<26} | {'RUN A (BASELINE)':<22} | {'RUN B (QUALITY ENGINE)':<22} | {'DELTA':<12}")
    print("-" * 85)

    def row(label, val_a, val_b, fmt_val, fmt_delta, higher_better=True):
        va_str = fmt_val(val_a) if res_a else "N/A"
        vb_str = fmt_val(val_b) if res_b else "N/A"
        if res_a and res_b and isinstance(val_a, (int, float)) and isinstance(val_b, (int, float)):
            delta = val_b - val_a
            d_str = fmt_delta(delta)
        else:
            d_str = "--"
        print(f"{label:<26} | {va_str:<22} | {vb_str:<22} | {d_str:<12}")

    t_a = res_a['Trades'] if res_a else 0
    t_b = res_b['Trades'] if res_b else 0
    row("Nombre de Trades", t_a, t_b, lambda v: f"{v}", lambda d: f"{d:+d}")

    wr_a = res_a['WinRate'] if res_a else 0.0
    wr_b = res_b['WinRate'] if res_b else 0.0
    row("Win Rate", wr_a, wr_b, lambda v: f"{v:.2f}%", lambda d: f"{d:+.2f}%")

    pf_a = res_a['ProfitFactor'] if res_a else 0.0
    pf_b = res_b['ProfitFactor'] if res_b else 0.0
    row("Profit Factor", pf_a, pf_b, lambda v: f"{v:.2f}", lambda d: f"{d:+.2f}")

    dd_a = res_a['MaxDD_USD'] if res_a else 0.0
    dd_b = res_b['MaxDD_USD'] if res_b else 0.0
    row("Max Drawdown ($)", dd_a, dd_b, lambda v: f"${v:,.2f}", lambda d: f"${d:+,.2f}", higher_better=False)

    ddr_a = res_a['MaxDD_R'] if res_a else 0.0
    ddr_b = res_b['MaxDD_R'] if res_b else 0.0
    row("Max Drawdown (R)", ddr_a, ddr_b, lambda v: f"{v:.2f} R", lambda d: f"{d:+.2f} R", higher_better=False)

    pnl_a = res_a['TotalUSD'] if res_a else 0.0
    pnl_b = res_b['TotalUSD'] if res_b else 0.0
    row("PnL Net Realise ($)", pnl_a, pnl_b, lambda v: f"${v:+,.2f}", lambda d: f"${d:+,.2f}")

    r_a = res_a['TotalR'] if res_a else 0.0
    r_b = res_b['TotalR'] if res_b else 0.0
    row("R-Multiple Net", r_a, r_b, lambda v: f"{v:+.2f} R", lambda d: f"{d:+.2f} R")

    avg_a = res_a['AvgTrade_USD'] if res_a else 0.0
    avg_b = res_b['AvgTrade_USD'] if res_b else 0.0
    row("Esperance / Trade ($)", avg_a, avg_b, lambda v: f"${v:+,.2f}", lambda d: f"${d:+,.2f}")

    if res_a and res_b and t_a > 0:
        filter_rate = (1.0 - (t_b / t_a)) * 100.0
        print("-" * 85)
        print(f"[*] Taux de filtrage / abstention du QualityEngine : {filter_rate:.1f}% des trades evites")

def main():
    parser = argparse.ArgumentParser(description="Analyse comparative A/B de l'impact du QualityEngine")
    parser.add_argument("--symbol", type=str, default=None, help="Symbole a analyser (CL, ES, NQ) ou tous")
    args = parser.parse_args()

    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    qe_dir = os.path.join(root_dir, 'shadow', 'SWING', 'QUALITY_ENGINE_AB')
    run_a_dir = os.path.join(qe_dir, 'RUN_A_BASELINE')
    run_b_dir = os.path.join(qe_dir, 'RUN_B_QUALITY_ENGINE')

    symbols = [args.symbol.upper()] if args.symbol else ['CL', 'ES', 'NQ']

    for sym in symbols:
        path_a = os.path.join(run_a_dir, sym, f"swing_trades_{sym}.csv")
        path_b = os.path.join(run_b_dir, sym, f"swing_trades_{sym}.csv")

        res_a = evaluate_run(path_a, sym)
        res_b = evaluate_run(path_b, sym)

        print_comparison(sym, res_a, res_b)

if __name__ == "__main__":
    main()
