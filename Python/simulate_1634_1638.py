with open("c:/AMC-Pro/AMC-V8/historical-data/mnq_full_advanced_data_20260819.md", "r", encoding="utf-8") as f:
    lines = f.readlines()

bars = {}
for line in lines:
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

print("--- SIMULATION DU SIGNAL 16:34 LONG ---")
entry_1634 = 29697.75
# Stop standard ATR / structure = 29694.75 (ou 1.75 ATR = ~29665)
time_keys = sorted(list(bars.keys()))
idx_1634 = time_keys.index("16:34")
print(f"Entree LONG a 16:34 a {entry_1634}")
for t in time_keys[idx_1634+1:idx_1634+10]:
    b = bars[t]
    print(f"  Bar {t} : Low = {b['L']} | High = {b['H']} | Close = {b['C']}")

print("\n--- SIMULATION DU SIGNAL 16:38 SHORT ---")
entry_1638 = 29611.50
idx_1638 = time_keys.index("16:38")
print(f"Entree SHORT a 16:38 a {entry_1638}")
for t in time_keys[idx_1638+1:idx_1638+15]:
    b = bars[t]
    print(f"  Bar {t} : Low = {b['L']} | High = {b['H']} | Close = {b['C']}")
