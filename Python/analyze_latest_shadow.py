import csv
from collections import defaultdict, Counter

outcomes_path = "c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv"
journal_path = "c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv"

# 1. Parse outcomes
trades = []
with open(outcomes_path, "r", encoding="utf-8-sig", errors="ignore") as f:
    reader = csv.DictReader(f, delimiter=';')
    for row in reader:
        r = {k.strip().lower(): v.strip() for k, v in row.items() if k}
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
print(f"           NOUVELLE ANALYSE DES RESULTATS DANS LE DOSSIER SHADOW          ")
print(f"==========================================================================")
print(f"Total Trades executés dans outcomes : {len(trades)}")

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
    
    print(f"\n--- Metriques Globales Outcomes ---")
    print(f"  * Win Rate         : {win_rate:.2f} % ({len(wins)} Wins / {len(losses)} Losses / {len(timeouts)} Timeouts)")
    print(f"  * Gain Net Total   : {total_r:+.2f} R")
    print(f"  * Gains Bruts      : +{gross_win_r:.2f} R")
    print(f"  * Pertes Brutes    : -{gross_loss_r:.2f} R")
    print(f"  * Profit Factor    : {profit_factor:.2f}")
    print(f"  * Esperance E[R]   : {expectancy:+.2f} R / trade")

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

    print(f"\n--- 15 Derniers Trades Enregistres ---")
    for idx, t in enumerate(trades[-15:], 1):
        status = "WIN " if t["is_win"] else ("LOSS" if t["is_loss"] else "TIME")
        print(f"  {idx:02d}. [{t['entry_time']} -> {t['exit_time']}] {t['setup']:22s} {t['side']:5s} | Score:{t['score']:4.1f} [{t['grade']:8s}] | {status} ({t['r_multiple']:+6.2f} R)")

# 2. Check the risk parameters in journal
print(f"\n==========================================================================")
print(f"        EXAMEN DES DERNIERES LIGNES DU JOURNAL (RISK & STOPS)             ")
print(f"==========================================================================")
with open(journal_path, "r", encoding="utf-8-sig", errors="ignore") as f:
    jlines = f.readlines()

emitted_count = 0
for line in jlines[-25:]:
    p = line.strip().split(";")
    if len(p) < 18: continue
    dt = p[0]
    setup = p[2]
    side = p[3]
    score_emitted = float(p[4]) if p[4] else 0.0
    gated = p[6]
    entry = float(p[14]) if p[14] else 0.0
    stop = float(p[15]) if p[15] else 0.0
    t1 = float(p[16]) if p[16] else 0.0
    rr = float(p[18]) if p[18] else 0.0
    risk_pts = abs(entry - stop)
    
    status = "EMIS" if (score_emitted >= 50.0 and gated == "0") else f"GATED ({p[8]})"
    print(f"[{dt}] {setup:22s} {side:5s} | Entry={entry:8.2f} | Stop={stop:8.2f} (Risk={risk_pts:5.2f} pts = {risk_pts*4:3.0f} tks) | T1={t1:8.2f} (RR={rr:0.2f}) | {status}")
