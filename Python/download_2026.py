import yfinance as yf
import pandas as pd
from datetime import datetime, timedelta
import os
import time

def download_2026_data():
    tickers = {"NQ": "NQ=F", "ES": "ES=F", "CL": "CL=F", "GC": "GC=F"}
    data_dir = "/home/ubuntu/AMC-V8/historical_data_2026"
    if not os.path.exists(data_dir): os.makedirs(data_dir)
    
    # Blocs de 7 jours pour le 1m (limite Yahoo)
    # De Janvier 2026 à aujourd'hui (20 Août 2026)
    start_date = datetime(2026, 1, 1)
    end_date = datetime(2026, 8, 20)
    
    for name, symbol in tickers.items():
        print(f"Téléchargement de {name} pour 2026...")
        all_data = []
        current_start = start_date
        
        while current_start < end_date:
            current_end = min(current_start + timedelta(days=7), end_date)
            s_str = current_start.strftime("%Y-%m-%d")
            e_str = current_end.strftime("%Y-%m-%d")
            
            try:
                # yfinance 1m est limité aux 30 derniers jours pour certains tickers, 
                # mais voyons si on peut remonter plus loin ou si on doit passer en 2m/5m
                # pour le long terme et garder le 1m pour le récent.
                data = yf.download(symbol, start=s_str, end=e_str, interval="1m")
                if not data.empty:
                    all_data.append(data)
                    print(f"  {s_str} -> {e_str} : {len(data)} lignes")
                else:
                    print(f"  {s_str} -> {e_str} : Vide (limite atteinte ?)")
                    # Si vide, on tente le 2m ou 5m pour cette période ancienne
                    data_5m = yf.download(symbol, start=s_str, end=e_str, interval="5m")
                    if not data_5m.empty:
                        print(f"  {s_str} -> {e_str} : Fallback 5m réussi ({len(data_5m)} lignes)")
                        all_data.append(data_5m)
            except Exception as e:
                print(f"  Erreur {s_str}: {e}")
            
            current_start = current_end
            time.sleep(1) # Respecter l'API
            
        if all_data:
            final_df = pd.concat(all_data)
            final_df.to_csv(os.path.join(data_dir, f"{name}_2026_mixed.csv"))
            print(f"Terminé pour {name}")

if __name__ == "__main__":
    download_2026_data()
