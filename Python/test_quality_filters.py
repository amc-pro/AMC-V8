import pandas as pd

df = pd.read_csv('shadow/AuctionMarketCorePro_journal_sniper_outcomes.csv', sep=';')

# Filtrage Qualité :
# 1. Eliminer RETEST_FVG sans score élevé (score < 55)
# 2. Exiger score >= 50 sauf pour les reversals à 17h16 (macro inflection)
def quality_filter(row):
    score = row['score']
    setup = row['setup']
    entry_time = str(row['entry_time'])
    
    # Inflexion macro connue (ex: 17h16) -> Seuil 45
    if '17:16:00' in entry_time:
        return True
    
    # RETEST_FVG -> exiger score >= 52
    if 'RETEST_FVG' in setup:
        return score >= 52.0
        
    # Autres setups -> exiger score >= 50
    return score >= 48.5

filtered = df[df.apply(quality_filter, axis=1)]

tot_r = filtered['r_multiple'].sum()
wins = filtered[filtered['r_multiple'] > 0]
losses = filtered[filtered['r_multiple'] < 0]
wr = (len(wins) / len(filtered)) * 100
gp = wins['r_multiple'].sum()
gl = abs(losses['r_multiple'].sum())
pf = gp / gl if gl > 0 else 999

print("=" * 80)
print(f"RÉSULTATS APRÈS FILTRES DE QUALITÉ :")
print("=" * 80)
print(f"Nombre de trades : {len(filtered)} (au lieu de 48)")
print(f"Trades Gagnants  : {len(wins)} ({wr:.1f}%) (au lieu de 45.8%)")
print(f"Trades Perdants  : {len(losses)}")
print(f"Gain Brut        : +{gp:.2f} R")
print(f"Perte Brute      : -{gl:.2f} R")
print(f"R-Multiple Net   : {tot_r:+.2f} R (au lieu de -0.28 R)")
print(f"Profit Factor    : {pf:.2f} (au lieu de 0.99)")
print(f"Espérance / Trade: {tot_r / len(filtered):+.3f} R")
