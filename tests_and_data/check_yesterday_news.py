import requests
from datetime import datetime, timedelta

def get_yesterday_news():
    url = "https://nfs.faireconomy.media/ff_calendar_thisweek.json"
    headers = {"User-Agent": "Mozilla/5.0"}
    yesterday_str = "2026-08-19"
    
    try:
        response = requests.get(url, headers=headers)
        events = response.json()
        
        yesterday_events = []
        for event in events:
            if event.get("date", "").startswith(yesterday_str):
                if event.get("impact") in ["High", "Medium"]:
                    # Extraction de l'heure EDT et conversion en GMT+3 (+7h)
                    time_part = event.get("date").split("T")[1].split("-")[0]
                    dt = datetime.strptime(time_part, "%H:%M:%S")
                    user_dt = dt + timedelta(hours=7)
                    yesterday_events.append({
                        "title": event.get("title"),
                        "time": user_dt.strftime("%H:%M"),
                        "hhmm": user_dt.strftime("%H%M"),
                        "impact": event.get("impact"),
                        "country": event.get("country")
                    })
        return yesterday_events
    except Exception as e:
        print(f"Erreur : {e}")
        return []

if __name__ == "__main__":
    news = get_yesterday_news()
    if not news:
        print("Aucune news majeure hier.")
    else:
        print("News du 19 août 2026 (GMT+3) :")
        for n in news:
            print(f"- {n['time']} : {n['title']} ({n['impact']}) [{n['country']}]")
