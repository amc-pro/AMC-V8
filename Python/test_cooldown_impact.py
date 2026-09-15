# Test Cooldown / Single-Active-Trade-per-direction on shadow outcomes

import csv
from datetime import datetime

outcomes_path = "c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv"

trades = []
with open(outcomes_path, "r", encoding="utf-8-sig", errors="ignore") as f:
    reader = csv.DictReader(f, delimiter=';')
    for row in reader:
        r = {k.strip().lower(): v.strip() for k, v in row.items() if k}
        if r.get("outcome"):
            try:
                r_mult = float(r.get("r_multiple", 0.0))
                score = float(r.get("score", 0.0))
                t_entry = datetime.strptime(r.get("entry_time"), "%Y-%m-%d %H:%M:%S")
                t_exit = datetime.strptime(r.get("exit_time"), "%Y-%m-%d %H:%M:%S")
            except:
                continue
            trades.append({
                "entry_time": t_entry,
                "exit_time": t_exit,
                "setup": r.get("setup", "N/A"),
                "side": r.get("side", "N/A"),
                "grade": r.get("grade", "N/A"),
                "score": score,
                "outcome": r.get("outcome", ""),
                "r_multiple": r_mult,
                "is_win": r.get("outcome") in ("TARGET1", "TARGET2"),
                "is_loss": r.get("outcome") == "STOP"
            })

print("==========================================================================")
print("     SIMULATION DU FILTRE ANTI-DOUBLON / COOLDOWN (MAX 1 TRADE ACTIF)     ")
print("==========================================================================")

# Scenario A: Baseline (All 51 trades as executed)
print(f"Total Trades bruts : {len(trades)}")
total_r_raw = sum(t["r_multiple"] for t in trades)
wins_raw = [t for t in trades if t["is_win"]]
losses_raw = [t for t in trades if t["is_loss"]]
print(f"BRUT -> WR: {len(wins_raw)/len(trades)*100:.1f}% | Net R: {total_r_raw:+0.2f} R | Wins: {len(wins_raw)} | Loss: {len(losses_raw)}")

# Scenario B: Single active trade per direction (no stacking while trade is active)
filtered_trades = []
active_buys = []
active_sells = []

for t in trades:
    # purge closed trades at t["entry_time"]
    active_buys = [x for x in active_buys if x["exit_time"] > t["entry_time"]]
    active_sells = [x for x in active_sells if x["exit_time"] > t["entry_time"]]
    
    if t["side"] == "LONG":
        if len(active_buys) > 0:
            # Duplicate / Stacked buy! Skip!
            continue
        active_buys.append(t)
        filtered_trades.append(t)
    else:
        if len(active_sells) > 0:
            # Duplicate / Stacked sell! Skip!
            continue
        active_sells.append(t)
        filtered_trades.append(t)

print("\n--- RESULTATS AVEC FILTRE ANTI-DOUBLON (1 TRADE ACTIF PAR SENS) ---")
total_r_filt = sum(t["r_multiple"] for t in filtered_trades)
wins_filt = [t for t in filtered_trades if t["is_win"]]
losses_filt = [t for t in filtered_trades if t["is_loss"]]
gross_win_f = sum(t["r_multiple"] for t in wins_filt)
gross_loss_f = abs(sum(t["r_multiple"] for t in losses_filt))
pf_f = gross_win_f / gross_loss_f if gross_loss_f > 0 else float("inf")

print(f"Trades retenus     : {len(filtered_trades)} (Élimination de {len(trades) - len(filtered_trades)} doublons destructeurs)")
print(f"Win Rate           : {len(wins_filt)/len(filtered_trades)*100:.1f}% ({len(wins_filt)} W / {len(losses_filt)} L)")
print(f"Gain Net Total     : {total_r_filt:+0.2f} R")
print(f"Gains Bruts        : +{gross_win_f:+0.2f} R")
print(f"Pertes Brutes      : -{gross_loss_f:+0.2f} R")
print(f"Profit Factor      : {pf_f:0.2f}")

# Group by Date
from collections import defaultdict
by_date_f = defaultdict(list)
for t in filtered_trades:
    d = t["entry_time"].strftime("%Y-%m-%d")
    by_date_f[d].append(t)

print("\n--- Performance par Journée (Filtré) ---")
for d in sorted(by_date_f.keys()):
    d_trades = by_date_f[d]
    d_wins = [t for t in d_trades if t["is_win"]]
    d_losses = [t for t in d_trades if t["is_loss"]]
    d_r = sum(t["r_multiple"] for t in d_trades)
    d_wr = (len(d_wins) / len(d_trades)) * 100
    print(f"  * {d} : {len(d_trades):2d} trades | WR {d_wr:5.1f}% | Net R: {d_r:+6.2f} R | Wins: {len(d_wins)} | Losses: {len(d_losses)}")

print("==========================================================================")
