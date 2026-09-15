# Test GC with anti-duplicate filter

import csv
from datetime import datetime

outcomes_path = "c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv"

trades = []
with open(outcomes_path, "r", encoding="utf-8-sig", errors="ignore") as f:
    reader = csv.DictReader(f, delimiter=';')
    for row in reader:
        r = {k.strip().lower(): (v.strip() if v else "") for k, v in row.items() if k}
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

filtered_trades = []
active_buys = []
active_sells = []

for t in trades:
    active_buys = [x for x in active_buys if x["exit_time"] > t["entry_time"]]
    active_sells = [x for x in active_sells if x["exit_time"] > t["entry_time"]]
    
    if t["side"] == "LONG":
        if len(active_buys) > 0:
            continue
        active_buys.append(t)
        filtered_trades.append(t)
    else:
        if len(active_sells) > 0:
            continue
        active_sells.append(t)
        filtered_trades.append(t)

print("==========================================================================")
print("             PERFORMANCE GC AVEC FILTRE ANTI-DOUBLON ACTIF                ")
print("==========================================================================")
wins_f = [t for t in filtered_trades if t["is_win"]]
losses_f = [t for t in filtered_trades if t["is_loss"]]
total_r_f = sum(t["r_multiple"] for t in filtered_trades)
gross_win_f = sum(t["r_multiple"] for t in wins_f)
gross_loss_f = abs(sum(t["r_multiple"] for t in losses_f))
pf_f = gross_win_f / gross_loss_f if gross_loss_f > 0 else float("inf")

print(f"Trades retenus     : {len(filtered_trades)} / {len(trades)}")
print(f"Win Rate           : {len(wins_f)/len(filtered_trades)*100:.2f} % ({len(wins_f)} W / {len(losses_f)} L)")
print(f"Gain Net Total     : {total_r_f:+.2f} R")
print(f"Profit Factor      : {pf_f:.2f}")
print(f"Esperance E[R]     : {total_r_f/len(filtered_trades):+.2f} R / trade")

# By date
from collections import defaultdict
by_date = defaultdict(list)
for t in filtered_trades:
    d = t["entry_time"].strftime("%Y-%m-%d")
    by_date[d].append(t)

print("\n--- Par Journée ---")
for d in sorted(by_date.keys()):
    d_trades = by_date[d]
    d_w = [t for t in d_trades if t["is_win"]]
    d_l = [t for t in d_trades if t["is_loss"]]
    d_r = sum(t["r_multiple"] for t in d_trades)
    print(f"  * {d} : {len(d_trades):2d} trades | WR {len(d_w)/len(d_trades)*100:5.1f}% | Net R: {d_r:+6.2f} R")
