import datetime

def parse_time_to_minutes(hhmm_str):
    hh = int(hhmm_str[:2])
    mm = int(hhmm_str[2:])
    return hh * 60 + mm

def is_news_blackout(current_time_str, news_times_csv, blackout_minutes=10):
    # current_time_str format "HH:MM"
    h, m = map(int, current_time_str.split(":"))
    now_minutes = h * 60 + m
    
    parts = news_times_csv.replace(';', ',').split(',')
    for part in parts:
        part = part.strip()
        if not part:
            continue
        try:
            news_hhmm = int(part)
            news_minutes = (news_hhmm // 100) * 60 + (news_hhmm % 100)
            diff = abs(now_minutes - news_minutes)
            if diff <= blackout_minutes:
                return True, part
        except:
            continue
    return False, None

def run_simulation():
    # Horaires des news d'hier (19 août 2026) en GMT+3 : 0900 (CPI), 2100 (FOMC)
    news_csv = "0900,2100"
    blackout_mins = 10
    
    # Test de différents moments de la journée d'hier
    test_times = [
        "08:45", # Normal (avant news 09:00)
        "08:55", # Dans la fenêtre de black-out (-5 min)
        "09:00", # Heure exacte de la news
        "09:08", # Dans la fenêtre de black-out (+8 min)
        "09:15", # Sorti de la fenêtre
        "14:30", # Heure calme
        "20:52", # Dans la fenêtre de black-out FOMC 21:00 (-8 min)
        "21:03", # Dans la fenêtre FOMC (+3 min)
        "22:00"  # Soirée
    ]
    
    print(f"Simulation du blocage des news (Blackout = {blackout_mins} min, News = {news_csv}) :")
    print("-" * 60)
    for t in test_times:
        blocked, matched_news = is_news_blackout(t, news_csv, blackout_mins)
        status = f"🔴 BLOQUÉ (News à {matched_news})" if blocked else "🟢 AUTORISÉ (Pas de news)"
        print(f"Heure testée : {t} -> {status}")

if __name__ == "__main__":
    run_simulation()
