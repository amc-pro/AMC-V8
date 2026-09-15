import os
import sys
import shutil
import argparse
import pandas as pd

if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

def main():
    parser = argparse.ArgumentParser(description="Collecte d'un run Shadow NinjaTrader vers le dossier A/B")
    parser.add_argument("--run", type=str, required=True, choices=["A", "B", "a", "b"], help="Type de run (A=Baseline, B=QualityEngine)")
    parser.add_argument("--symbol", type=str, required=True, help="Symbole (CL, ES, NQ, MNQ, etc.)")
    args = parser.parse_args()

    run_type = args.run.upper()
    sym = args.symbol.upper()

    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    nt8_shadow = os.path.expanduser('~/Documents/NinjaTrader 8/shadow')
    nt8_file = os.path.join(nt8_shadow, 'swing_trades.csv')

    if not os.path.exists(nt8_file):
        print(f"[ERROR] Fichier {nt8_file} introuvable.")
        return

    df = pd.read_csv(nt8_file)
    print(f"[*] Chargement du journal NT8 : {len(df)} lignes trouvees.")

    run_name = "RUN_A_BASELINE" if run_type == "A" else "RUN_B_QUALITY_ENGINE"
    target_dir = os.path.join(root_dir, 'shadow', 'SWING', 'QUALITY_ENGINE_AB', run_name, sym)
    os.makedirs(target_dir, exist_ok=True)
    target_file = os.path.join(target_dir, f"swing_trades_{sym}.csv")

    shutil.copy2(nt8_file, target_file)
    print(f"[OK] Run {run_type} sauvegarde dans : {target_file}")

    # Reinitialisation propre du fichier NT8 pour le prochain run
    header = "TradeId,SignalId,Symbol,Direction,SetupType,Tier,Status,EntryTimeUtc,ExitTimeUtc,EntryPrice,ExitPrice,StopPrice,TP1,TP2,InitialContracts,RemainingContracts,RealizedR,RealizedUSD,ExitReason,Notes\n"
    with open(nt8_file, 'w', encoding='utf-8') as f:
        f.write(header)
    print(f"[*] Journal NT8 {nt8_file} reinitialise et pret pour le prochain test.")

if __name__ == "__main__":
    main()
