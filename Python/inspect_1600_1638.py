# Detailed inspection of 16:00 to 16:38 on 2026-08-19

with open("c:/AMC-Pro/AMC-V8/historical-data/mnq_full_advanced_data_20260819.md", "r", encoding="utf-8") as f:
    data_lines = f.readlines()

with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv", "r", encoding="utf-8", errors="ignore") as f:
    shadow_lines = f.readlines()

print("==========================================================================")
print("             1. BARS DATA BETWEEN 16:00 AND 16:38 (19 AUG 2026)")
print("==========================================================================")
for line in data_lines:
    if not line.startswith("|"): continue
    p = [x.strip() for x in line.split("|")[1:-1]]
    if len(p) < 10 or "Heure" in p[0] or p[0].startswith("---"): continue
    t = p[0]
    if "16:00" <= t <= "16:38":
        print(f"{t} | O:{p[1]:<8} H:{p[2]:<8} L:{p[3]:<8} C:{p[4]:<8} | Vol:{p[5]:<6} | Delta:{p[7]:<7} | CVD:{p[8]:<7} | Z:{p[9]:<5} | {p[10]}")

print("\n==========================================================================")
print("       2. SHADOW CANDIDATES LOGGED BETWEEN 16:00 AND 16:38 (19 AUG 2026)")
print("==========================================================================")
count = 0
for line in shadow_lines[1:]:
    p = line.strip().split(";")
    if len(p) < 10: continue
    dt = p[0]
    if "2026-08-19" in dt:
        t = dt.split()[1] if " " in dt else dt
        if "16:00" <= t <= "16:38":
            count += 1
            setup = p[2]
            side = p[3]
            emitted = p[4]
            raw = p[5]
            gated = p[6]
            gate_failed = p[8]
            grade = p[7]
            n1, n2, n3, n4, pen = p[9], p[10], p[11], p[12], p[13]
            entry, stop, rr = p[14], p[15], p[18]
            detail = p[-1]
            print(f"Candidate #{count} [{t}] {setup} ({side})")
            print(f"  -> Emitted Score: {emitted} (Raw: {raw}) | Gated: {gated} | GateFailed: '{gate_failed}' | Grade: {grade}")
            print(f"  -> Scores: N1={n1}/30, N2={n2}/30, N3={n3}/25, N4={n4}/15, Pen={pen} | Entry={entry}, Stop={stop}, RR={rr}")
            print(f"  -> Detailed Notes:")
            for d in detail.split(" | "):
                print(f"       * {d}")
            print("-" * 75)

if count == 0:
    print("NO CANDIDATE IN SHADOW JOURNAL BETWEEN 16:00 AND 16:38")
