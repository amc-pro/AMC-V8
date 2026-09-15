# Rapport d'Analyse de Performance Shadow — NQ (E-mini Nasdaq-100)
**Mode :** Scalping Pro (Sniper Engine)  
**Actif :** NQ (E-mini Nasdaq-100 - 5 Minutes)  
**Période analysée :** 25 Mai 2026 au 03 Septembre 2026 (100 jours)  
**Source des données :** `shadow/SCALPING/NQ/` (Dossiers 1, 2, 3)  
**Date du rapport :** 04 Septembre 2026  

---

## 1. Tableau Comparatif Évolutif : Test 1 vs Test 2 vs Test 3

| Métrique | Test 1 (Baseline Brut) | Test 2 (RTH-Only strict) | Test 3 (24h/24, FVG off, HtfStrict) | Progression T2 → T3 |
| :--- | :---: | :---: | :---: | :---: |
| **Total Trades Exécutés** | 512 | 270 | **520** | +250 (flux 24h restauré) |
| **Gagnants (Wins)** | 203 (195 T1 + 8 T2) | 112 (108 T1 + 4 T2) | **206 (201 T1 + 5 T2)** | +94 |
| **Perdants (STOP)** | 286 | 151 | **297** | — |
| **Neutres (SESSION_END)** | 23 | 7 | **17** | — |
| **Win Rate Effectif** | 41.51 % | 42.59 % | **40.95 %** | Équivalent |
| **Gain Net Total** | +29.96 R | +4.58 R | **+16.46 R** | 🚀 **+11.88 R (×3.6)** |
| **Gains Bruts** | +315.96 R | +157.17 R | **+315.23 R** | +158.06 R |
| **Pertes Brutes** | -286.00 R | -152.59 R | **-298.77 R** | — |
| **Profit Factor (PF)** | 1.10 | 1.03 | **1.06** | +0.03 |
| **Espérance (R/trade)** | +0.0585 R | +0.0169 R | **+0.0316 R** | 📈 ×1.9 |
| **Max Drawdown** | -19.49 R | -21.50 R | **-25.01 R** | — |

---

## 2. Performance par Direction (Test 3)

| Direction | Trades | Wins | Losses | Win Rate Effectif | Gain Net (R) | Profit Factor |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **SHORT (Vente)** | **217** | 94 | 118 | **44.34 %** | **+21.73 R** | **1.18** ⭐ |
| **LONG (Achat)** | **303** | 112 | 179 | **38.49 %** | **-5.27 R** | **0.97** |
| **TOTAL** | **520** | 206 | 297 | **40.95 %** | **+16.46 R** | **1.06** |

> [!NOTE]
> En Test 2 (RTH strict), les Longs perdaient **-18.35 R**. En Test 3 avec 24h/24 et `HtfStrictMode = true`, ils ne perdent plus que **-5.27 R** (amélioration de +13.08 R sur la patte acheteuse).

---

## 3. Performance Détaillée par Setup (Test 3)

| Setup | Trades | Wins | Losses | WR Effectif | Gain Net (R) | Profit Factor | Espérance (R) | Évaluation |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **`CUM_DELTA_DIV`** | **85** | 36 | 43 | **45.57 %** | **+26.42 R** | **1.61** | **+0.311 R** | ⭐ **Super Setup NQ (+26.42 R)** |
| **`STACKED_IMB_RETEST`** | **1** | 1 | 0 | **100.00 %** | **+1.65 R** | $\infty$ | **+1.654 R** | ⭐ 1 trade gagnant |
| **`FAILED_AUCTION_VA`** | **4** | 2 | 2 | **50.00 %** | **+0.32 R** | **1.16** | **+0.080 R** | ⭐ Positif |
| **`NPOC_ABSORPTION`** | **2** | 1 | 1 | **50.00 %** | **+0.13 R** | **1.13** | **+0.066 R** | ℹ️ Neutre |
| **`OPEN_DRIVE_FAILURE`** | **8** | 2 | 6 | **25.00 %** | **-1.95 R** | **0.68** | -0.243 R | ℹ️ Faible volume |
| **`DELTA_FLIP`** | **208** | 71 | 129 | **35.50 %** | **-4.10 R** | **0.97** | -0.020 R | ⚠️ Quasi-neutre (Short +4.90R) |
| **`FINISHED_AUCTION`** | **212** | 93 | 116 | **44.50 %** | **-6.01 R** | **0.95** | -0.028 R | ⚠️ Long -2.72R / Short -3.29R |
| **`RETEST_FVG`** | **0** | — | — | — | **0.00 R** | — | — | 🛡️ **Désactivé (Pertes évitées)** |

---

## 4. Matrice Setup × Direction (Test 3)

| Setup | Direction | Trades | Win Rate Effectif | Gain Net (R) |
| :--- | :---: | :---: | :---: | :---: |
| **`CUM_DELTA_DIV`** | **Short** | **49** | **50.0 %** | **+21.42 R** ⭐⭐ |
| **`CUM_DELTA_DIV`** | **Long** | **36** | **38.7 %** | **+4.99 R** ⭐ |
| **`DELTA_FLIP`** | **Short** | **78** | **40.8 %** | **+4.90 R** ⭐ |
| **`DELTA_FLIP`** | **Long** | **130** | **32.3 %** | **-9.01 R** |
| **`FINISHED_AUCTION`** | **Long** | **128** | **44.4 %** | **-2.72 R** |
| **`FINISHED_AUCTION`** | **Short** | **84** | **45.1 %** | **-3.29 R** |
| **`FAILED_AUCTION_VA`** | **Long** | **3** | **66.7 %** | **+1.32 R** |
| **`STACKED_IMB_RETEST`** | **Short** | **1** | **100.0 %** | **+1.65 R** |

---

## 5. Conclusions Stratégiques sur NQ

1. **La réouverture 24h/24 (`SniperRthOnly = false`) a sauvé la performance NQ** : De **+4.58 R** en Test 2 (étranglé par les heures RTH), NQ remonte à **+16.46 R** en Test 3.
2. **`CUM_DELTA_DIV` est la pépite institutionnelle du Nasdaq** : À lui seul, ce setup rapporte **+26.42 R (PF 1.61)**, dont **+21.42 R du côté Short**.
3. **L'élimination du `RETEST_FVG` standard** : A permis de supprimer les -7.48 R de pertes observées dans le Test 2.
