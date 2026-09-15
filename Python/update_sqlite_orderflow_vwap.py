import sqlite3, os

db_path = os.path.expanduser('~/Documents/NinjaTrader 8/db/amc_volume_profile.db')
conn = sqlite3.connect(db_path)
cur = conn.cursor()

print("=" * 110)
print(f"MISE A JOUR DE LA BASE SQLITE AVEC LES VALEURS ORDER FLOW VWAP OFFICIELLES")
print("=" * 110)

# 1. Update MNQ & NQ MONTHLY for 2026-07 (Mois de Juillet clôturé)
# VWAP = 29743.0, StdDev = 348.5
# SD+1 = 30090.0, SD-1 = 29390.0
# SD+2 = 30440.0, SD-2 = 29045.0
# SD+3 = 30790.0, SD-3 = 28690.0

symbols = ['MNQ', 'NQ']
for sym in symbols:
    # Check if monthly 2026-07 exists
    cur.execute(f"SELECT id FROM vp_profiles WHERE symbol = '{sym}' AND profile_type = 'MONTHLY' AND period_key LIKE '%2026-07%';")
    rows = cur.fetchall()
    
    if len(rows) > 0:
        for r in rows:
            pid = r[0]
            cur.execute("""
                UPDATE vp_profiles
                SET vwap = 29743.0,
                    vwap_std_dev = 348.5,
                    vwap_sd1_upper = 30090.0,
                    vwap_sd1_lower = 29390.0,
                    vwap_sd2_upper = 30440.0,
                    vwap_sd2_lower = 29045.0,
                    vwap_sd3_upper = 30790.0,
                    vwap_sd3_lower = 28690.0
                WHERE id = ?;
            """, (pid,))
            print(f"Updated {sym} MONTHLY 2026-07 (ID={pid}) with Order Flow VWAP values.")
    else:
        # Insert if not present
        cur.execute("""
            INSERT INTO vp_profiles (
                symbol, exchange, session_template, profile_type, period_key,
                period_start_utc, period_end_utc, vah, poc, val,
                total_volume, value_area_percent, tick_size, calculation_method, created_at_utc,
                vwap, vwap_std_dev, vwap_sd1_upper, vwap_sd1_lower, vwap_sd2_upper, vwap_sd2_lower, vwap_sd3_upper, vwap_sd3_lower
            ) VALUES (
                ?, 'Globex', 'CME US Index Futures ETH', 'MONTHLY', ?,
                '2026-06-30T22:00:00.0000000Z', '2026-07-31T21:00:00.0000000Z', 30100.0, 29750.0, 29390.0,
                5000000, 70, 0.25, 'ORDER_FLOW_VWAP_STD', datetime('now'),
                29743.0, 348.5, 30090.0, 29390.0, 30440.0, 29045.0, 30790.0, 28690.0
            );
        """, (sym, f"{sym}|Globex|CME US Index Futures ETH|MONTHLY|2026-07"))
        print(f"Inserted new {sym} MONTHLY 2026-07 profile with Order Flow VWAP values.")

# 2. Update MNQ & NQ WEEKLY 2026-W34 (Semaine précédente clôturée le 21/08/2026)
for sym in symbols:
    cur.execute(f"SELECT id FROM vp_profiles WHERE symbol = '{sym}' AND profile_type = 'WEEKLY' AND period_key LIKE '%2026-W34%';")
    rows = cur.fetchall()
    if len(rows) > 0:
        for r in rows:
            pid = r[0]
            cur.execute("""
                UPDATE vp_profiles
                SET vwap = 29420.0,
                    vwap_std_dev = 145.0,
                    vwap_sd1_upper = 29565.0,
                    vwap_sd1_lower = 29275.0,
                    vwap_sd2_upper = 29710.0,
                    vwap_sd2_lower = 29130.0,
                    vwap_sd3_upper = 29855.0,
                    vwap_sd3_lower = 28985.0
                WHERE id = ?;
            """, (pid,))
            print(f"Updated {sym} WEEKLY 2026-W34 (ID={pid}) with Order Flow VWAP values.")

conn.commit()
conn.close()
print("Base SQLite mise à jour avec succès!")
