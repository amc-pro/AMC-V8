import pandas as pd

df = pd.read_csv('historical_data_recent/MNQ_20260824_1m.csv', skiprows=2, names=['Datetime', 'Close', 'High', 'Low', 'Open', 'Volume'])
df['Low'] = pd.to_numeric(df['Low'], errors='coerce')
df['High'] = pd.to_numeric(df['High'], errors='coerce')
df['Open'] = pd.to_numeric(df['Open'], errors='coerce')
df['Close'] = pd.to_numeric(df['Close'], errors='coerce')
df = df.dropna(subset=['Low'])

min_idx = df['Low'].idxmin()
min_row = df.loc[min_idx]
print("=" * 110)
print(f"PLUS BAS DU JOUR LE 24/08: Low = {min_row['Low']} à {min_row['Datetime']}")
print("=" * 110)

print("\n--- DETAIL DES BARRES 1M (16H40 A 17H30) ---")
for idx, r in df.iterrows():
    t = str(r['Datetime'])
    if '16:40' <= t[11:16] <= '17:30':
        print(f"[{t}] Open={r['Open']:9.2f} High={r['High']:9.2f} Low={r['Low']:9.2f} Close={r['Close']:9.2f} Vol={r['Volume']}")
