# 📊 Rapport Comparatif de Performance Institutionnelle : GC, CL, NQ
## AMC PRO V8.0 — Shadow Testing & Backtest (Juillet & Août 2026)

* **Date d'Analyse** : 19 Août 2026
* **Période Analysée** : Juillet & Août 2026 (~7 semaines)
* **Moteur & Système** : `SniperMarketCorePro` (AMC PRO V8.0)
* **Données Sources** : 
  - 🥇 [csv/GC/AuctionMarketCorePro_journal_sniper_outcomes.csv](file:///c:/Users/andro/Downloads/volumeprofile/AMC_PRO_V8.0/csv/GC/AuctionMarketCorePro_journal_sniper_outcomes.csv)
  - 🛢️ [csv/CL/AuctionMarketCorePro_journal_sniper_outcomes.csv](file:///c:/Users/andro/Downloads/volumeprofile/AMC_PRO_V8.0/csv/CL/AuctionMarketCorePro_journal_sniper_outcomes.csv)
  - 💻 [csv/NQ/AuctionMarketCorePro_journal_sniper_outcomes.csv](file:///c:/Users/andro/Downloads/volumeprofile/AMC_PRO_V8.0/csv/NQ/AuctionMarketCorePro_journal_sniper_outcomes.csv)

---

## 🧭 1. Synthèse Comparative Globale

| Indicateur Clé | 🥇 Gold (GC) | 🛢️ Crude Oil (CL) | 💻 Nasdaq (NQ) | Total Portefeuille |
| :--- | :---: | :---: | :---: | :---: |
| **Nombre Total de Trades** | **108** | **155** | **50** | **313 trades** |
| **Taux de Réussite (Win Rate)** | **53.70 %** | **40.65 %** | **8.00 %** | **40.06 %** |
| **Trades Gagnants (TP1 / TP2)** | **58** (52 TP1 / 6 TP2) | **63** (63 TP1 / 0 TP2) | **4** (4 TP1 / 0 TP2) | **125 trades** |
| **Trades Perdants (Stop Loss)** | **50** (46.30 %) | **91** (58.71 %) | **46** (92.00 %) | **187 trades** |
| **Fin de Session / Timeout** | 0 | 1 (0.65 %) | 0 | 1 trade |
| **Gain Net Total ($R$)** | **`+34.61 R`** 🚀 | **`-4.71 R`** | **`-13.33 R`** | **`+16.57 R`** 📈 |
| **Gains Bruts / Pertes Brutes** | +84.86 R / -50.25 R | +86.36 R / -91.07 R | +32.67 R / -46.00 R | +203.89 R / -187.32 R |
| **Profit Factor (PF)** | **`1.69`** (Solide) | **`0.95`** (Neutre) | **`0.71`** (Volatile) | **`1.09`** |
| **Espérance $E[R]$ / trade** | **`+0.32 R`** | **`-0.03 R`** | **`-0.27 R`** | **`+0.05 R`** |
| **Max Drawdown (en $R$)** | **`6.00 R`** | **`17.67 R`** | **`22.29 R`** | — |
| **Série Max (Wins / Losses)** | **6 W / 6 L** | **5 W / 8 L** | **1 W / 20 L** | — |

---

## 🥇 2. Focus Instrument : GC (Gold / Or) — Leader de Performance

Le Gold affiche des performances exceptionnelles, avec une stabilité remarquable et une profitabilité positive chaque semaine.

### A. Performance par Mois & Semaines
* **Juillet 2026** : 56 trades | **55.4 % WR** | **+19.13 R** | **PF 1.76** | $E[R]$ +0.34 R
* **Août 2026** : 52 trades | **51.9 % WR** | **+15.48 R** | **PF 1.62** | $E[R]$ +0.30 R
* **Régularité Hebdomadaire** : **100 % des semaines positives** (+1.28R, +2.73R, +7.78R, +7.35R, +11.29R, +3.77R, +0.42R).

### B. Décomposition par Session
| Session | Horaires (UTC) | Nb Trades | % Total | Win Rate % | Gain Net ($R$) | Profit Factor |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Asie / Nuit** | 00h00 – 08h00 | **62** | 57.4 % | **54.8 %** | **+23.13 R** | **1.83** |
| **US / Après-midi** | 14h30 – 22h00 | **24** | 22.2 % | **45.8 %** | **+7.02 R** | **1.53** |
| **Londres / Matin** | 08h00 – 14h30 | **22** | 20.4 % | **59.1 %** | **+4.46 R** | **1.50** |

### C. Top Setups sur GC
1. **`CUM_DELTA_DIV`** : 15 trades | **80.0 % WR** | **+11.90 R** | **Profit Factor : 4.97**
2. **`DELTA_FLIP`** : 17 trades | **58.8 % WR** | **+8.82 R** | **Profit Factor : 2.26**
3. **`FINISHED_AUCTION`** : 67 trades | **47.8 % WR** | **+12.23 R** | **Profit Factor : 1.35**

---

## 🛢️ 3. Focus Instrument : CL (Crude Oil / Pétrole Brut)

Le Pétrole Brut présente une structure globale proche de l'équilibre (-4.71 R), pénalisée principalement par la session européenne.

### A. Performance par Mois
* **Juillet 2026** : 135 trades | 41.5 % WR | **-4.10 R** | PF 0.95
* **Août 2026** : 20 trades | 35.0 % WR | **-0.61 R** | PF 0.95

### B. Décomposition par Session
| Session | Horaires (UTC) | Nb Trades | % Total | Win Rate % | Gain Net ($R$) | Profit Factor |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Asie / Nuit** | 00h00 – 08h00 | **93** | 60.0 % | **40.9 %** | **+2.13 R** | **1.04** |
| **US / Après-midi** | 14h30 – 22h00 | **38** | 24.5 % | **47.4 %** | **+0.93 R** | **1.05** |
| **Londres / Matin** | 08h00 – 14h30 | **24** | 15.5 % | **29.2 %** | **-7.78 R** | **0.54** ⚠️ |

### C. Constats & Clés d'Amélioration CL
* **Fuite de performance** localisée sur la session Londres (**-7.78 R**). Les sessions Asie et US sont toutes deux positives.
* Les signaux de score élevé **`[70-79]`** sont rentables (**+0.58 R, PF 1.12**).
* **Action recommandée** : Filtrer la session Londres ou relever le seuil `MinScoreToAlert` à 60-65 sur CL.

---

## 💻 4. Focus Instrument : NQ (Nasdaq 100)

Le Nasdaq illustre l'impact de la forte volatilité et des mèches (wicks) sur les stops serrés.

### A. Performance par Mois
* **Juillet 2026** : 30 trades | 6.7 % WR | **-4.29 R** | PF 0.85
* **Août 2026** : 20 trades | 10.0 % WR | **-9.04 R** | PF 0.50

### B. Décomposition par Session
| Session | Horaires (UTC) | Nb Trades | % Total | Win Rate % | Gain Net ($R$) | Profit Factor |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Asie / Nuit** | 00h00 – 08h00 | **27** | 54.0 % | **14.8 %** | **+9.67 R** | **1.42** 🚀 |
| **Londres / Matin** | 08h00 – 14h30 | **13** | 26.0 % | 0.0 % | **-13.00 R** | 0.00 🛑 |
| **US / Après-midi** | 14h30 – 22h00 | **10** | 20.0 % | 0.0 % | **-10.00 R** | 0.00 🛑 |

### C. La Pépite : Le Grade `TRESFORT` et le Setup `CUM_DELTA_DIV`
* **Grade `TRESFORT`** : **+19.71 R** avec un **Profit Factor de 5.93** et une espérance de **+3.28 R / trade** !
* **Setup `CUM_DELTA_DIV`** : **+8.71 R**, Profit Factor **1.58**.
* **Grade `FORT`** : **-33.04 R** (les stops sont chassés par le bruit intraday avant le mouvement).

---

## 🎯 5. Synthèse & Plan d'Action Recommandé

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ 🥇 GC (GOLD) : PRESET EXCELLENT - À CONSERVER TEL QUEL                     │
│    - Profit Factor : 1.69 | Gain Net : +34.61 R                            │
│    - Régularité : 100% de semaines gagnantes                               │
└─────────────────────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────────────────────┐
│ 🛢️ CL (CRUDE OIL) : OPTIMISATION CIBLÉE                                      │
│    - Désactiver / filtrer les alertes en session Londres (08h00 - 14h30)    │
│    - Augmenter le filtre MinScoreToAlert de 50 à 60                         │
└─────────────────────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────────────────────┐
│ 💻 NQ (NASDAQ) : SÉLECTIVITÉ ULTRA-SNIPER & GESTION DU STOP                 │
│    - N'exécuter que les grades TRESFORT (MinScoreToAlert >= 70) ➔ +19.71 R  │
│    - Privilégier la session Asie (+9.67 R) ou élargir StopAtrMultiple (2.0) │
└─────────────────────────────────────────────────────────────────────────────┘
```
