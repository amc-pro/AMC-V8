# Test with real stop loss (1.75 ATR ~ 20-30 pts) vs artificial 3 pts stop cap

with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv", "r", encoding="utf-8", errors="ignore") as f:
    lines = f.readlines()

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
print("     ANALYSE DU STOP LOSS : 3.0 POINTS (BUG CAP PIPS) vs 20-30 POINTS    ")
print("==========================================================================")

time_keys = sorted(list(bars.keys()))

# Let's test the trades on 19 Aug with real stops (1.75 ATR or 20-30 pts)
# For example: 01:26 SHORT, 16:38 SHORT, 16:56 LONG, 18:46 LONG, 21:08 SHORT
test_trades = [
    {"time": "01:26", "side": "SHORT", "entry": 29557.75, "t1": 29525.0},
    {"time": "16:38", "side": "SHORT", "entry": 29611.50, "t1": 29470.0},
    {"time": "16:56", "side": "LONG", "entry": 29503.50, "t1": 29580.0},
    {"time": "18:46", "side": "LONG", "entry": 29605.00, "t1": 29650.0},
    {"time": "21:08", "side": "SHORT", "entry": 29514.50, "t1": 29470.0},
]

for tr in test_trades:
    t = tr["time"]
    if t not in time_keys: continue
    idx = time_keys.index(t)
    entry = tr["entry"]
    is_buy = tr["side"] == "LONG"
    
    # 1. With 3 points stop
    stop_3pt = entry - 3.0 if is_buy else entry + 3.0
    # 2. With 25 points stop (1.75 ATR)
    stop_25pt = entry - 25.0 if is_buy else entry + 25.0
    target = tr["t1"]
    
    # Simulate
    outcome_3pt = "OPEN"
    outcome_25pt = "OPEN"
    
    for fut_t in time_keys[idx+1:idx+30]:
        b = bars[fut_t]
        # Check 3pt
        if outcome_3pt == "OPEN":
            if is_buy and b["L"] <= stop_3pt: outcome_3pt = f"STOP at {fut_t}"
            elif (not is_buy) and b["H"] >= stop_3pt: outcome_3pt = f"STOP at {fut_t}"
            elif is_buy and b["H"] >= target: outcome_3pt = f"TP at {fut_t}"
            elif (not is_buy) and b["L"] <= target: outcome_3pt = f"TP at {fut_t}"
            
        # Check 25pt
        if outcome_25pt == "OPEN":
            if is_buy and b["L"] <= stop_25pt: outcome_25pt = f"STOP at {fut_t}"
            elif (not is_buy) and b["H"] >= stop_25pt: outcome_25pt = f"STOP at {fut_t}"
            elif is_buy and b["H"] >= target: outcome_25pt = f"TP at {fut_t}"
            elif (not is_buy) and b["L"] <= target: outcome_25pt = f"TP at {fut_t}"
            
    print(f"Trade [{t}] {tr['side']:5s} Entry={entry:8.2f} | 3-pt Stop Outcome: {outcome_3pt:15s} | 25-pt Stop Outcome: {outcome_25pt:15s}")
