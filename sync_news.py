import os
import xml.etree.ElementTree as ET
from datetime import datetime

def update_xml_configs():
    # Exemple d'horaires de news par défaut pour aujourd'hui (format HHMM, ex: 0830, 1000, 1430)
    # Dans un cas réel, ce script peut interroger une API de calendrier économique (Investing, ForexFactory, etc.)
    default_news_times = "0830,1000,1430,1500"
    
    config_dir = "/home/ubuntu/AMC-V8/configs"
    if not os.path.exists(config_dir):
        print("Dossier de configuration introuvable.")
        return

    updated_count = 0
    for root, dirs, files in os.walk(config_dir):
        for file in files:
            if file.endswith(".xml") and "SCALPING_PRO" in root:
                xml_path = os.path.join(root, file)
                try:
                    tree = ET.parse(xml_path)
                    xml_root = tree.getroot()
                    
                    # Chercher et mettre à jour NewsTimesCsv
                    found = False
                    for elem in xml_root.iter('NewsTimesCsv'):
                        elem.text = default_news_times
                        found = True
                        
                    if found:
                        tree.write(xml_path, encoding="utf-8", xml_declaration=True)
                        updated_count += 1
                except Exception as e:
                    print(f"Erreur lors de la mise à jour de {xml_path}: {e}")

    print(f"Mise à jour réussie : {updated_count} fichiers XML Scalping Pro mis à jour avec les horaires de news : {default_news_times}")

if __name__ == "__main__":
    update_xml_configs()
