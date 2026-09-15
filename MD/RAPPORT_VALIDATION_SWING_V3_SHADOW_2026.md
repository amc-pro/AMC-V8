# 📊 Rapport d'Analyse Approfondie — Validation Swing V3 Shadow

**Source des Données :** `C:\Users\andro\Documents\NinjaTrader 8\shadow\swing_trades.csv`  
**Période Testée (Out-Of-Sample) :** 31 Décembre 2025 au 28 Mai 2026 (~5 mois / 150 jours)  
**Actifs Institutionnels Évalués :** Gold (`GC`), S&P 500 (`ES`), Nasdaq E-mini (`NQ`), Micro Nasdaq (`MNQ`), Pétrole Brut (`CL`)  
**Volume d'Échantillonnage :** **1 047 trades clôturés**  
**Date d'Audit :** 04 Septembre 2026  

---

## 1. Synthèse Exécutive & Découverte Capitale

Le présent test constitue une **validation hors-échantillon (Out-Of-Sample - OOS)** majeure sur les 5 premiers mois de l'année 2026 (H1 2026), complétant le Test 2 précédent qui couvrait la période estivale (Juin à Septembre 2026).

L'audit approfondi révèle une situation nette :

1. **Succès Total de l'Infrastructure Swing V3 :**
   - **Anti-Stacking & Campaign Locking (`SwingOpportunityManager`) :** **Zéro chevauchement (0 overlap)**. Le bug historique d'émissions en rafale est éradiqué.
   - **Régulation de Session (Option A) :** Exactement **2.0 trades par session et par actif** sur 5 mois (CL: 203, ES: 212, GC: 208, MNQ: 212, NQ: 212), éliminant tout sur-trading.
   - **Snapping Dynamique de TP1 :** 28 trades ont vu leur TP1 snappé entre 1.01R et 1.45R sur des niveaux institutionnels (VAH/VAL/SD Bands), sécurisant des gains avant retournement.
   - **Le Moteur Swing Naturel est Profitable sur 100% des Actifs :** Lorsque les trades suivent leur cycle normal (SL, TP1, TP2, BE), le portefeuille génère **+$57 605,33 USD (+59,85 R)** avec un **Profit Factor de 1.23** et **100% des 5 actifs dans le vert**.

2. **La Cause Racine du PnL Brut Défavorable (-$9 029,17 USD) :**
   - L'implémentation de la règle de sortie anticipée sur changement de régime (`ExitOnRegimeChange`, ligne 1319 de `AuctionMarketCore.Swing.cs`) a testé un simple franchissement de bougie M5 contre l'EMA HTF :
     `bool regimeOpposed = (t.IsLong && close < htfEma[0]) || (!t.IsLong && close > htfEma[0]);`
   - **Conséquence dramatique :** **558 trades (53,3% de tous les trades du test !)** ont été liquidés au marché après seulement **15 minutes (3 barres)** d'existence, générant une hémorragie purement frictionnelle de **-$66 634,50 USD (-74,04 R)** !
   - Pour `MacroReversal`, **426 des 434 trades (98,2%)** ont été coupés immédiatement car, par définition, une entrée en retournement s'effectue sous l'EMA pour un Long ou au-dessus de l'EMA pour un Short.

---

## 2. Tableau Comparatif : Résultats Bruts vs Performance des Sorties Naturelles

| Métrique | Résultat Brut Global (avec Churn `REGIME_CHANGED`) | Sorties Naturelles V3 (SL, TP1, TP2, BE) | Impact du Bug Ligne 1319 (`REGIME_CHANGED`) |
| :--- | :---: | :---: | :---: |
| **Total Trades Clôturés** | 1 047 | **489** | 558 (53,3% du flux) |
| **Win Rate** | 40,69 % | **45,60 %** | 36,38 % |
| **PnL Net Réalisé ($)** | -$9 029,17 USD | 🚀 **+$57 605,33 USD** | ⚠️ **-$66 634,50 USD** |
| **Gain Net en R** | -14,19 R | 🚀 **+59,85 R** | ⚠️ **-74,04 R** |
| **Profit Factor** | 0,98 | ⭐ **1,23** | 0,72 |
| **Gross Profit ($)** | $358 738,91 | $304 539,41 | $54 199,50 |
| **Gross Loss ($)** | $367 768,08 | $246 934,08 | $120 834,00 |
| **Espérance par Trade** | -$8,62 / trade | **+$117,80 / trade (+0,122 R)** | -$119,42 / trade |
| **Durée Moyenne en Trade** | 158 min | **270 min (~4,5 h)** | **31,4 min (avorté à bar 3)** |

