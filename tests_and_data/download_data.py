import yfinance as yf
import pandas as pd
from datetime import datetime, timedelta

def download_futures_data():
    # Liste des tickers Yahoo Finance pour les futurs
    tickers = {
        "NQ": "NQ=F",
        "ES": "ES=F",
        "CL": "CL=F",
        "GC": "GC=F"
    }
    
    # Période : cette semaine (du 17 au 20 août 2026)
    start_date = "2026-08-17"
    end_date = "2026-08-21" # Exclut le 21, donc jusqu'au 20 inclus
    
    data_dir = "/home/ubuntu/AMC-V8/historical_data"
    import os
    if not os.path.exists(data_dir):
        os.makedirs(data_dir)
        
    print(f"Téléchargement des données 1-minute du {start_date} au {end_date}...")
    
    for name, symbol in tickers.items():
        try:
            print(f"Téléchargement de {name} ({symbol})...")
            # Yahoo Finance permet 1m jusqu'à 7 jours
            data = yf.download(symbol, start=start_date, end=end_date, interval="1m")
            
            if not data.empty:
                file_path = os.path.join(data_dir, f"{name}_1min.csv")
                data.to_csv(file_path)
                print(f"Succès : {len(data)} lignes enregistrées dans {file_path}")
            else:
                print(f"Erreur : Aucune donnée trouvée pour {name}")
        except Exception as e:
            print(f"Erreur pour {name} : {e}")

if __name__ == "__main__":
    download_futures_data()
