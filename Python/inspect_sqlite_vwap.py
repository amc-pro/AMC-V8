import sqlite3, os

db_path = os.path.expanduser('~/Documents/NinjaTrader 8/db/amc_volume_profile.db')
conn = sqlite3.connect(db_path)
cur = conn.cursor()

print("=" * 110)
print(f"DATABASE SQLITE: {db_path} ({os.path.getsize(db_path)} bytes)")
print("=" * 110)

cur.execute("SELECT symbol, profile_type, period_key, period_start_utc, period_end_utc, poc, vah, val, vwap, vwap_std_dev, vwap_sd1_lower, vwap_sd2_lower, vwap_sd3_lower, vwap_sd1_upper, vwap_sd2_upper, vwap_sd3_upper FROM vp_profiles ORDER BY id ASC;")
rows = cur.fetchall()

print(f"Total profiles enregistrés: {len(rows)}\n")

for r in rows:
    sym, ptype, pkey, pstart, pend, poc, vah, val, vwap, std, sd1_l, sd2_l, sd3_l, sd1_u, sd2_u, sd3_u = r
    print(f"[{sym:5s}] {ptype:8s} | Key: {pkey:50s}")
    print(f"   Dates: {pstart} -> {pend}")
    print(f"   VA: POC={poc} VAH={vah} VAL={val}")
    print(f"   VWAP: {vwap} | STD_DEV: {std}")
    print(f"   Lower Bands: SD-1={sd1_l} | SD-2={sd2_l} | SD-3={sd3_l}")
    print(f"   Upper Bands: SD+1={sd1_u} | SD+2={sd2_u} | SD+3={sd3_u}")
    print("-" * 100)
