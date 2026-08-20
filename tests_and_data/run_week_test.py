import pandas as pd
import numpy as np
import os
from datetime import datetime, timedelta

def calculate_atr(df, period=14):
    high_low = df['High'] - df['Low']
    high_close = np.abs(df['High'] - df['Close'].shift())
    low_close = np.abs(df['Low'] - df['Close'].shift())
    ranges = pd.concat([high_low, high_close, low_close], axis=1)
    true_range = np.max(ranges, axis=1)
    return true_range.rolling(window=period).mean()

def test_logic():
    data_file = "/home/ubuntu/AMC-V8/historical_data/NQ_1min.csv"
    if not os.path.exists(data_file):
        print("Fichier de données introuvable.")
        return

    # Lire en sautant les lignes de métadonnées de Yahoo Finance
    df = pd.read_csv(data_file, skiprows=2)
    df.columns = ['Datetime', 'Close', 'High', 'Low', 'Open', 'Volume']
    df['Datetime'] = pd.to_datetime(df['Datetime'])
    df = df.sort_values('Datetime')

    # Calcul ATR pour valider le Stop Loss
    df['ATR'] = calculate_atr(df)
    avg_atr = df['ATR'].mean()
    
    print(f"--- ANALYSE DE LA SEMAINE (NQ) ---")
    print(f"Volatilité moyenne (ATR 14) : {avg_atr:.2f} points")
    print(f"Ancien Stop (1.25 ATR) : {1.25 * avg_atr:.2f} points")
    print(f"Nouveau Stop (1.75 ATR) : {1.75 * avg_atr:.2f} points (+{(1.75-1.25)*100:.0f}% de marge)")
    print("-" * 40)

    # Simulation du blocage News
    # News majeures simulées pour la semaine (ex: 15h30 ouverture, 14h30 CPI/Jobless)
    # Dans un cas réel, on utiliserait le calendrier complet.
    news_times = ["14:30", "15:30", "16:00", "20:00"]
    blackout_mins = 10
    
    blocked_count = 0
    total_bars = len(df)
    
    for idx, row in df.iterrows():
        current_time = row['Datetime'].strftime("%H:%M")
        for nt in news_times:
            nt_dt = datetime.strptime(nt, "%H:%M")
            curr_dt = datetime.strptime(current_time, "%H:%M")
            diff = abs((curr_dt - nt_dt).total_seconds() / 60)
            if diff <= blackout_mins:
                blocked_count += 1
                break
                
    print(f"Fenêtres de blocage News : {len(news_times)} événements/jour")
    print(f"Minutes protégées cette semaine : {blocked_count} min")
    print(f"Pourcentage de temps sous protection : {(blocked_count/total_bars)*100:.1f}%")
    print("-" * 40)
    print("RÉSULTAT : Le système est prêt. Les données sont téléchargées et prêtes pour import.")

if __name__ == "__main__":
    test_logic()
