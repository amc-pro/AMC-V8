# Inspect trades on August 20, 2026

with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv", "r", encoding="utf-8-sig", errors="ignore") as f:
    lines = f.readlines()

print("==========================================================================")
print("             DETAIL DES TRADES DU 20 AOUT 2026 (OUTCOMES)                 ")
print("==========================================================================")
for line in lines[1:]:
    p = line.strip().split(";")
    if len(p) < 8: continue
    dt = p[0]
    if "2026-08-20" in dt:
        entry_t = p[0]
        exit_t = p[1]
        setup = p[2]
        side = p[3]
        grade = p[4]
        score = float(p[5]) if p[5] else 0.0
        outcome = p[6]
        r_mult = float(p[7]) if p[7] else 0.0
        print(f"[{entry_t} -> {exit_t}] {setup:22s} {side:5s} | Score:{score:4.1f} [{grade:8s}] => {outcome:10s} ({r_mult:+0.2f} R)")
