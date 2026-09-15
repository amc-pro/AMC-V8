import math

with open("c:/AMC-Pro/AMC-V8/historical-data/mnq_full_advanced_data_20260819.md", "r", encoding="utf-8") as f:
    lines = f.readlines()

data = []
header = None
for line in lines:
    line = line.strip()
    if not line.startswith("|"):
        continue
    parts = [p.strip() for p in line.split("|")[1:-1]]
    if not parts or parts[0].startswith("---") or parts[0].startswith(":"):
        continue
    if "Heure" in parts[0]:
        header = parts
        continue
    if len(parts) >= 10:
        data.append(parts)

print(f"Total bars parsed: {len(data)}")
print(f"Header: {header}")

# Parse into list of dicts
bars = []
for p in data:
    try:
        b = {
            "time": p[0],
            "open": float(p[1]),
            "high": float(p[2]),
            "low": float(p[3]),
            "close": float(p[4]),
            "volume": int(float(p[5])),
            "vwap": float(p[6]) if p[6] != 'nan' else float('nan'),
            "delta": int(float(p[7])),
            "cvd": int(float(p[8])),
            "delta_z": float(p[9]),
            "imbalance": p[10] if len(p) > 10 else "Normal"
        }
        bars.append(b)
    except Exception as e:
        pass

print(f"Valid numerical bars: {len(bars)}")

# Check news blackout for each bar
# News times in config: 0830, 1000, 1430, 1500 (Note: Exchange time EDT or User time GMT+3?)
# Let's check how NewsTimesCsv works in AMC-Pro:
# In C#:
# int[] times = NewsMinutesOfDay();
# int now = snTime.Hour * 60 + snTime.Minute;
# diff = abs(now - times[i]);
# if (diff <= NewsBlackoutMinutes) return true;
# In Config: NewsTimesCsv = "0830,1000,1430,1500", NewsBlackoutMinutes = 10, NewsHardBlock = true

def is_news_blackout(time_str, news_csv="0830,1000,1430,1500", blackout_mins=10):
    parts = time_str.split(":")
    h, m = int(parts[0]), int(parts[1])
    now_mins = h * 60 + m
    
    for n in news_csv.split(","):
        n = n.strip()
        if not n: continue
        val = int(n)
        nh = val // 100
        nm = val % 100
        nmins = nh * 60 + nm
        diff = abs(now_mins - nmins)
        if diff > 720: diff = 1440 - diff
        if diff <= blackout_mins:
            return True, n
    return False, None

# Let's check Delta Flips
# In C#:
# half = DeltaFlipLookback (default 2)
# need = half * 2 = 4
# start = count - need
# before deltas, after deltas
# mag = Math.Max(1, deltaFlipMagnitudeThreshold) -> in ScalpingPro, what is magnitude threshold?
# In C#, deltaFlipMagnitudeThreshold is computed via percentile or calibration (e.g. 100 to 500)
# Let's check where delta flips occur with various mag thresholds (50, 100, 200, 300)

for mag in [50, 100, 200, 300, 500]:
    flips = []
    for i in range(4, len(bars)):
        sub = bars[i-4:i]
        b_deltas = [x['delta'] for x in sub[0:2]]
        a_deltas = [x['delta'] for x in sub[2:4]]
        
        b_sum = sum(b_deltas)
        a_sum = sum(a_deltas)
        
        pos_before = sum(1 for d in b_deltas if d > 0)
        neg_before = sum(1 for d in b_deltas if d < 0)
        pos_after = sum(1 for d in a_deltas if d > 0)
        neg_after = sum(1 for d in a_deltas if d < 0)
        
        bullish = (pos_after == 2) and (neg_before >= 1) and (b_sum <= -mag) and (a_sum >= mag)
        bearish = (neg_after == 2) and (pos_before >= 1) and (b_sum >= mag) and (a_sum <= -mag)
        
        if bullish:
            flips.append((bars[i]['time'], "BULLISH", b_sum, a_sum, bars[i]['close']))
        if bearish:
            flips.append((bars[i]['time'], "BEARISH", b_sum, a_sum, bars[i]['close']))
            
    print(f"\n--- Magnitude Threshold = {mag} : Found {len(flips)} Delta Flips ---")
    for f in flips[:15]:
        blocked, ne = is_news_blackout(f[0])
        print(f"  Time: {f[0]} | {f[1]:7s} | Before: {f[2]:6d} -> After: {f[3]:6d} | Close: {f[4]} | NewsBlocked: {blocked}")
