import os
import requests
import xml.etree.ElementTree as ET
from datetime import datetime, timedelta
import json

def get_real_news():
    url = "https://nfs.faireconomy.media/ff_calendar_thisweek.json"
    headers = {"User-Agent": "Mozilla/5.0"}
    
    try:
        response = requests.get(url, headers=headers)
        if response.status_code != 200:
            print(f"Erreur HTTP : {response.status_code}")
            return []
        
        events = response.json()
        
        # Date du jour (User timezone: GMT+3)
        # Note: Le sandbox est en UTC, le script doit s'adapter.
        # Le JSON est en EDT (UTC-4).
        today_str = "2026-08-20" # Fixé pour le test, sinon datetime.now().strftime("%Y-%m-%d")
        
        news_times = []
        for event in events:
            # Format: "2026-08-16T18:30:00-04:00"
            event_date_str = event.get("date", "")
            if not event_date_str.startswith(today_str):
                # On pourrait aussi filtrer sur demain ou la semaine, 
                # mais NinjaTrader applique les horaires chaque jour.
                continue
                
            # Filtrer par impact (High et Medium par défaut pour le scalping)
            if event.get("impact") not in ["High", "Medium"]:
                continue
                
            # Filtrer par devise (USD, EUR, GBP, NQ/ES/CL/GC sont liés au USD)
            if event.get("country") not in ["USD", "EUR", "GBP", "ALL"]:
                continue

            # Conversion heure : EDT (UTC-4) vers GMT+3 => +7 heures
            # Exemple : 08:30 EDT -> 15:30 GMT+3
            try:
                # Extraction simple de l'heure sans parsing complexe de fuseau pour rester robuste
                time_part = event_date_str.split("T")[1].split("-")[0] # "18:30:00"
                dt = datetime.strptime(time_part, "%H:%M:%S")
                # Ajustement +7h (EDT -> GMT+3)
                user_dt = dt + timedelta(hours=7)
                hhmm = user_dt.strftime("%H%M")
                if hhmm not in news_times:
                    news_times.append(hhmm)
            except:
                continue
        
        return sorted(news_times)
    except Exception as e:
        print(f"Erreur lors de la récupération des news : {e}")
        return []

def update_xml_configs():
    news_times_list = get_real_news()
    if not news_times_list:
        # Si aucune news trouvée, on met des horaires de sécurité ou on vide
        print("Aucune news majeure trouvée pour aujourd'hui.")
        news_times_str = ""
    else:
        news_times_str = ",".join(news_times_list)
    
    config_dir = "/home/ubuntu/AMC-V8/configs"
    updated_count = 0
    for root, dirs, files in os.walk(config_dir):
        for file in files:
            if file.endswith(".xml") and "SCALPING_PRO" in root:
                xml_path = os.path.join(root, file)
                try:
                    tree = ET.parse(xml_path)
                    xml_root = tree.getroot()
                    
                    found = False
                    for elem in xml_root.iter('NewsTimesCsv'):
                        elem.text = news_times_str
                        found = True
                        
                    if found:
                        tree.write(xml_path, encoding="utf-8", xml_declaration=True)
                        updated_count += 1
                except Exception as e:
                    print(f"Erreur sur {xml_path}: {e}")

    print(f"Mise à jour terminée : {updated_count} fichiers impactés.")
    print(f"Horaires synchronisés (GMT+3) : {news_times_str}")

if __name__ == "__main__":
    update_xml_configs()
