import os
import hashlib
import pandas as pd
import numpy as np

def run_reconciliation():
    csv_path = os.path.expanduser("~/Documents/NinjaTrader 8/shadow/swing_trades.csv")
    if not os.path.exists(csv_path):
        print(f"Error: {csv_path} not found")
        return

    with open(csv_path, "rb") as f:
        sha256_hash = hashlib.sha256(f.read()).hexdigest()

    df = pd.read_csv(csv_path)
    closed = df[df["Status"] == "CLOSED"].copy()
    closed["EntryTimeUtc"] = pd.to_datetime(closed["EntryTimeUtc"])
    closed["ExitTimeUtc"] = pd.to_datetime(closed["ExitTimeUtc"])
    closed = closed.sort_values("EntryTimeUtc").reset_index(drop=True)

    print("=== RECONCILIATION DATASET AUDIT ===")
    print("File:", csv_path)
    print("SHA256:", sha256_hash)
    print(f"Total Rows: {len(df):,}")
    print(f"Closed Trades: {len(closed):,}")
    print("\n--- EXIT REASON BREAKDOWN ---")
    exits = closed["ExitReason"].value_counts()
    print(exits)

    print("\n--- TOTAL PNL AND R ---")
    print(f"Total RealizedR: {closed['RealizedR'].sum():.2f} R")
    print(f"Total USD: ${closed['RealizedUSD'].sum():,.2f}")

    # Reconcile with counterfactual
    regime_trades = closed[closed["ExitReason"] == "REGIME_CHANGED"]
    print(f"\nRegime Changed Trades count: {len(regime_trades)}")
    print(f"Regime Changed RealizedR: {regime_trades['RealizedR'].sum():.2f} R")
    print(f"Regime Changed RealizedUSD: ${regime_trades['RealizedUSD'].sum():,.2f}")

    # Natural exits
    natural_trades = closed[closed["ExitReason"] != "REGIME_CHANGED"]
    print(f"\nNatural Exits Trades count: {len(natural_trades)}")
    print(f"Natural Exits RealizedR: {natural_trades['RealizedR'].sum():.2f} R")
    print(f"Natural Exits RealizedUSD: ${natural_trades['RealizedUSD'].sum():,.2f}")
    print(natural_trades["ExitReason"].value_counts())

if __name__ == "__main__":
    run_reconciliation()
