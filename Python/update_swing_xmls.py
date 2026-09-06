import os
import re

configs_dir = "c:/AMC-Pro/AMC-V8/configs/SWING"

instruments_config = {
    "GC": {"reject": "false", "breakout": "true", "poc": "true", "score": "70"},
    "MGC": {"reject": "false", "breakout": "true", "poc": "true", "score": "70"},
    "ES": {"reject": "false", "breakout": "true", "poc": "true", "score": "50"},
    "MES": {"reject": "false", "breakout": "true", "poc": "true", "score": "50"},
    "CL": {"reject": "false", "breakout": "true", "poc": "false", "score": "50"},
    "MCL": {"reject": "false", "breakout": "true", "poc": "false", "score": "50"},
    "NQ": {"reject": "false", "breakout": "false", "poc": "false", "score": "50"},
    "MNQ": {"reject": "false", "breakout": "false", "poc": "false", "score": "50"},
}

opp_manager_xml = """      <EnableOpportunityManager>true</EnableOpportunityManager>
      <SameCampaignLock>true</SameCampaignLock>
      <RequireNewStructureForReentry>true</RequireNewStructureForReentry>
      <ExitOnRegimeChange>false</ExitOnRegimeChange>
      <SwingEntryCooldownBars>12</SwingEntryCooldownBars>
      <SwingMaxEntriesPerSession>2</SwingMaxEntriesPerSession>
      <SwingMaxLongEntriesPerSession>1</SwingMaxLongEntriesPerSession>
      <SwingMaxShortEntriesPerSession>1</SwingMaxShortEntriesPerSession>
      <SwingMaxBarsInTrade>0</SwingMaxBarsInTrade>
      <EnableLateEntryPenalty>true</EnableLateEntryPenalty>
      <EnableCandidateRanking>true</EnableCandidateRanking>"""

for sym, cfg in instruments_config.items():
    xml_path = os.path.join(configs_dir, f"CONFIG_{sym}_SWING.xml")
    if not os.path.exists(xml_path):
        print(f"Non trouvé: {xml_path}")
        continue
        
    with open(xml_path, "r", encoding="utf-8") as f:
        content = f.read()

    # Mettre à jour ExitOnRegimeChange vers false
    content = re.sub(r"<ExitOnRegimeChange>\w+</ExitOnRegimeChange>", "<ExitOnRegimeChange>false</ExitOnRegimeChange>", content)

    # Mettre à jour SwingMinScoreToAlert
    content = re.sub(r"<SwingMinScoreToAlert>\d+</SwingMinScoreToAlert>", f"<SwingMinScoreToAlert>{cfg['score']}</SwingMinScoreToAlert>", content)
    
    # Mettre à jour EnablePocMigration
    content = re.sub(r"<EnablePocMigration>\w+</EnablePocMigration>", f"<EnablePocMigration>{cfg['poc']}</EnablePocMigration>", content)

    # Mettre à jour EnableSwingBreakoutRetest
    content = re.sub(r"<EnableSwingBreakoutRetest>\w+</EnableSwingBreakoutRetest>", f"<EnableSwingBreakoutRetest>{cfg['breakout']}</EnableSwingBreakoutRetest>", content)
    
    # Mettre à jour EnableSwingRejectExtreme
    content = re.sub(r"<EnableSwingRejectExtreme>\w+</EnableSwingRejectExtreme>", f"<EnableSwingRejectExtreme>{cfg['reject']}</EnableSwingRejectExtreme>", content)

    # Insérer Opportunity Manager si absent
    if "<EnableOpportunityManager>" not in content:
        target_anchor = "<EnableSwingValueReentry>true</EnableSwingValueReentry>"
        if target_anchor in content:
            content = content.replace(target_anchor, f"{target_anchor}\n{opp_manager_xml}")

    with open(xml_path, "w", encoding="utf-8") as f:
        f.write(content)
        
    print(f"Mis à jour : CONFIG_{sym}_SWING.xml (BreakoutRetest={cfg['breakout']}, PocMigration={cfg['poc']}, Score={cfg['score']})")

print("\nTous les 8 fichiers XML Swing ont été calibrés V3 avec succès.")
