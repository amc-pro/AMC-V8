import yfinance as yf
import pandas as pd
import os
from datetime import datetime, timedelta

def download_mnq_data():
    target_dir = "/home/ubuntu/AMC-V8/tests_and_data/historical_data"
    long_term_dir = "/home/ubuntu/AMC-V8/tests_and_data/long_term_data"
    os.makedirs(target_dir, exist_ok=True)
    os.makedirs(long_term_dir, exist_ok=True)

    print("Téléchargement des données MNQ 1-minute (7 derniers jours)...")
    # yfinance limit for 1m is 7 days (or 30 if you use interval='1m' and period='1mo' sometimes it works, but 7 is safer)
    try:
        mnq_1m = yf.download("MNQ=F", period="7d", interval="1m")
        if not mnq_1m.empty:
            # Flatten multi-index if exists
            if isinstance(mnq_1m.columns, pd.MultiIndex):
                mnq_1m.columns = mnq_1m.columns.get_level_values(0)
            
            # Format for NinjaTrader: Time;Close;High;Low;Open;Volume
            mnq_1m_export = mnq_1m[['Close', 'High', 'Low', 'Open', 'Volume']].copy()
            mnq_1m_export.index.name = 'Time'
            mnq_1m_export.to_csv(os.path.join(target_dir, "MNQ_1min.csv"), sep=';')
            print(f"MNQ 1min sauvegardé: {len(mnq_1m_export)} lignes")
    except Exception as e:
        print(f"Erreur MNQ 1m: {e}")

    print("Téléchargement des données MNQ 5-minutes (60 derniers jours)...")
    try:
        mnq_5m = yf.download("MNQ=F", period="60d", interval="5m")
        if not mnq_5m.empty:
            if isinstance(mnq_5m.columns, pd.MultiIndex):
                mnq_5m.columns = mnq_5m.columns.get_level_values(0)
            
            mnq_5m_export = mnq_5m[['Close', 'High', 'Low', 'Open', 'Volume']].copy()
            mnq_5m_export.index.name = 'Time'
            mnq_5m_export.to_csv(os.path.join(long_term_dir, "MNQ_5min_recent.csv"), sep=';')
            print(f"MNQ 5min sauvegardé: {len(mnq_5m_export)} lignes")
    except Exception as e:
        print(f"Erreur MNQ 5m: {e}")

if __name__ == "__main__":
    download_mnq_data()
