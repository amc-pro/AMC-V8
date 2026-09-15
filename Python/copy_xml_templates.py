"""
Script de Synchronisation Automatique des Templates XML AMC-V8 vers NinjaTrader 8.
Copie l'ensemble des configurations SCALPING_PRO et SWING vers les dossiers de templates NinjaTrader.
"""

import os
import sys
import shutil
import glob

sys.stdout.reconfigure(encoding='utf-8')

def sync_templates():
    # Chemins source (Dépôt Git)
    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    scalping_src = os.path.join(base_dir, "configs", "SCALPING_PRO")
    swing_src = os.path.join(base_dir, "configs", "SWING")

    # Chemins cible (NinjaTrader 8)
    user_home = os.path.expanduser("~")
    nt8_indicator_base = os.path.join(user_home, "Documents", "NinjaTrader 8", "templates", "Indicator", "AuctionMarketCore")
    nt8_scalping_dest = os.path.join(nt8_indicator_base, "SCALPING_PRO")
    nt8_swing_dest = os.path.join(nt8_indicator_base, "SWING")

    # Création des dossiers cibles s'ils n'existent pas
    os.makedirs(nt8_indicator_base, exist_ok=True)
    os.makedirs(nt8_scalping_dest, exist_ok=True)
    os.makedirs(nt8_swing_dest, exist_ok=True)

    print("=" * 80)
    print("      SYNCHRONISATION DES TEMPLATES XML AMC-V8 -> NINJATRADER 8")
    print("=" * 80)
    print(f"Source Git       : {base_dir}\\configs")
    print(f"Cible NinjaTrader: {nt8_indicator_base}\n")

    total_copied = 0

    # 1. Copie SCALPING_PRO
    print("[1/2] Synchronisation SCALPING PRO :")
    scalping_files = glob.glob(os.path.join(scalping_src, "*.xml"))
    if not scalping_files:
        print("  ❌ Aucun fichier XML trouvé dans configs/SCALPING_PRO")
    for src_file in scalping_files:
        fname = os.path.basename(src_file)
        
        # Copie dans le sous-dossier SCALPING_PRO
        dest_file_sub = os.path.join(nt8_scalping_dest, fname)
        shutil.copy2(src_file, dest_file_sub)
        
        # Copie aussi dans la racine AuctionMarketCore pour sélection directe
        dest_file_root = os.path.join(nt8_indicator_base, fname)
        shutil.copy2(src_file, dest_file_root)
        
        size_kb = os.path.getsize(src_file) / 1024.0
        print(f"  * {fname:30s} -> Copié ({size_kb:.1f} KB)")
        total_copied += 1

    # 2. Copie SWING
    print("\n[2/2] Synchronisation SWING :")
    swing_files = glob.glob(os.path.join(swing_src, "*.xml"))
    if not swing_files:
        print("  ❌ Aucun fichier XML trouvé dans configs/SWING")
    for src_file in swing_files:
        fname = os.path.basename(src_file)
        
        # Copie dans le sous-dossier SWING
        dest_file_sub = os.path.join(nt8_swing_dest, fname)
        shutil.copy2(src_file, dest_file_sub)
        
        # Copie aussi dans la racine AuctionMarketCore
        dest_file_root = os.path.join(nt8_indicator_base, fname)
        shutil.copy2(src_file, dest_file_root)
        
        size_kb = os.path.getsize(src_file) / 1024.0
        print(f"  * {fname:30s} -> Copié ({size_kb:.1f} KB)")
        total_copied += 1

    print("\n" + "=" * 80)
    print(f"SYNCHRONISATION TERMINÉE AVEC SUCCÈS : {total_copied} fichiers XML déployés !")
    print("=" * 80)
    print("Dans NinjaTrader 8 :")
    print("  1. Clic droit sur votre graphique -> 'Templates' -> 'Load'")
    print("  2. Choisissez votre template (ex: CONFIG_GC_SCALPING_PRO, CONFIG_NQ_SCALPING_PRO...)")

if __name__ == "__main__":
    sync_templates()
