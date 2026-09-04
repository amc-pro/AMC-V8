import sqlite3, os

db_path = os.path.expanduser('~/Documents/NinjaTrader 8/db/amc_volume_profile.db')
conn = sqlite3.connect(db_path)
cur = conn.cursor()

# Check all profiles for MNQ and NQ
cur.execute("SELECT id, symbol, profile_type, period_key, period_start_utc, period_end_utc, vwap, vwap_sd1_lower, vwap_sd2_lower, vwap_sd3_lower FROM vp_profiles WHERE symbol IN ('MNQ', 'NQ') ORDER BY profile_type, period_start_utc DESC;")
rows = cur.fetchall()

print("PROFILES NQ / MNQ DANS LA BASE SQLITE:")
print("-" * 110)
for r in rows:
    pid, sym, ptype, pkey, pstart, pend, vwap, sd1, sd2, sd3 = r
    print(f"ID={pid:3d} | {sym:4s} | {ptype:8s} | {pkey:45s} | VWAP={str(vwap):15s} | SD-2={str(sd2):15s}")
