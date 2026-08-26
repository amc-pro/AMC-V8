import shutil, os, glob

src_dir = 'c:/AMC-Pro/AMC-V8'
dst_dir = os.path.expanduser('~/Documents/NinjaTrader 8/bin/Custom/Indicators/AuctionMarketScalpingPro')

print("=" * 110)
print(f"SYNCHRONISATION AMC-V8 -> {dst_dir}")
print("=" * 110)

cs_files = [
    'AuctionMarketScalpingPro.cs',
    'AuctionMarketScalpingPro.Engine.cs',
    'AuctionMarketScalpingPro.Exports.cs',
    'AuctionMarketScalpingPro.Features.cs',
    'AuctionMarketScalpingPro.MarketIntelligence.cs',
    'AuctionMarketScalpingPro.Network.cs',
    'AuctionMarketScalpingPro.Render.cs',
    'AuctionMarketScalpingPro.ScalpingPro.cs',
    'AuctionMarketScalpingPro.Sniper.cs',
    'AuctionMarketScalpingPro.VolumeProfile.cs',
]

for f in cs_files:
    s = os.path.join(src_dir, f)
    d = os.path.join(dst_dir, f)
    if os.path.exists(s):
        shutil.copy2(s, d)
        print(f"Copie {f} -> NT8 Custom ({os.path.getsize(d)} bytes)")

# Sync VolumeProfile subfolder
vp_src = os.path.join(src_dir, 'VolumeProfile')
vp_dst = os.path.join(dst_dir, 'VolumeProfile')
if os.path.exists(vp_src):
    for f in os.listdir(vp_src):
        if f.endswith('.cs'):
            shutil.copy2(os.path.join(vp_src, f), os.path.join(vp_dst, f))
            print(f"Copie VolumeProfile/{f} -> NT8 Custom")

print("\nSynchronisation terminée avec succès!")
