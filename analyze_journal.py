#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AMC PRO — Journal & Strategy Performance Analyzer
Analyse rapidement les résultats réels et de shadow-journaling générés par AMC PRO / NinjaTrader 8.
"""

import sys
import os
import glob
import csv
from collections import defaultdict

# Support UTF-8 output on Windows terminal
if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


def find_journal_files():
    candidates = []
    # 1. Dossier courant
    for f in glob.glob("*outcomes*.csv"):
        candidates.append(os.path.abspath(f))
    for f in glob.glob("*journal*.csv"):
        candidates.append(os.path.abspath(f))

    # 2. Dossier NinjaTrader 8 par défaut
    nt_dir = os.path.expanduser(r"~\Documents\NinjaTrader 8")
    if os.path.exists(nt_dir):
        for f in glob.glob(os.path.join(nt_dir, "*outcomes*.csv")):
            candidates.append(os.path.abspath(f))
        for f in glob.glob(os.path.join(nt_dir, "sniper", "*outcomes*.csv")):
            candidates.append(os.path.abspath(f))
        for f in glob.glob(os.path.join(nt_dir, "*sniper*.csv")):
            if "_outcomes" in f:
                candidates.append(os.path.abspath(f))

    # Dédoublonner en conservant l'ordre
    seen = set()
    unique_candidates = []
    for c in candidates:
        if c not in seen and os.path.exists(c) and os.path.getsize(c) > 0:
            seen.add(c)
            unique_candidates.append(c)
    return unique_candidates

def parse_outcomes_csv(filepath):
    trades = []
    with open(filepath, "r", encoding="utf-8-sig") as f:
        # Détection du délimiteur (; ou ,)
        sample = f.readline()
        f.seek(0)
        delimiter = ";" if ";" in sample else ","
        reader = csv.DictReader(f, delimiter=delimiter)
        
        for row in reader:
            # Normaliser les clés en minuscules
            r = {k.strip().lower(): v.strip() for k, v in row.items() if k}
            
            outcome = r.get("outcome", "").upper()
            if not outcome or outcome not in ("TARGET1", "TARGET2", "STOP", "TIMEOUT", "SESSION_END"):
                continue
                
            try:
                r_mult = float(r.get("r_multiple", 0.0))
            except ValueError:
                r_mult = 0.0
                
            try:
                score = float(r.get("score", 0.0))
            except ValueError:
                score = 0.0

            trades.append({
                "entry_time": r.get("entry_time", ""),
                "exit_time": r.get("exit_time", ""),
                "setup": r.get("setup", "N/A"),
                "side": r.get("side", "N/A").upper(),
                "grade": r.get("grade", "N/A").upper(),
                "score": score,
                "outcome": outcome,
                "r_multiple": r_mult,
                "is_win": outcome in ("TARGET1", "TARGET2"),
                "is_loss": outcome == "STOP"
            })
    return trades

def compute_metrics(trades):
    if not trades:
        return None
    
    total = len(trades)
    wins = [t for t in trades if t["is_win"]]
    losses = [t for t in trades if t["is_loss"]]
    timeouts = [t for t in trades if t["outcome"] == "TIMEOUT"]
    session_ends = [t for t in trades if t["outcome"] == "SESSION_END"]
    
    tp1_count = sum(1 for t in trades if t["outcome"] == "TARGET1")
    tp2_count = sum(1 for t in trades if t["outcome"] == "TARGET2")
    
    win_rate = (len(wins) / total) * 100.0 if total > 0 else 0.0
    loss_rate = (len(losses) / total) * 100.0 if total > 0 else 0.0
    
    r_multiples = [t["r_multiple"] for t in trades]
    total_r = sum(r_multiples)
    gross_win_r = sum(r for r in r_multiples if r > 0)
    gross_loss_r = abs(sum(r for r in r_multiples if r < 0))
    profit_factor = (gross_win_r / gross_loss_r) if gross_loss_r > 0 else (99.9 if gross_win_r > 0 else 0.0)
    expectancy = (total_r / total) if total > 0 else 0.0
    
    # Max Drawdown en R
    cum_r = 0.0
    peak_r = 0.0
    max_dd_r = 0.0
    for r in r_multiples:
        cum_r += r
        if cum_r > peak_r:
            peak_r = cum_r
        dd = peak_r - cum_r
        if dd > max_dd_r:
            max_dd_r = dd
            
    # Streaks (séries)
    max_win_streak = 0
    max_loss_streak = 0
    cur_win_streak = 0
    cur_loss_streak = 0
    for t in trades:
        if t["is_win"]:
            cur_win_streak += 1
            cur_loss_streak = 0
            if cur_win_streak > max_win_streak:
                max_win_streak = cur_win_streak
        elif t["is_loss"]:
            cur_loss_streak += 1
            cur_win_streak = 0
            if cur_loss_streak > max_loss_streak:
                max_loss_streak = cur_loss_streak
        else:
            cur_win_streak = 0
            cur_loss_streak = 0

    return {
        "total": total,
        "wins": len(wins),
        "losses": len(losses),
        "timeouts": len(timeouts),
        "session_ends": len(session_ends),
        "tp1_count": tp1_count,
        "tp2_count": tp2_count,
        "win_rate": win_rate,
        "loss_rate": loss_rate,
        "total_r": total_r,
        "gross_win_r": gross_win_r,
        "gross_loss_r": gross_loss_r,
        "profit_factor": profit_factor,
        "expectancy": expectancy,
        "max_dd_r": max_dd_r,
        "max_win_streak": max_win_streak,
        "max_loss_streak": max_loss_streak,
    }

def print_group_breakdown(title, grouped_dict):
    print(f"\n┌────────────────────────────────────────────────────────────────────────────────────────┐")
    print(f"│ 📊 {title:<83} │")
    print(f"├──────────────────────┬─────────┬──────────┬──────────┬───────────┬──────────────┬────────┤")
    print(f"│ Catégorie            │  Trades │ Win Rate │ Pertes % │ Gain Net  │  P. Factor   │  E[R]  │")
    print(f"├──────────────────────┼─────────┼──────────┼──────────┼───────────┼──────────────┼────────┤")
    
    for key, items in sorted(grouped_dict.items(), key=lambda x: len(x[1]), reverse=True):
        m = compute_metrics(items)
        if not m:
            continue
        cat_str = (key[:20] + "..") if len(key) > 20 else key
        pf_str = f"{m['profit_factor']:.2f}" if m['profit_factor'] < 99 else ">99.0"
        print(f"│ {cat_str:<20} │ {m['total']:7d} │ {m['win_rate']:7.1f}% │ {m['loss_rate']:7.1f}% │ {m['total_r']:+8.2f}R │ {pf_str:>12s} │ {m['expectancy']:+5.2f}R │")
    print(f"└──────────────────────┴─────────┴──────────┴──────────┴───────────┴──────────────┴────────┘")

def analyze_file(filepath):
    trades = parse_outcomes_csv(filepath)
    if not trades:
        print(f"❌ Aucun trade valide trouvé dans : {filepath}")
        return
        
    m = compute_metrics(trades)
    
    print("\n" + "=" * 90)
    print(f"🎯 RAPPORT DE PERFORMANCE AMC PRO — {os.path.basename(filepath)}")
    print("=" * 90)
    print(f"📁 Fichier analysé : {filepath}")
    entry_times = [t["entry_time"] for t in trades if t["entry_time"]]
    exit_times = [t["exit_time"] for t in trades if t["exit_time"]]
    if entry_times and exit_times:
        print(f"📅 Période couverte: du {min(entry_times)} au {max(exit_times)}")
    print("-" * 90)
    
    print(f"📈 TOTAL TRADES EXÉCUTÉS : {m['total']}")
    print(f"   ├─ ✅ Gagnants (TP1 + TP2) : {m['wins']:4d}  ({m['win_rate']:.2f}%)  [TP1: {m['tp1_count']} | TP2: {m['tp2_count']}]")
    print(f"   ├─ ❌ Perdants (Stop Loss)  : {m['losses']:4d}  ({m['loss_rate']:.2f}%)")
    print(f"   ├─ ⏳ Expirations (Timeout) : {m['timeouts']:4d}  ({(m['timeouts']/m['total'])*100:.2f}%)")
    if m['session_ends'] > 0:
        print(f"   └─ 🛑 Fin de Session        : {m['session_ends']:4d}  ({(m['session_ends']/m['total'])*100:.2f}%)")
    
    print("-" * 90)
    print(f"💰 RÉSULTATS FINANCIERS (en Multiples de Risque R) :")
    print(f"   ├─ 🚀 Gain Net Total       : {m['total_r']:+.2f} R")
    print(f"   ├─ 💎 Gains Bruts (Wins)   : +{m['gross_win_r']:.2f} R")
    print(f"   ├─ 🔻 Pertes Brutes        : -{m['gross_loss_r']:.2f} R")
    print(f"   ├─ ⚖️ Profit Factor (PF)   : {m['profit_factor']:.2f}")
    print(f"   ├─ 🎯 Espérance E[R]/trade : {m['expectancy']:+.2f} R")
    print(f"   ├─ 📉 Max Drawdown (en R)  : {m['max_dd_r']:.2f} R")
    print(f"   └─ 🔥 Max Série Win/Loss   : {m['max_win_streak']} Wins consécutifs / {m['max_loss_streak']} Pertes consécutives")
    
    # 1. Performance par Mois (Monthly Breakdown)
    by_month = defaultdict(list)
    for t in trades:
        m_key = t["entry_time"][:7] if len(t["entry_time"]) >= 7 else "Inconnu"
        by_month[m_key].append(t)
    print_group_breakdown("PERFORMANCE MENSUELLE (PAR MOIS)", by_month)

    # 2. Performance par Grade
    by_grade = defaultdict(list)
    for t in trades:
        by_grade[t["grade"]].append(t)
    print_group_breakdown("PERFORMANCE PAR GRADE", by_grade)
    
    # 3. Performance par Sens (LONG vs SHORT)
    by_side = defaultdict(list)
    for t in trades:
        by_side[t["side"]].append(t)
    print_group_breakdown("PERFORMANCE PAR SENS (LONG vs SHORT)", by_side)
    
    # 4. Performance par Setup Type
    by_setup = defaultdict(list)
    for t in trades:
        by_setup[t["setup"]].append(t)
    print_group_breakdown("PERFORMANCE PAR PATTERN / SETUP", by_setup)
    
    # 5. Performance par Tranche de Score
    by_score = defaultdict(list)
    for t in trades:
        s = t["score"]
        if s >= 80:
            bracket = "[80 - 100] (Très Élevé)"
        elif s >= 70:
            bracket = "[70 - 79]  (Élevé)"
        elif s >= 50:
            bracket = "[50 - 69]  (Moyen)"
        elif s >= 35:
            bracket = "[35 - 49]  (Scalping Pro)"
        else:
            bracket = "[ 0 - 34]  (Faible)"
        by_score[bracket].append(t)
    print_group_breakdown("PERFORMANCE PAR TRANCHE DE SCORE", by_score)


def analyze_candidates_file(filepath):
    print("\n" + "=" * 90)
    print(f"🎯 ANALYSE DES SIGNAUX CANDIDATS AMC PRO — {os.path.basename(filepath)}")
    print("=" * 90)
    print(f"📁 Fichier analysé : {filepath}")
    
    rows = []
    with open(filepath, "r", encoding="utf-8-sig") as f:
        sample = f.readline()
        f.seek(0)
        delimiter = ";" if ";" in sample else ","
        reader = csv.DictReader(f, delimiter=delimiter)
        for r in reader:
            rows.append({k.strip().lower(): v.strip() for k, v in r.items() if k})
            
    if not rows:
        print("❌ Aucun signal candidat trouvé.")
        return
        
    times = [r.get("time", "") for r in rows if r.get("time")]
    if times:
        print(f"📅 Période couverte: du {min(times)} au {max(times)}")
    print(f"📈 Total Candidats Générés : {len(rows):,}")
    
    gated_0 = [r for r in rows if r.get("gated") == "0"]
    gated_1 = [r for r in rows if r.get("gated") == "1"]
    
    print(f"   ├─ ✅ Signaux Validés (Passé tous les Gates) : {len(gated_0):,} ({len(gated_0)/len(rows)*100:.1f}%)")
    print(f"   └─ 🛡️ Signaux Bloqués par Gates Sécurité   : {len(gated_1):,} ({len(gated_1)/len(rows)*100:.1f}%)")
    
    # Répartition des motifs de blocage
    gates = defaultdict(int)
    for r in gated_1:
        gf = r.get("gate_failed", "Autre")
        gates[gf] += 1
    
    print("\n┌────────────────────────────────────────────────────────────────────────────────────────┐")
    print(f"│ 🛡️ RÉPARTITION DES GATES DE PROTECTION (FILTRES DE SÉCURITÉ)                           │")
    print("├──────────────────────────────────────────────────────┬──────────────┬──────────────────┤")
    print("│ Gate / Filtre Déclenché                              │   Nb Bloqués │       Pourcentage│")
    print("├──────────────────────────────────────────────────────┼──────────────┼──────────────────┤")
    for g, count in sorted(gates.items(), key=lambda x: x[1], reverse=True)[:10]:
        print(f"│ {g:<52} │ {count:12,d} │ {count/len(gated_1)*100:15.1f}% │")
    print("└──────────────────────────────────────────────────────┴──────────────┴──────────────────┘")
    
    # Répartition par Mois pour les signaux validés
    by_month = defaultdict(list)
    for r in gated_0:
        m_key = r.get("time", "")[:7] if len(r.get("time", "")) >= 7 else "Inconnu"
        by_month[m_key].append(r)
        
    print("\n┌────────────────────────────────────────────────────────────────────────────────────────┐")
    print(f"│ 📅 SIGNAUX VALIDÉS PAR MOIS (PRÊTS POUR EXÉCUTION)                                     │")
    print("├──────────────────────┬──────────────────────┬──────────────────────────────────────────┤")
    print("│ Mois (Année-Mois)    │ Nb Signaux Validés   │ Répartition Grades (A+, A, B, C...)      │")
    print("├──────────────────────┼──────────────────────┼──────────────────────────────────────────┤")
    for m, m_rows in sorted(by_month.items()):
        grades_cnt = Counter(r.get("grade", "C") for r in m_rows)
        grades_str = ", ".join(f"{g}:{c}" for g, c in sorted(grades_cnt.items()))
        print(f"│ {m:<20} │ {len(m_rows):20d} │ {grades_str:<40} │")
    print("└──────────────────────┴──────────────────────┴──────────────────────────────────────────┘")


def main():
    target_file = None
    if len(sys.argv) > 1:
        target_file = sys.argv[1]
    else:
        files = find_journal_files()
        if not files:
            # Chercher n'importe quel fichier sniper
            nt_dir = os.path.expanduser(r"~\Documents\NinjaTrader 8")
            all_sniper = glob.glob(os.path.join(nt_dir, "*sniper*.csv"))
            if all_sniper:
                files = all_sniper
                
        if not files:
            print("❌ Aucun fichier de journal / outcomes trouvé dans le dossier NinjaTrader ou local.")
            print("💡 Pour lancer un test dans NinjaTrader 8 :")
            print("   1. Chargez l'indicateur SniperMarketCorePro sur un graphique avec de l'historique.")
            print("   2. Cochez 'Journal Sniper (shadow mode)' ou passez 'Mode d'execution' sur 'Research'.")
            print("   3. Relancez ce script : python analyze_journal.py")
            return
        
        # Priorité aux fichiers outcomes
        target_file = files[0]
        for f in files:
            if "outcomes" in f.lower():
                target_file = f
                break
                
    if os.path.exists(target_file):
        if "outcomes" in target_file.lower():
            analyze_file(target_file)
        else:
            analyze_candidates_file(target_file)
    else:
        print(f"❌ Fichier non trouvé : {target_file}")


if __name__ == "__main__":
    from collections import Counter
    main()

