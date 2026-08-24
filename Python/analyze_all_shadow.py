with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv", "r", encoding="utf-8", errors="ignore") as f:
    header = f.readline().strip().split(";")

print("Header fields:", header)

rows = []
with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv", "r", encoding="utf-8", errors="ignore") as f:
    for line in f.readlines()[1:]:
        p = line.strip().split(";")
        if len(p) >= 10:
            rows.append(p)

from collections import Counter
by_date_total = Counter()
by_date_emitted = Counter()
reasons = Counter()
setups_total = Counter()
setups_emitted = Counter()

# Header: [0:Time, 1:Instrument, 2:Setup, 3:Direction, 4:ScoreEmitted, 5:ScoreRaw, 6:Gated, 7:Grade, 8:GateFailed, 9:N1, 10:N2, 11:N3, 12:N4, 13:Pen, 14:Entry, 15:Stop, 16:T1, 17:T2, 18:RR, 19:DayType, ...]

for p in rows:
    dt = p[0]
    date = dt.split()[0]
    by_date_total[date] += 1
    
    setup = p[2]
    score_emitted = float(p[4]) if p[4] else 0.0
    gated = p[6]
    gate_failed = p[8]
    
    setups_total[setup] += 1
    
    if gated == "0" and score_emitted > 0:
        by_date_emitted[date] += 1
        setups_emitted[setup] += 1
    else:
        reasons[gate_failed if gate_failed else "UNKNOWN_GATE"] += 1

print("\n--- Summary By Date (Total Candidates vs Emitted) ---")
for d in sorted(by_date_total.keys()):
    print(f"Date: {d} | Candidates: {by_date_total[d]:3d} | Emitted: {by_date_emitted[d]:2d}")

print("\n--- Most Common Gate Failures (Rejections) ---")
for reason, count in reasons.most_common(15):
    print(f"  {reason:25s} : {count:4d} ({count/len(rows)*100:.1f}%)")

print("\n--- Candidates by Setup Type ---")
for s, c in setups_total.most_common(10):
    em = setups_emitted[s]
    print(f"  {s:26s} : Total={c:4d} | Emitted={em:3d} ({em/c*100:.1f}%)")
