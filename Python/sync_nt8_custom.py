import shutil, os, glob

src_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
dst_code_dir = os.path.expanduser('~/Documents/NinjaTrader 8/bin/Custom/Indicators/AuctionMarketCore')
dst_template_dir = os.path.expanduser('~/Documents/NinjaTrader 8/templates/Indicator/AuctionMarketCore')

print("=" * 110)
print(f"SYNCHRONISATION AMC-V8")
print(f"  Source Code       : {src_dir} -> {dst_code_dir}")
print(f"  Indicator Templates: {src_dir}/configs -> {dst_template_dir}")
print("=" * 110)

os.makedirs(dst_code_dir, exist_ok=True)
os.makedirs(dst_template_dir, exist_ok=True)

cs_files = [
    'AuctionMarketCore.cs',
    'AuctionMarketCore.Engine.cs',
    'AuctionMarketCore.Exports.cs',
    'AuctionMarketCore.Features.cs',
    'AuctionMarketCore.MarketIntelligence.cs',
    'AuctionMarketCore.Network.cs',
    'AuctionMarketCore.Render.cs',
    'AuctionMarketCore.ScalpingPro.cs',
    'AuctionMarketCore.Sniper.cs',
    'AuctionMarketCore.Swing.Models.cs',
    'AuctionMarketCore.Swing.cs',
    'AuctionMarketCore.VolumeProfile.cs',
]

for f in cs_files:
    s = os.path.join(src_dir, f)
    d = os.path.join(dst_code_dir, f)
    if os.path.exists(s):
        shutil.copy2(s, d)
        print(f"[CODE] Copie {f} -> NT8 Custom ({os.path.getsize(d)} bytes)")

# Sync VolumeProfile subfolder
vp_src = os.path.join(src_dir, 'VolumeProfile')
vp_dst = os.path.join(dst_code_dir, 'VolumeProfile')
if os.path.exists(vp_src):
    os.makedirs(vp_dst, exist_ok=True)
    for f in os.listdir(vp_src):
        if f.endswith('.cs'):
            shutil.copy2(os.path.join(vp_src, f), os.path.join(vp_dst, f))
            print(f"[CODE] Copie VolumeProfile/{f} -> NT8 Custom")

# Sync MarketIntelligence subfolder
mi_src = os.path.join(src_dir, 'MarketIntelligence')
mi_dst = os.path.join(dst_code_dir, 'MarketIntelligence')
if os.path.exists(mi_src):
    os.makedirs(mi_dst, exist_ok=True)
    for f in os.listdir(mi_src):
        if f.endswith('.cs'):
            shutil.copy2(os.path.join(mi_src, f), os.path.join(mi_dst, f))
            print(f"[CODE] Copie MarketIntelligence/{f} -> NT8 Custom")

# Sync SWING XML Templates
swing_src = os.path.join(src_dir, 'configs', 'SWING')
swing_sub_dst = os.path.join(dst_template_dir, 'SWING')
os.makedirs(swing_sub_dst, exist_ok=True)

if os.path.exists(swing_src):
    for f in os.listdir(swing_src):
        if f.endswith('.xml'):
            s = os.path.join(swing_src, f)
            # Copie à la racine de AuctionMarketCore templates
            d1 = os.path.join(dst_template_dir, f)
            shutil.copy2(s, d1)
            # Copie dans le sous-dossier SWING
            d2 = os.path.join(swing_sub_dst, f)
            shutil.copy2(s, d2)
            print(f"[XML]  Copie SWING/{f} -> NT8 Templates (root & SWING/ subfolder)")

# Sync SCALPING_PRO XML Templates
scalp_src = os.path.join(src_dir, 'configs', 'SCALPING_PRO')
scalp_sub_dst = os.path.join(dst_template_dir, 'SCALPING_PRO')
os.makedirs(scalp_sub_dst, exist_ok=True)

if os.path.exists(scalp_src):
    for f in os.listdir(scalp_src):
        if f.endswith('.xml'):
            s = os.path.join(scalp_src, f)
            d1 = os.path.join(dst_template_dir, f)
            shutil.copy2(s, d1)
            d2 = os.path.join(scalp_sub_dst, f)
            shutil.copy2(s, d2)
            print(f"[XML]  Copie SCALPING_PRO/{f} -> NT8 Templates (root & SCALPING_PRO/ subfolder)")

print("\n" + "=" * 110)
print("Synchronisation Code & XML terminée avec succès vers NinjaTrader 8!")
print("=" * 110)