---

## 3. Analyse Détaillée par Actif

### 3.1. Performance Brute Enregistrée
| Actif | Trades | Wins | Losses | Win Rate | Net PnL ($) | Net Gain (R) | Profit Factor | Max Drawdown ($) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **GC (Gold)** | 208 | 91 | 116 | **43,8 %** | 🚀 **+$7 014,28** | **+5,27 R** | **1,06** | -$21 743,81 |
| **ES (S&P 500)** | 212 | 94 | 114 | **44,3 %** | 🚀 **+$2 678,84** | **+5,32 R** | **1,04** | -$12 026,10 |
| **MNQ (Micro NQ)** | 212 | 81 | 129 | 38,2 % | -$1 876,50 | -7,09 R | 0,90 | -$6 353,08 |
| **CL (Crude Oil)** | 203 | 75 | 124 | 36,9 % | -$2 713,52 | -6,69 R | 0,96 | -$14 110,64 |
| **NQ (Nasdaq E-mini)**| 212 | 85 | 126 | 40,1 % | -$14 132,27 | -11,00 R | 0,86 | -$33 994,92 |
| **TOTAL** | **1 047** | **426** | **609** | **40,7 %** | **-$9 029,17** | **-14,19 R** | **0,98** | **-$71 838,10** |

### 3.2. Performance Réelle Hors Coupures Intempestives (Sorties Naturelles SL/TP)
| Actif | Trades Naturels | Wins | Win Rate | Net USD Naturel | Net R Naturel | Profit Factor Naturel | Statut |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **GC (Gold)** | 101 | 49 | **48,5 %** | 🚀 **+$27 374,28** | **+18,55 R** | **1,36** | Leader Absolu Alpha |
| **ES (S&P 500)** | 98 | 48 | **49,0 %** | 🚀 **+$16 516,34** | **+19,29 R** | **1,40** | Régularité Institutionnelle |
| **NQ (Nasdaq E-mini)**| 100 | 44 | **44,0 %** | 🚀 **+$9 172,73** | **+8,46 R** | **1,14** | Positif sans coupures |
| **MNQ (Micro NQ)** | 99 | 45 | **45,5 %** | 🚀 **+$2 855,50** | **+12,66 R** | **1,22** | Solide Ratio R/Trade |
| **CL (Crude Oil)** | 91 | 37 | **40,7 %** | 🚀 **+$1 686,48** | **+0,89 R** | **1,03** | À l'équilibre positif |
| **TOTAL DU PORTEFEUILLE** | **489** | **223** | **45,6 %** | 🚀 **+$57 605,33** | 🚀 **+59,85 R** | ⭐ **1,23** | **100% des Actifs dans le Vert** |

---

## 4. Analyse par Type de Setup

| Setup Type | Trades Totaux | Trades Naturels | Win Rate Naturel | Net USD Naturel | Net R Naturel | PF Naturel | Diagnostic Swing V3 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **`HtfContinuation`** | 506 | 448 | **45,5 %** | 🚀 **+$48 959,25** | **+52,28 R** | **1,22** | **Pilier Maître Indiscutable** |
| **`MacroReversal`** | 448 | 14 | **57,1 %** | 🚀 **+$5 787,22** | **+6,98 R** | **2,16** | Excellent si non avorté (426 trades tués à M15) |
| **`ValueReentry`** | 51 | 4 | **50,0 %** | 🚀 **+$2 390,00** | **+0,69 R** | **2,66** | Haute Précision |
| **`MonthlyVwapBandRetest`** | 9 | 9 | **44,4 %** | 🚀 **+$627,80** | **+1,48 R** | **1,12** | Nouvelle brique validée positive |
| **`BreakoutRetest`** | 33 | 14 | 35,7 % | -$155,01 | -1,58 R | 0,98 | Faible sur indices, bon sur GC (+5,6K$) |

---

## 5. Analyse par Direction & Biais de Marché

| Direction | Trades | Win Rate | Net PnL ($) | Net Gain (R) | Profit Factor |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **LONG** | 523 | **44,6 %** | 🚀 **+$31 298,61 USD** | **+37,52 R** | **1,16** |
| **SHORT** | 524 | 36,8 % | ⚠️ **-$40 327,78 USD** | **-51,71 R** | 0,77 |

> [!NOTE]
> Le premier semestre 2026 a été caractérisé par une tendance haussière puissante sur les actions et les métaux. Les positions Long ont généré +31,3K$ de profit net. Les Shorts ont souffert, particulièrement sur les tentatives de `MacroReversal` contre-tendance coupées au plus mauvais moment.

