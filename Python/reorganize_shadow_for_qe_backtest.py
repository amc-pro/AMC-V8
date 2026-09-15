import os
import sys
import shutil
import hashlib
from datetime import datetime

# Assurer l'encodage utf-8 sur stdout pour Windows
if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

def sha256_file(filepath):
    if not os.path.exists(filepath):
        return None
    h = hashlib.sha256()
    with open(filepath, 'rb') as f:
        while chunk := f.read(8192):
            h.update(chunk)
    return h.hexdigest()

def main():
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    shadow_repo = os.path.join(root_dir, 'shadow')
    nt8_shadow = os.path.expanduser('~/Documents/NinjaTrader 8/shadow')
    
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_dir = os.path.join(shadow_repo, 'ARCHIVES', f'backup_{timestamp}_pre_qe')
    os.makedirs(backup_dir, exist_ok=True)
    
    print("=" * 80)
    print("REORGANISATION DU DOSSIER SHADOW -- PREPARATION DU BACKTEST QE (CL/ES/NQ)")
    print("=" * 80)
    
    # 1. Sauvegarde et scellage de NinjaTrader 8 shadow
    nt8_swing_csv = os.path.join(nt8_shadow, 'swing_trades.csv')
    if os.path.exists(nt8_swing_csv):
        h = sha256_file(nt8_swing_csv)
        lines = open(nt8_swing_csv, 'r', encoding='utf-8', errors='ignore').readlines()
        print(f"[*] Fichier NT8 swing_trades.csv trouve : {len(lines)} lignes (SHA-256: {h[:16]}...)")
        
        # Copie archive NT8
        nt8_backup_file = os.path.join(nt8_shadow, f'swing_trades_backup_{timestamp}.csv')
        shutil.copy2(nt8_swing_csv, nt8_backup_file)
        print(f"  -> Backup local NT8 : {nt8_backup_file}")
        
        # Copie archive Repo
        repo_backup_file = os.path.join(backup_dir, 'swing_trades_nt8_active.csv')
        shutil.copy2(nt8_swing_csv, repo_backup_file)
        print(f"  -> Backup repo scelle : {repo_backup_file}")
        
    # Copie des autres fichiers NT8 shadow
    if os.path.exists(nt8_shadow):
        for f in os.listdir(nt8_shadow):
            src_f = os.path.join(nt8_shadow, f)
            if os.path.isfile(src_f) and not f.startswith('swing_trades_backup_'):
                shutil.copy2(src_f, os.path.join(backup_dir, f"NT8_{f}"))

    # 2. Sauvegarde des runs precedents dans shadow/SWING
    swing_repo = os.path.join(shadow_repo, 'SWING')
    if os.path.exists(swing_repo):
        swing_backup_dir = os.path.join(backup_dir, 'SWING_SNAPSHOT')
        os.makedirs(swing_backup_dir, exist_ok=True)
        for item in os.listdir(swing_repo):
            src_item = os.path.join(swing_repo, item)
            dst_item = os.path.join(swing_backup_dir, item)
            if os.path.isfile(src_item):
                shutil.copy2(src_item, dst_item)
            elif os.path.isdir(src_item) and item not in ['QUALITY_ENGINE_AB']:
                shutil.copytree(src_item, dst_item, dirs_exist_ok=True)
        print(f"[*] Sauvegarde de l'etat actuel de shadow/SWING -> {swing_backup_dir}")

    # Deplacement de l'ancien backup 20260904 vers ARCHIVES s'il existe
    old_backup = os.path.join(shadow_repo, 'backup_20260904_095208')
    if os.path.exists(old_backup):
        archives_dir = os.path.join(shadow_repo, 'ARCHIVES')
        os.makedirs(archives_dir, exist_ok=True)
        dst_old = os.path.join(archives_dir, 'backup_20260904_095208')
        if not os.path.exists(dst_old):
            shutil.move(old_backup, dst_old)
            print(f"[*] Migration ancien backup {old_backup} -> {dst_old}")

    # 3. Creation de la structure pour le backtest QualityEngine A/B
    qe_dir = os.path.join(shadow_repo, 'SWING', 'QUALITY_ENGINE_AB')
    run_a_dir = os.path.join(qe_dir, 'RUN_A_BASELINE')
    run_b_dir = os.path.join(qe_dir, 'RUN_B_QUALITY_ENGINE')
    reports_dir = os.path.join(qe_dir, 'COMPARATIVE_REPORTS')
    
    instruments = ['CL', 'ES', 'NQ']
    for inst in instruments:
        os.makedirs(os.path.join(run_a_dir, inst), exist_ok=True)
        os.makedirs(os.path.join(run_b_dir, inst), exist_ok=True)
    os.makedirs(reports_dir, exist_ok=True)
    
    # Header standard pour les journaux Shadow Swing
    header = "TradeId,SignalId,Symbol,Direction,SetupType,Tier,Status,EntryTimeUtc,ExitTimeUtc,EntryPrice,ExitPrice,StopPrice,TP1,TP2,InitialContracts,RemainingContracts,RealizedR,RealizedUSD,ExitReason,Notes\n"
    
    # 4. Reinitialisation propre du fichier swing_trades.csv actif dans NT8
    if os.path.exists(nt8_shadow):
        with open(nt8_swing_csv, 'w', encoding='utf-8') as f:
            f.write(header)
        print(f"[*] Reinitialisation vierge : {nt8_swing_csv} (pret pour enregistrement)")

    # 5. Creation d'un README explicatif dans QUALITY_ENGINE_AB
    readme_path = os.path.join(qe_dir, 'README.md')
    with open(readme_path, 'w', encoding='utf-8') as f:
        f.write("""# PROTOCOLE DE BACKTEST A/B -- QUALITY ENGINE & NO-TRADE MATRIX

Dossier de recueil des resultats du backtest comparatif sur **CL**, **ES** et **NQ**.

## Structure du Repertoire

- `RUN_A_BASELINE/` : Resultats avec `EnableQualityEngine = false` (Baseline Swing V3 pure).
  - `CL/` : `swing_trades_CL.csv`
  - `ES/` : `swing_trades_ES.csv`
  - `NQ/` : `swing_trades_NQ.csv`
- `RUN_B_QUALITY_ENGINE/` : Resultats avec `EnableQualityEngine = true` (Filtrage No-Trade Matrix actif).
  - `CL/` : `swing_trades_CL.csv`
  - `ES/` : `swing_trades_ES.csv`
  - `NQ/` : `swing_trades_NQ.csv`
- `COMPARATIVE_REPORTS/` : Rapports d'analyse comparative du **Profit Factor** et du **Max Drawdown**.

## Procedure de Recuperation des Resultats

1. Lancer le backtest Run A sur NinjaTrader pour un instrument (ex: ES).
2. Executer : `python Python/collect_shadow_run.py --run A --symbol ES`
3. Lancer le backtest Run B sur NinjaTrader pour le meme instrument (ES).
4. Executer : `python Python/collect_shadow_run.py --run B --symbol ES`
5. Generer la synthese comparative : `python Python/analyze_quality_engine_ab.py --symbol ES`
""")
    print(f"[*] Fichier guide cree : {readme_path}")
    
    print("=" * 80)
    print("[SUCCESS] DOSSIER SHADOW PARFAITEMENT REORGANISE ET PRET POUR LE BACKTEST !")
    print("=" * 80)

if __name__ == "__main__":
    main()
