import sqlite3
import os

db_path = os.path.expanduser('~/Documents/NinjaTrader 8/db/amc_volume_profile.db')
if not os.path.exists(db_path):
    print("Database does not exist at", db_path)
    exit(1)

conn = sqlite3.connect(db_path)
cur = conn.cursor()

cur.execute("SELECT name FROM sqlite_master WHERE type='table'")
tables = cur.fetchall()
print("Tables in DB:", tables)

for (tname,) in tables:
    cur.execute(f"PRAGMA table_info({tname})")
    cols = [col[1] for col in cur.fetchall()]
    cur.execute(f"SELECT COUNT(*) FROM {tname}")
    count = cur.fetchone()[0]
    print(f"Table '{tname}': {count} rows. Columns: {cols}")

print("\n--- vp_profiles breakdown by symbol and profile_type ---")
cur.execute("""
    SELECT symbol, profile_type, COUNT(*), MIN(period_start_utc), MAX(period_end_utc), 
           COUNT(vwap), COUNT(CASE WHEN vwap IS NOT NULL AND vwap > 0 THEN 1 END)
    FROM vp_profiles 
    GROUP BY symbol, profile_type
""")
for r in cur.fetchall():
    sym, ptype, count, min_date, max_date, vwap_count, valid_vwap = r
    print(f"{sym:6s} | {ptype:8s} | count={count:3d} | dates: {min_date[:10]} -> {max_date[:10]} | valid_vwap={valid_vwap}/{count}")

print("\n--- Checking missing symbols or gaps ---")
symbols_in_db = set(r[0] for r in cur.execute("SELECT DISTINCT symbol FROM vp_profiles").fetchall())
print("Symbols present:", symbols_in_db)

conn.close()
