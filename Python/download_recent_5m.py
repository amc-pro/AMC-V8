import yfinance as yf
import pandas as pd
from datetime import datetime, timedelta
import os

def download_recent():
    tickers = {"NQ": "NQ=F", "ES": "ES=F", "CL": "CL=F", "GC": "GC=F"}
    data_dir = "/home/ubuntu/AMC-V8/historical_data_recent"
    if not os.path.exists(data_dir): os.makedirs(data_dir)
    
    # 60 derniers jours pour le 5m
    end_date = datetime.now()
    start_date = end_date - timedelta(days=59)
    
    for name, symbol in tickers.items():
        try:
            print(f"Téléchargement 5m pour {name}...")
            data = yf.download(symbol, start=start_date.strftime("%Y-%m-%d"), 
                              end=end_date.strftime("%Y-%m-%d"), interval="5m")
            if not data.empty:
                data.to_csv(os.path.join(data_dir, f"{name}_5min_recent.csv"))
                print(f"  Succès: {len(data)} lignes")
        except Exception as e:
            print(f"  Erreur {name}: {e}")

if __name__ == "__main__":
    download_recent()
