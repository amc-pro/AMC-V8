import csv
from collections import defaultdict, Counter

outcomes_path = "c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv"
journal_path = "c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv"

# 1. Parse outcomes
trades = []
with open(outcomes_path, "r", encoding="utf-8-sig", errors="ignore") as f:
    reader = csv.DictReader(f, delimiter=';')
    for row in reader:
        r = {k.strip().lower(): (v.strip() if v else "") for k, v in row.items() if k}
        if r.get("outcome"):
            try:
                r_mult = float(r.get("r_multiple", 0.0))
                score = float(r.get("score", 0.0))
            except:
                r_mult = 0.0
                score = 0.0
            trades.append({
                "entry_time": r.get("entry_time", ""),
                "exit_time": r.get("exit_time", ""),
                "setup": r.get("setup", "N/A"),
                "side": r.get("side", "N/A"),
                "grade": r.get("grade", "N/A"),
                "score": score,
                "outcome": r.get("outcome", ""),
                "r_multiple": r_mult,
                "is_win": r.get("outcome") in ("TARGET1", "TARGET2"),
                "is_loss": r.get("outcome") == "STOP"
            })

print(f"==========================================================================")
print(f"               ANALYSE DES TRADES DU SHADOW JOURNAL SUR GC (GOLD)         ")
print(f"==========================================================================")

# Check all instruments in journal
instruments = Counter()
journal_rows = []
with open(journal_path, "r", encoding="utf-8-sig", errors="ignore") as f:
    reader = csv.DictReader(f, delimiter=';')
    for row in reader:
        r = {k.strip().lower(): (v.strip() if v else "") for k, v in row.items() if k}
        inst = r.get("instrument", "Unknown")
        instruments[inst] += 1
        journal_rows.append(r)

print(f"Total lignes dans le Journal Sniper : {len(journal_rows)}")
print(f"Instruments detectes dans le journal :")
for inst, count in instruments.items():
    print(f"  * {inst:20s} : {count:4d} candidats")

# Outcomes stats
print(f"\n--- TOTAL TRADES EXECUTÉS DANS OUTCOMES : {len(trades)} ---")
if trades:
    wins = [t for t in trades if t["is_win"]]
    losses = [t for t in trades if t["is_loss"]]
    timeouts = [t for t in trades if t["outcome"] == "TIMEOUT"]
    
    total_r = sum(t["r_multiple"] for t in trades)
    gross_win_r = sum(t["r_multiple"] for t in wins)
    gross_loss_r = abs(sum(t["r_multiple"] for t in losses))
    
    win_rate = (len(wins) / len(trades)) * 100 if trades else 0
    profit_factor = (gross_win_r / gross_loss_r) if gross_loss_r > 0 else float("inf")
    expectancy = total_r / len(trades) if trades else 0
    
    print(f"  * Win Rate         : {win_rate:.2f} % ({len(wins)} W / {len(losses)} L / {len(timeouts)} Timeouts)")
    print(f"  * Gain Net Total   : {total_r:+.2f} R")
    print(f"  * Gains Bruts      : +{gross_win_r:.2f} R")
    print(f"  * Pertes Brutes    : -{gross_loss_r:.2f} R")
    print(f"  * Profit Factor    : {profit_factor:.2f}")
    print(f"  * Esperance E[R]   : {expectancy:+.2f} R / trade")

    # Group by setup
    by_setup = defaultdict(list)
    for t in trades:
        by_setup[t["setup"]].append(t)
        
    print(f"\n--- Performance par Setup ---")
    for s, st_trades in sorted(by_setup.items(), key=lambda x: len(x[1]), reverse=True):
        st_wins = [t for t in st_trades if t["is_win"]]
        st_losses = [t for t in st_trades if t["is_loss"]]
        st_r = sum(t["r_multiple"] for t in st_trades)
        st_wr = (len(st_wins) / len(st_trades)) * 100
        print(f"  * {s:25s} : {len(st_trades):2d} trades | WR {st_wr:5.1f}% | Net R: {st_r:+6.2f} R | Wins: {len(st_wins)} | Loss: {len(st_losses)}")

    # Group by Date
    by_date = defaultdict(list)
    for t in trades:
        d = t["entry_time"].split()[0] if " " in t["entry_time"] else "Unknown"
        by_date[d].append(t)
        
    print(f"\n--- Performance par Journee ---")
    for d, d_trades in sorted(by_date.items()):
        d_wins = [t for t in d_trades if t["is_win"]]
        d_losses = [t for t in d_trades if t["is_loss"]]
        d_r = sum(t["r_multiple"] for t in d_trades)
        d_wr = (len(d_wins) / len(d_trades)) * 100
        print(f"  * {d} : {len(d_trades):2d} trades | WR {d_wr:5.1f}% | Net R: {d_r:+6.2f} R | Wins: {len(d_wins)} | Losses: {len(d_losses)}")

    print(f"\n--- Liste Complète des Derniers Trades Exécutés ---")
    for idx, t in enumerate(trades[-25:], 1):
        status = "WIN " if t["is_win"] else ("LOSS" if t["is_loss"] else "TIME")
        print(f"  {idx:02d}. [{t['entry_time']} -> {t['exit_time']}] {t['setup']:22s} {t['side']:5s} | Score:{t['score']:4.1f} [{t['grade']:8s}] | {status} ({t['r_multiple']:+6.2f} R)")
