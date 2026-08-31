# Simulation test of the improved ScalpingPro rules on 2026-08-19

with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv", "r", encoding="utf-8", errors="ignore") as f:
    lines = f.readlines()

print("==========================================================================")
print("     SIMULATION DES NOUVELLES REGLES SCALPING PRO SUR LE 19 AOUT 2026     ")
print("==========================================================================")

count = 0
emitted_signals = []

for line in lines[1:]:
    p = line.strip().split(";")
    if len(p) < 10: continue
    dt = p[0]
    if "2026-08-19" in dt:
        t = dt.split()[1] if " " in dt else dt
        setup = p[2]
        side = p[3]
        old_score_emitted = float(p[4]) if p[4] else 0.0
        score_raw = float(p[5]) if p[5] else 0.0
        old_gated = p[6]
        old_gate_failed = p[8]
        grade = p[7]
        n1 = float(p[9]) if p[9] else 0.0
        n2 = float(p[10]) if p[10] else 0.0
        n3 = float(p[11]) if p[11] else 0.0
        n4 = float(p[12]) if p[12] else 0.0
        pen = float(p[13]) if p[13] else 0.0
        entry = float(p[14]) if p[14] else 0.0
        stop = float(p[15]) if p[15] else 0.0
        detail = p[-1]
        
        # New Gate Evaluation:
        is_orderflow = setup in ("DELTA_FLIP", "CUM_DELTA_DIV") or "BREAKOUT" in setup
        
        # Gate N1 (min 6), Gate N2 (min 3), Gate N3 (min 3), Gate N4 (min 2)
        g1 = n1 >= 6.0
        g2 = n2 >= 3.0 or (is_orderflow and n2 >= 1.0)
        g3 = n3 >= 3.0 or is_orderflow
        g4 = n4 >= 2.0 or is_orderflow
        
        # Anti-counter-trend filter:
        # At 16:34, DELTA_FLIP LONG with HTF opposite and N3=0 -> Rejected
        anti_counter_trend = (setup in ("FINISHED_AUCTION", "DELTA_FLIP")) and ("htf=ko" in detail or "HTF non aligne" in detail) and n3 < 10.0 and score_raw < 55.0
        
        gate_failed = ""
        if not g1: gate_failed = "N1_CONTEXTE"
        elif not g2: gate_failed = "N2_LOCALISATION"
        elif not g3: gate_failed = "N3_MICROSTRUCTURE"
        elif not g4: gate_failed = "N4_TRIGGER"
        
        # Recoverable gate check if ScoreRaw >= 50
        is_gated = gate_failed != "" or anti_counter_trend
        if is_gated and score_raw >= 50.0 and not anti_counter_trend:
            is_gated = False
            gate_failed = ""
            
        new_score_emitted = score_raw if not is_gated else 0.0
        
        # New Tier:
        if new_score_emitted >= 65.0:
            new_tier = "TRES FORT"
        elif new_score_emitted >= 50.0:
            new_tier = "FORT"
        elif new_score_emitted >= 45.0:
            new_tier = "MOYEN"
        else:
            new_tier = "AUCUN"
            
        is_emitted = (not is_gated) and (new_score_emitted >= 50.0)
        
        if is_emitted:
            emitted_signals.append({
                "time": t, "setup": setup, "side": side, "score": new_score_emitted,
                "tier": new_tier, "entry": entry, "stop": stop,
                "was_emitted_before": (old_score_emitted >= 50.0 and old_gated == "0")
            })

print(f"Total Signaux Emis avec les nouvelles regles sur le 19 Aout : {len(emitted_signals)}")
print("-" * 80)
for s in emitted_signals:
    status = "[ANCIEN SIGNAL]" if s["was_emitted_before"] else "[NOUVEAU SIGNAL DEVERROUILLE]"
    print(f"[{s['time']}] {s['setup']:24s} ({s['side']:5s}) | Score: {s['score']:0.1f} [{s['tier']:9s}] | Entry: {s['entry']} | {status}")

print("==========================================================================")
