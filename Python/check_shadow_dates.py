with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv", "r", encoding="utf-8", errors="ignore") as f:
    lines = f.readlines()

header = lines[0].strip().split(";")

print(f"Header: {header[:15]}")

entries_18 = []
entries_19 = []
other_dates = set()

for line in lines[1:]:
    parts = line.strip().split(";")
    if not parts or len(parts) < 5: continue
    dt = parts[0]
    date = dt.split()[0] if " " in dt else dt
    other_dates.add(date)
    if "2026-08-18" in dt:
        entries_18.append(parts)
    elif "2026-08-19" in dt:
        entries_19.append(parts)

print(f"All dates in journal: {sorted(list(other_dates))}")
print(f"Total entries on 2026-08-18: {len(entries_18)}")
print(f"Total entries on 2026-08-19: {len(entries_19)}")

print("\n--- 2026-08-18 Entries ---")
for e in entries_18:
    print(f"Time: {e[0]} | Setup: {e[2]:20s} | Side: {e[3]:5s} | ScoreEmitted: {e[4]:5s} | ScoreRaw: {e[5]:5s} | Gated: {e[6]} | GateFailed: {e[8]:18s} | Grade: {e[7]}")

print("\n--- 2026-08-19 Entries ---")
for e in entries_19:
    print(f"Time: {e[0]} | Setup: {e[2]:20s} | Side: {e[3]:5s} | ScoreEmitted: {e[4]:5s} | ScoreRaw: {e[5]:5s} | Gated: {e[6]} | GateFailed: {e[8]:18s} | Grade: {e[7]}")