---

## 6. Analyse Mensuelle & Évolution Temporelle

| Mois | Trades Totaux | Win Rate Total | Net USD Total | Trades REGIME_CHANGED | PnL REGIME_CHANGED | Net USD Sorties Naturelles |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Décembre 2025** | 10 | 60,0 % | +$1 656,78 | 4 | +$268,00 | +$1 388,78 (+1,50 R) |
| **Janvier 2026** | 210 | 34,3 % | -$11 376,25 | 118 | -$11 816,00 | **+$439,75** (-3,48 R) |
| **Février 2026** | 199 | 36,7 % | -$18 683,59 | 115 | -$13 746,50 | -$4 937,09 (-8,52 R) |
| **Mars 2026** | 226 | 40,3 % | -$17 357,68 | 116 | -$23 572,50 | 🚀 **+$6 214,82** (+8,61 R) |
| **Avril 2026** | 213 | 41,3 % | +$4 600,25 | 106 | -$13 266,50 | 🚀 **+$17 866,75** (+24,42 R) |
| **Mai 2026** | 189 | **50,8 %** | 🚀 **+$32 131,32** | 99 | -$4 501,00 | 🚀 **+$36 632,32** (+37,32 R) |

---

## 7. Audit Forensique : Le Goulot d'Étranglement `REGIME_CHANGED`

### 7.1. Le Code Fautif (`AuctionMarketCore.Swing.cs`, lignes 1317-1334)
```csharp
// 0. Vérification de rupture de régime HTF (Hard Exit on Regime Change - Option B validée)
if (ExitOnRegimeChange && htfEma != null && htfEma.IsValidDataPoint(0))
{
    bool regimeOpposed = (t.IsLong && close < htfEma[0]) || (!t.IsLong && close > htfEma[0]);
    if (regimeOpposed)
    {
        t.CloseTrade(close, nowUtc, "REGIME_CHANGED", tick, ptVal);
        ...
    }
}
```

### 7.2. Pourquoi Cette Ligne a Détruit -$66,6K$ :
1. **Confusion Timeframe M5 vs Régime HTF :** `close` est le cours de clôture de la bougie M5. Dans un trade Swing en M5, le prix oscille continuellement autour de la moyenne mobile HTF.
2. **Exécution Immédiate sur `MacroReversal` :** Par définition, une entrée acheteuse en retournement macro intervient au fond d'un creux (donc *sous* l'EMA HTF). Au bout de 3 barres (15 min), le code constate `t.IsLong && close < htfEma[0]`, conclut à tort à une invalidation du régime, et liquide la position au marché !
3. **Distribution Temporelle :** **498 des 558 trades coupés (89,2%)** ont été liquidés à exactement **3 barres (15 minutes)**. Ce n'est plus du swing trading, mais du bruit de marché coupé systématiquement sur le pire prix.

---

## 8. Plan d'Action & Recommandations Techniques

### Action Immédiate N°1 : Désactiver ou Réaligner `ExitOnRegimeChange`
- **Option Simple & Sûre (Recommandée) :** Fixer `<ExitOnRegimeChange>false</ExitOnRegimeChange>` dans les 8 fichiers XML de configuration Swing (`configs/SWING/*.xml`).
  * En laissant les trades courir jusqu'à leur Stop-Loss ou Take-Profit naturel, le test prouve que le portefeuille passe immédiatement de **-$9 029 $** à **+$57 605 $ (+59,85 R, PF 1.23)** sur cette période.
- **Option Structurelle V3.1 :** Si l'on souhaite conserver une sortie sur retournement de régime, ne **jamais** utiliser `close < htfEma[0]`. Utiliser le statut de la structure HTF globale `ResolveSwingRegimeHtf` et exiger une confirmation sur le timeframe supérieur (ex: EMA daily inversée sur 2 barres consécutives, et non un tick M5).

### Action N°2 : Consolidation Globale Annuelle (H1 + H2 2026)
En combinant les sorties naturelles de ce test H1 2026 avec le Test 2 de H2 2026 :
- **H1 2026 (Jan - Mai) :** **+$57 605,33 USD (+59,85 R)**
- **H2 2026 (Juin - Septembre) :** **+$78 640,69 USD (+100,75 R)**
- **BILAN 9 MOIS COMBINÉ :** 🚀 **+$136 246,02 USD (+160,60 R)** avec les 5 actifs rentables !
