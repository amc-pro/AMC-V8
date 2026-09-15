
import pandas as pd
import datetime

def is_news_blackout(dt, news_times_hhmm, window_mins):
    now_mins = dt.hour * 60 + dt.minute
    for hhmm in news_times_hhmm:
        hh = hhmm // 100
        mm = hhmm % 100
        news_mins = hh * 60 + mm
        diff = abs(now_mins - news_mins)
        if diff > 720: diff = 1440 - diff
        if diff <= window_mins:
            return True, hhmm
    return False, None

def audit():
    # MNQ 1min data
    df = pd.read_csv('/home/ubuntu/AMC-V8/tests_and_data/historical_data/MNQ_1min.csv', sep=';')
    df['Time'] = pd.to_datetime(df['Time'])
    
    # Target window: 2026-08-19 15:20 to 15:40
    target_date = '2026-08-19'
    sub = df[df['Time'].dt.strftime('%Y-%m-%d') == target_date].copy()
    
    news_times = [830, 1000, 1430, 1500] # Exchange Time (EDT)
    # The data is in -04:00 (EDT).
    # 15:30 GMT+3 = 08:30 EDT.
    # The Time column in CSV is already in -04:00.
    
    print(f"--- Audit MNQ 15:20-15:40 (EDT) for {target_date} ---")
    
    for idx, row in sub.iterrows():
        t = row['Time']
        if not (t.hour == 8 and t.minute >= 20 and t.minute <= 40):
            continue
            
        blackout, event = is_news_blackout(t, news_times, 10)
        
        range_ticks = (row['High'] - row['Low']) / 0.25
        atr_veto = range_ticks > 140
        
        # Check for potential Finished Auction (wick > 40%)
        bar_range = row['High'] - row['Low']
        wick_long = (min(row['Open'], row['Close']) - row['Low']) / bar_range if bar_range > 0 else 0
        
        status = "BLOCKED (News)" if blackout else "OK"
        if atr_veto: status += " | BLOCKED (ATR Volatility)"
        
        print(f"{t.strftime('%H:%M')} | Close: {row['Close']} | Wick: {wick_long*100:.1f}% | ATR_Ticks: {range_ticks:.0f} | Status: {status}")

if __name__ == "__main__":
    audit()
