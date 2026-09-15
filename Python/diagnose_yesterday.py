import pandas as pd
import numpy as np

def diagnose():
    # Chargement avec gestion des lignes d'en-tête multiples de yfinance
    df = pd.read_csv("/home/ubuntu/AMC-V8/tests_and_data/historical_data/NQ_1min.csv", skiprows=[1, 2])
    
    # Renommer la première colonne en 'Datetime'
    df.columns = ['Datetime', 'Close', 'High', 'Low', 'Open', 'Volume']
    
    # Nettoyage et conversion
    df['Datetime'] = pd.to_datetime(df['Datetime'], utc=True)
    # Conversion en GMT+3 (User Timezone)
    df['Datetime'] = df['Datetime'].dt.tz_convert('Etc/GMT-3')
    
    day_df = df[df['Datetime'].dt.strftime('%Y-%m-%d') == '2026-08-19'].copy()
    
    if day_df.empty:
        print("Aucune donnée pour le 19 août.")
        return

    day_df['Range'] = day_df['High'] - day_df['Low']
    day_df['ATR'] = day_df['Range'].rolling(window=14).mean()
    
    print("--- Analyse de la Session US (16:30 - 20:00 GMT+3) ---")
    us_session = day_df[(day_df['Datetime'].dt.hour >= 16) & (day_df['Datetime'].dt.hour <= 20)]
    
    print(f"ATR Moyen US Session: {us_session['ATR'].mean():.2f} pts")
    print(f"ATR Max US Session: {us_session['ATR'].max():.2f} pts")
    
    # Pics de volatilité
    peaks = us_session[us_session['Range'] > us_session['ATR'] * 2.5]
    if not peaks.empty:
        print("\nPics de volatilité détectés (> 2.5x ATR) :")
        for idx, row in peaks.iterrows():
            print(f"  {row['Datetime']} : Range {row['Range']:.2f} (ATR {row['ATR']:.2f})")

    print("\n--- Analyse des trades échoués (Matin) ---")
    # On cherche les bars proches de 01:40 et 02:08 GMT+3
    for h, m in [(1, 40), (2, 8)]:
        trade_bar = day_df[(day_df['Datetime'].dt.hour == h) & (day_df['Datetime'].dt.minute == m)]
        if not trade_bar.empty:
            row = trade_bar.iloc[0]
            atr = row['ATR']
            print(f"Trade à {h:02d}:{m:02d} : ATR = {atr:.2f}, Stop Loss (1.75x) = {atr*1.75:.2f} pts")
            # Vérifier si le prix a bougé contre le trade dans les minutes suivantes
            future = day_df[day_df['Datetime'] > row['Datetime']].head(10)
            if not future.empty:
                max_adverse = future['High'].max() - row['Close'] if row['Close'] > row['Open'] else row['Close'] - future['Low'].min()
                print(f"  Mouvement adverse max dans les 10 min : {max_adverse:.2f} pts")

if __name__ == "__main__":
    diagnose()
