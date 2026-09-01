# Test simulated stop loss with real dynamic ATR

with open("c:/AMC-Pro/AMC-V8/historical-data/mnq_full_advanced_data_20260819.md", "r", encoding="utf-8") as f:
    data_lines = f.readlines()

bars = {}
for line in data_lines:
    if not line.startswith("|"): continue
    p = [x.strip() for x in line.split("|")[1:-1]]
    if len(p) < 10 or "Heure" in p[0] or p[0].startswith("---") or p[0].startswith(":"): continue
    try:
        bars[p[0]] = {
            "O": float(p[1]), "H": float(p[2]), "L": float(p[3]), "C": float(p[4]),
            "Vol": int(float(p[5])), "Delta": int(float(p[7]))
        }
    except:
        continue

print("==========================================================================")
print("     SIMULATION DU CALCUL DYNAMIQUE DU STOP LOSS (1.75 ATR + STRUCTURE)   ")
print("==========================================================================")

time_keys = sorted(list(bars.keys()))

# Compute running ATR (14-period)
atr_vals = {}
for idx in range(len(time_keys)):
    t = time_keys[idx]
    if idx < 14:
        atr_vals[t] = 12.0 # default
    else:
        tr_list = []
        for j in range(idx-13, idx+1):
            prev_b = bars[time_keys[j-1]]
            curr_b = bars[time_keys[j]]
            tr = max(curr_b["H"] - curr_b["L"], abs(curr_b["H"] - prev_b["C"]), abs(curr_b["L"] - prev_b["C"]))
            tr_list.append(tr)
        atr_vals[t] = sum(tr_list) / len(tr_list)

# Test key signals
signals = [
    {"time": "01:26", "side": "SHORT", "entry": 29557.75, "target": 29515.0},
    {"time": "16:38", "side": "SHORT", "entry": 29611.50, "target": 29450.0},
    {"time": "16:46", "side": "LONG",  "entry": 29606.50, "target": 29660.0},
    {"time": "16:56", "side": "LONG",  "entry": 29502.75, "target": 29580.0},
    {"time": "18:46", "side": "LONG",  "entry": 29598.25, "target": 29645.0},
    {"time": "21:08", "side": "SHORT", "entry": 29518.00, "target": 29470.0},
]

for s in signals:
    t = s["time"]
    idx = time_keys.index(t)
    atr = atr_vals[t]
    stop_dist = max(atr * 1.75 + 1.5, 3.0) # buffer 6 ticks = 1.5 pts
    entry = s["entry"]
    is_buy = s["side"] == "LONG"
    stop_price = entry - stop_dist if is_buy else entry + stop_dist
    target_price = s["target"]
    
    # Simulate outcome over next 40 bars
    outcome = "TIMEOUT"
    exit_t = ""
    for fut_t in time_keys[idx+1:min(idx+45, len(time_keys))]:
        b = bars[fut_t]
        if is_buy:
            if b["L"] <= stop_price:
                outcome = "STOP LOSS"
                exit_t = fut_t
                break
            elif b["H"] >= target_price:
                outcome = "TARGET PROFIT"
                exit_t = fut_t
                break
        else:
            if b["H"] >= stop_price:
                outcome = "STOP LOSS"
                exit_t = fut_t
                break
            elif b["L"] <= target_price:
                outcome = "TARGET PROFIT"
                exit_t = fut_t
                break
                
    r_pts = abs(entry - stop_price)
    rew_pts = abs(target_price - entry)
    rr = rew_pts / r_pts
    print(f"[{t}] {s['side']:5s} @ {entry:8.2f} | ATR: {atr:4.1f} pts -> Stop: {stop_price:8.2f} (Risque={r_pts:4.1f} pts = {r_pts*4:2.0f} ticks) | T1: {target_price:8.2f} (RR={rr:0.2f}) => {outcome} à {exit_t}")

print("==========================================================================")
