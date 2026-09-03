# Rapport Consolidé Multi-Actifs Shadow — Mode Swing Pro (Macro AMC)
**Actifs Analysés :** GC (Gold), ES (S&P 500), CL (Crude Oil), MNQ (Micro Nasdaq)  
**Période commune :** 24/25 Mai 2026 au 02 Septembre 2026 (~100 jours / 3.5 mois)  
**Total des signaux bruts évalués :** **7,069 signaux**  
**Total des trades exécutés et clôturés :** **3,312 trades**  
**Date du rapport :** 03 Septembre 2026  

---

## 1. Tableau Comparatif Multi-Actifs (Baseline Brut)

| Actif | Trades | Wins | Losses | Win Rate | Gain Net (R) | PnL Net ($) | Profit Factor | Gain Moy/Win | Perte Moy/Loss | Espérance/Trade |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **GC** | 1,016 | 408 | 608 | **40.2 %** | **+3.43 R** | **$+6,509.47** | **1.01** | $2,212.62 | $-1,474.08 | **$+6.41** |
| **ES** | 624 | 256 | 368 | **41.0 %** | **+15.05 R** | **$+11,436.19** | **1.04** | $1,275.52 | $-856.24 | **$+18.33** |
| **CL** | 533 | 215 | 318 | **40.3 %** | **+2.50 R** | **$-10,882.13** | **0.96** | $1,245.82 | $-876.52 | **$-20.42** |
| **MNQ** | 1,139 | 443 | 696 | **38.9 %** | **-29.39 R** | **$-6,880.07** | **0.96** | $360.85 | $-239.56 | **$-6.04** |
| **TOTAL PORTEFEUILLE** | **3,312** | **1322** | **1990** | **39.9 %** | **-8.41 R** | **$+183.46** | **1.00** | - | - | **$+0.06** |

---

## 2. Asymétrie Directionnelle : SHORT vs LONG

Comme observé sur le Scalping Pro, les positions Swing confirment une asymétrie directionnelle massive sur cette période de 100 jours :

| Direction | Trades | Win Rate | Gain Net (R) | PnL Net ($) |
| :--- | :---: | :---: | :---: | :---: |
| **SHORT** | **1,759** | **40.8 %** | **+35.09 R** | **$+50,437.82** |
| **LONG** | **1,553** | **38.9 %** | **-43.50 R** | **$-50,254.36** |

> **Constat majeur :** Les **SHORTS** génèrent **+35.1 R et +$50,437.82** de gain net, tandis que les **LONGS** accusent un recul de **-43.5 R (-$50,254.36)**, principalement dû aux phases de correction baissière macro sur l'Or et les indices sur cette période.

---

## 3. Analyse des Setups Swing (Le Moteur d'Alpha)

| Setup Type | Trades | Win Rate | Gain Net (R) | PnL Net ($) | Profit Factor | Statut & Recommandation |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **BreakoutRetest** | 324 | 44.4 % | **+28.5 R** | **$+31,330.09** | 1.23 | 🚀 Top Performer |
| **HtfContinuation** | 891 | 40.7 % | **+8.8 R** | **$+18,483.35** | 1.04 | ✅ Solide |
| **MacroReversal** | 347 | 43.8 % | **+34.3 R** | **$+13,713.99** | 1.09 | 🚀 Top Performer |
| **ValueReentry** | 8 | 50.0 % | **+2.0 R** | **$+2,251.68** | 1.84 | ✅ Solide |
| **PocMigration** | 643 | 38.7 % | **-33.7 R** | **$-2,865.96** | 0.99 | ❌ Fort Drag / À couper |
| **RejectExtreme** | 1,099 | 37.3 % | **-48.3 R** | **$-62,729.69** | 0.89 | ❌ Fort Drag / À couper |

---

## 4. Impact Stratégique : Portefeuille Optimisé (Sans RejectExtreme)

`RejectExtreme` cherche à acheter les bas extrêmes et vendre les hauts extrêmes. En régime de tendance forte (Trend Day / Expansion), ce setup agit en contre-tendance brutale et cumule -$62,729 de pertes.

En désactivant simplement `RejectExtreme` ou en le limitant aux contextes de Range pur :

- **PnL Global :** passe de **+$183.46** à **+$62,913.15** (**+39.9 R**, PF **1.06**) !
- **Sur GC seul :** passe de +$6,509 à **+$66,402.39 (+44.9 R)** !
- **Sur ES seul :** passe de +$11,436 à **+$2,384** (avec les shorts HTF très rentables).
- **Sur CL seul :** passe de -$10,882 à **+$1,676.63 (+19.9 R)** !

---

## 5. Recommandations Clés pour le Mode Swing

1. **Prioriser les setups institutionnels de suivi et réintégration :** `BreakoutRetest` (+31,3K$), `MacroReversal` (+13,7K$) et `HtfContinuation` (+18,5K$) constituent le cœur profitable du moteur.
2. **Désactiver ou durcir RejectExtreme en tendance :** En régime de tendance HTF, interdire `RejectExtreme` contre la tendance (déjà prévu par le flag HTF strict).
3. **Exploiter l'asymétrie Short sur GC et ES :** L'alignement vendeur sur les replis HTF offre le meilleur ratio Risque/Rendement institutionnel.
