import os
import glob
import re

configs_dir = r"c:\AMC-Pro\AMC-V8\configs\SCALPING_PRO"
xml_files = glob.glob(os.path.join(configs_dir, "*.xml"))

print(f"Found {len(xml_files)} XML config files in {configs_dir}")

replacements = {
    r"<MinScoreToAlert>\d+</MinScoreToAlert>": "<MinScoreToAlert>50</MinScoreToAlert>",
    r"<NewsHardBlock>(true|false)</NewsHardBlock>": "<NewsHardBlock>false</NewsHardBlock>",
    r"<NewsWindowPenalty>\d+</NewsWindowPenalty>": "<NewsWindowPenalty>15</NewsWindowPenalty>",
    r"<GateN1MinScore>\d+</GateN1MinScore>": "<GateN1MinScore>6</GateN1MinScore>",
    r"<GateN2MinScore>\d+</GateN2MinScore>": "<GateN2MinScore>3</GateN2MinScore>",
    r"<GateN3MinScore>\d+</GateN3MinScore>": "<GateN3MinScore>3</GateN3MinScore>",
    r"<GateN4MinScore>\d+</GateN4MinScore>": "<GateN4MinScore>2</GateN4MinScore>",
    r"<TierSilverScore>\d+</TierSilverScore>": "<TierSilverScore>45</TierSilverScore>",
    r"<TierGoldScore>\d+</TierGoldScore>": "<TierGoldScore>65</TierGoldScore>",
    r"<HtfSoftMode>(true|false)</HtfSoftMode>": "<HtfSoftMode>true</HtfSoftMode>",
}

for fpath in xml_files:
    with open(fpath, "r", encoding="utf-8") as f:
        content = f.read()
    
    modified = content
    for pat, repl in replacements.items():
        modified = re.sub(pat, repl, modified)
        
    with open(fpath, "w", encoding="utf-8") as f:
        f.write(modified)
    
    print(f"Updated {os.path.basename(fpath)}")

print("All ScalpingPro XML configuration files updated successfully!")
