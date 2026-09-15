with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv", "r", encoding="utf-8", errors="ignore") as f:
    lines = f.readlines()

print("=== CANDIDATES ON 2026-08-19 BETWEEN 08:00 and 18:00 ===")
for line in lines[1:]:
    parts = line.strip().split(";")
    if len(parts) < 10: continue
    dt = parts[0]
    if "2026-08-19" in dt:
        time = dt.split()[1] if " " in dt else dt
        h = int(time.split(":")[0])
        if 8 <= h <= 18:
            setup = parts[2]
            side = parts[3]
            emitted = parts[4]
            raw = parts[5]
            gated = parts[6]
            grade = parts[7]
            gate_failed = parts[8]
            n1 = parts[9]
            n2 = parts[10]
            n3 = parts[11]
            n4 = parts[12]
            pen = parts[13]
            entry = parts[14]
            stop = parts[15]
            detail = parts[-1]
            
            print(f"[{time}] {setup:24s} | {side:5s} | Emitted={emitted:4s} (Raw={raw:4s}) | Gated={gated} | GateFailed={gate_failed:18s} | Grade={grade}")
            print(f"    Scores: N1={n1} N2={n2} N3={n3} N4={n4} Pen={pen} | Entry={entry} Stop={stop}")
            print(f"    Detail: {detail}")
            print("-" * 110)
