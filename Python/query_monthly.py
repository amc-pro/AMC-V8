import sqlite3, os
db_path = os.path.expanduser('~/Documents/NinjaTrader 8/db/amc_volume_profile.db')
conn = sqlite3.connect(db_path)
cur = conn.cursor()
cur.execute("SELECT symbol, profile_type, period_key, vwap, vwap_sd1_lower, vwap_sd2_lower, vwap_sd3_lower, vwap_sd1_upper, vwap_sd2_upper, vwap_sd3_upper FROM vp_profiles WHERE profile_type = 'MONTHLY';")
rows = cur.fetchall()
print(f"Total MONTHLY profiles in SQLite: {len(rows)}")
for r in rows:
    print("  ", r)
