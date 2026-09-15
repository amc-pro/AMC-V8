import pandas as pd
import numpy as np

df = pd.read_csv('historical_data_recent/NQ_5min_recent.csv', skiprows=2, names=['Datetime', 'Close', 'High', 'Low', 'Open', 'Volume'])
df = df.dropna()
df['Close'] = pd.to_numeric(df['Close'], errors='coerce')
df['High'] = pd.to_numeric(df['High'], errors='coerce')
df['Low'] = pd.to_numeric(df['Low'], errors='coerce')
df['Open'] = pd.to_numeric(df['Open'], errors='coerce')
df['Volume'] = pd.to_numeric(df['Volume'], errors='coerce')
df = df.dropna()

# Filter for July 2026 (2026-07-01 to 2026-07-31)
# Typical price = (H + L + C) / 3
july = df[(df['Datetime'] >= '2026-07-01') & (df['Datetime'] < '2026-08-01')].copy()
print(f"Total barres 5min en Juillet 2026: {len(july)}")

july['Typical'] = (july['High'] + july['Low'] + july['Close']) / 3.0
july['VolPrice'] = july['Typical'] * july['Volume']

total_vol = july['Volume'].sum()
total_vol_price = july['VolPrice'].sum()
vwap_july = total_vol_price / total_vol

# Standard deviation
variance = np.sum(july['Volume'] * ((july['Typical'] - vwap_july) ** 2)) / total_vol
std_dev = np.sqrt(variance)

sd1_lower = vwap_july - 1.0 * std_dev
sd2_lower = vwap_july - 2.0 * std_dev
sd3_lower = vwap_july - 3.0 * std_dev

sd1_upper = vwap_july + 1.0 * std_dev
sd2_upper = vwap_july + 2.0 * std_dev
sd3_upper = vwap_july + 3.0 * std_dev

print("=" * 110)
print("CALCUL EXACT DU VWAP DU MOIS DE JUILLET 2026 (NQ 5-MIN HISTORIQUE)")
print("=" * 110)
print(f"Volume Total Juillet: {total_vol:,.0f}")
print(f"VWAP Juillet 2026   : {vwap_july:.2f}")
print(f"Ecart-Type (STD DEV): {std_dev:.2f}")
print("-" * 50)
print(f"SD+3 Upper          : {sd3_upper:.2f}")
print(f"SD+2 Upper          : {sd2_upper:.2f}")
print(f"SD+1 Upper          : {sd1_upper:.2f}")
print(f"VWAP                : {vwap_july:.2f}")
print(f"SD-1 Lower          : {sd1_lower:.2f}")
print(f"SD-2 Lower          : {sd2_lower:.2f}")
print(f"SD-3 Lower          : {sd3_lower:.2f}")
print("=" * 110)
