import os
import glob
import re

configs_dir = r"c:\AMC-Pro\AMC-V8\configs\SCALPING_PRO"
xml_files = glob.glob(os.path.join(configs_dir, "*.xml"))

print(f"Found {len(xml_files)} XML config files in {configs_dir}")

for fpath in xml_files:
    fname = os.path.basename(fpath)
    with open(fpath, "r", encoding="utf-8") as f:
        content = f.read()
    
    # 1. MaxStopPips -> 0
    content = re.sub(r"<MaxStopPips>\d+</MaxStopPips>", "<MaxStopPips>0</MaxStopPips>", content)
    
    # 2. StopAtrMultiple -> 1.75
    content = re.sub(r"<StopAtrMultiple>[\d\.]+</StopAtrMultiple>", "<StopAtrMultiple>1.75</StopAtrMultiple>", content)
    
    # 3. Instrument specific stop ticks
    if "NQ" in fname:
        content = re.sub(r"<MinStopTicks>\d+</MinStopTicks>", "<MinStopTicks>12</MinStopTicks>", content)
        content = re.sub(r"<MaxStopTicks>\d+</MaxStopTicks>", "<MaxStopTicks>160</MaxStopTicks>", content)
        content = re.sub(r"<StopBufferTicks>\d+</StopBufferTicks>", "<StopBufferTicks>6</StopBufferTicks>", content)
    elif "ES" in fname:
        content = re.sub(r"<MinStopTicks>\d+</MinStopTicks>", "<MinStopTicks>8</MinStopTicks>", content)
        content = re.sub(r"<MaxStopTicks>\d+</MaxStopTicks>", "<MaxStopTicks>40</MaxStopTicks>", content)
        content = re.sub(r"<StopBufferTicks>\d+</StopBufferTicks>", "<StopBufferTicks>4</StopBufferTicks>", content)
    elif "GC" in fname:
        content = re.sub(r"<MinStopTicks>\d+</MinStopTicks>", "<MinStopTicks>10</MinStopTicks>", content)
        content = re.sub(r"<MaxStopTicks>\d+</MaxStopTicks>", "<MaxStopTicks>60</MaxStopTicks>", content)
        content = re.sub(r"<StopBufferTicks>\d+</StopBufferTicks>", "<StopBufferTicks>4</StopBufferTicks>", content)
    elif "CL" in fname:
        content = re.sub(r"<MinStopTicks>\d+</MinStopTicks>", "<MinStopTicks>10</MinStopTicks>", content)
        content = re.sub(r"<MaxStopTicks>\d+</MaxStopTicks>", "<MaxStopTicks>50</MaxStopTicks>", content)
        content = re.sub(r"<StopBufferTicks>\d+</StopBufferTicks>", "<StopBufferTicks>4</StopBufferTicks>", content)
        
    with open(fpath, "w", encoding="utf-8") as f:
        f.write(content)
    
    print(f"Updated stop loss settings in {fname}")

print("All ScalpingPro XML files updated successfully!")
