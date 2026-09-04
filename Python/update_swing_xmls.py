import os
import glob
import re

configs_dir = "c:/AMC-Pro/AMC-V8/configs/SWING"

instruments_config = {
    "GC": {"reject": "false", "poc": "true", "score": "70"},
    "MGC": {"reject": "false", "poc": "true", "score": "70"},
    "ES": {"reject": "false", "poc": "true", "score": "50"},
    "MES": {"reject": "false", "poc": "true", "score": "50"},
    "CL": {"reject": "false", "poc": "false", "score": "50"},
    "MCL": {"reject": "false", "poc": "false", "score": "50"},
    "NQ": {"reject": "false", "poc": "false", "score": "50"},
    "MNQ": {"reject": "false", "poc": "false", "score": "50"},
}

for sym, cfg in instruments_config.items():
    xml_path = os.path.join(configs_dir, f"CONFIG_{sym}_SWING.xml")
    if not os.path.exists(xml_path):
        print(f"Non trouvé: {xml_path}")
        continue
        
    with open(xml_path, "r", encoding="utf-8") as f:
        content = f.read()

    # Mettre à jour SwingMinScoreToAlert
    content = re.sub(r"<SwingMinScoreToAlert>\d+</SwingMinScoreToAlert>", f"<SwingMinScoreToAlert>{cfg['score']}</SwingMinScoreToAlert>", content)
    
    # Mettre à jour EnablePocMigration
    content = re.sub(r"<EnablePocMigration>\w+</EnablePocMigration>", f"<EnablePocMigration>{cfg['poc']}</EnablePocMigration>", content)
    
    # Vérifier / Insérer les toggles de setups après EnableSwingTelegramAlerts
    toggles_xml = f"""      <EnableSwingTelegramAlerts>true</EnableSwingTelegramAlerts>
      <EnableSwingRejectExtreme>{cfg['reject']}</EnableSwingRejectExtreme>
      <EnableSwingBreakoutRetest>true</EnableSwingBreakoutRetest>
      <EnableSwingMacroReversal>true</EnableSwingMacroReversal>
      <EnableSwingHtfContinuation>true</EnableSwingHtfContinuation>
      <EnableSwingValueReentry>true</EnableSwingValueReentry>"""

    if "<EnableSwingRejectExtreme>" in content:
        content = re.sub(r"<EnableSwingRejectExtreme>\w+</EnableSwingRejectExtreme>", f"<EnableSwingRejectExtreme>{cfg['reject']}</EnableSwingRejectExtreme>", content)
    else:
        content = content.replace("      <EnableSwingTelegramAlerts>true</EnableSwingTelegramAlerts>", toggles_xml)

    with open(xml_path, "w", encoding="utf-8") as f:
        f.write(content)
        
    print(f"Mis à jour avec succès : CONFIG_{sym}_SWING.xml (RejectExtreme={cfg['reject']}, PocMigration={cfg['poc']}, MinScore={cfg['score']})")

print("\nTous les 8 fichiers XML Swing ont été calibrés avec succès.")
