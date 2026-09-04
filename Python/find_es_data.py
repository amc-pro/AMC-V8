import csv
import glob
import os

print("--- RECHERCHE DE DONNEES ET TRADES SUR ES / MES ---")

# Check all csv files in workspace
csv_files = glob.glob("c:/AMC-Pro/AMC-V8/**/*.csv", recursive=True)
for cf in csv_files:
    try:
        with open(cf, "r", encoding="utf-8-sig", errors="ignore") as f:
            first_lines = [f.readline() for _ in range(5)]
            content = "".join(first_lines)
            if "ES" in content or "MES" in content or "ES 09" in content or "ES 12" in content:
                print(f"Trouvé mention ES dans {cf}")
    except Exception as e:
        pass

# Check shadow journal
with open("c:/AMC-Pro/AMC-V8/shadow/AuctionMarketCorePro_journal_sniper.csv", "r", encoding="utf-8-sig", errors="ignore") as f:
    reader = csv.DictReader(f, delimiter=';')
    es_rows = [r for r in reader if "ES" in (r.get("instrument") or "").upper()]

print(f"Lignes ES dans shadow journal: {len(es_rows)}")
