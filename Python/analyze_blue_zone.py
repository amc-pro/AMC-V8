# Let's inspect the exact bars around the blue zone (13:00 to 15:30) in mnq_full_advanced_data_20260819.md
with open("c:/AMC-Pro/AMC-V8/historical-data/mnq_full_advanced_data_20260819.md", "r", encoding="utf-8") as f:
    lines = f.readlines()

zone_bars = []
for line in lines:
    if not line.startswith("|"): continue
    parts = [p.strip() for p in line.split("|")[1:-1]]
    if len(parts) < 10 or "Heure" in parts[0] or parts[0].startswith("---"): continue
    time = parts[0]
    if "13:00" <= time <= "15:35":
        zone_bars.append(parts)

print(f"Total bars in zone window (13:00 to 15:35): {len(zone_bars)}")
# Check price range, delta, volume
lows = [float(p[3]) for p in zone_bars]
highs = [float(p[2]) for p in zone_bars]
volumes = [int(float(p[5])) for p in zone_bars]
deltas = [int(float(p[7])) for p in zone_bars]

print(f"Price Min Low: {min(lows)} | Price Max High: {max(highs)}")
print(f"Average Bar Volume: {sum(volumes)/len(volumes):.1f}")
print(f"Average Abs Delta: {sum(abs(d) for d in deltas)/len(deltas):.1f}")

# Print bars where price is touching 29500-29525
touching = [p for p in zone_bars if float(p[3]) <= 29525 and float(p[2]) >= 29500]
print(f"Bars touching 29500-29525: {len(touching)}")
for p in touching[:15]:
    print(f"  {p[0]} | O={p[1]} H={p[2]} L={p[3]} C={p[4]} | Vol={p[5]} | Delta={p[7]} | DeltaZ={p[9]} | Imb={p[10]}")
