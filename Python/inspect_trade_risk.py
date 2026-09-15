with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv", "r", encoding="utf-8", errors="ignore") as f:
    lines = f.readlines()

print("--- EXAMEN DES PARAMETRES DE RISK (ENTRY, STOP, T1, RR) SUR LES TRADES DU 19 ET 20 AOUT ---")
count = 0
for line in lines[1:]:
    p = line.strip().split(";")
    if len(p) < 15: continue
    dt = p[0]
    score_emitted = float(p[4]) if p[4] else 0.0
    gated = p[6]
    if score_emitted >= 50.0 and gated == "0":
        count += 1
        t = dt
        setup = p[2]
        side = p[3]
        entry = float(p[14])
        stop = float(p[15])
        t1 = float(p[16]) if p[16] else 0.0
        rr = float(p[18]) if p[18] else 0.0
        risk_pts = abs(entry - stop)
        t1_pts = abs(t1 - entry)
        detail = p[-1]
        print(f"#{count:02d} [{t}] {setup:22s} {side:5s} | Entry={entry:8.2f} | Stop={stop:8.2f} (Risk={risk_pts:4.2f} pts = {risk_pts*4:2.0f} ticks) | T1={t1:8.2f} (Reward={t1_pts:5.2f} pts) | RR={rr:0.2f}")
