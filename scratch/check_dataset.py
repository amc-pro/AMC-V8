import os
import pandas as pd

path = os.path.expanduser("~/Documents/NinjaTrader 8/shadow/swing_trades.csv")
print("Path:", path)
if not os.path.exists(path):
    print("File not found")
else:
    df = pd.read_csv(path)
    print("Shape:", df.shape)
    print("Columns:", df.columns.tolist())
    if "ExitReason" in df.columns:
        print("\nExitReason counts:")
        print(df["ExitReason"].value_counts())
    if "RealizedR" in df.columns:
        print("\nTotal RealizedR:", df["RealizedR"].sum())
    if "RealizedPnlCurrency" in df.columns:
        print("Total USD:", df["RealizedPnlCurrency"].sum())
    if "Symbol" in df.columns:
        print("\nSymbol counts:")
        print(df["Symbol"].value_counts())
