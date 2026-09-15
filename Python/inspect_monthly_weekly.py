import sqlite3, os

db_path = os.path.expanduser('~/Documents/NinjaTrader 8/db/amc_volume_profile.db')
conn = sqlite3.connect(db_path)
cur = conn.cursor()

print("=" * 110)
print(f"DATABASE SQLITE: {db_path}")
print("=" * 110)

cur.execute("SELECT id, symbol, profile_type, period_key, poc, vah, val, vwap, vwap_sd1_lower, vwap_sd2_lower, vwap_sd3_lower, vwap_sd1_upper, vwap_sd2_upper, vwap_sd3_upper FROM vp_profiles WHERE profile_type IN ('MONTHLY', 'WEEKLY') ORDER BY id DESC LIMIT 20;")
rows = cur.fetchall()

print(f"Derniers profils MONTHLY et WEEKLY dans la base ({len(rows)} trouvés):\n")
for r in rows:
    pid, sym, ptype, pkey, poc, vah, val, vwap, sd1_l, sd2_l, sd3_l, sd1_u, sd2_u, sd3_u = r
    print(f"ID={pid:3d} | [{sym:5s}] {ptype:8s} | Key: {pkey}")
    print(f"   POC={poc} VAH={vah} VAL={val} | VWAP={vwap}")
    print(f"   Bandes: SD-1={sd1_l} SD-2={sd2_l} SD-3={sd3_l} | SD+1={sd1_u} SD+2={sd2_u} SD+3={sd3_u}")
    print("-" * 100)
