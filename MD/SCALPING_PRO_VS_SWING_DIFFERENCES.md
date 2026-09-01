# Comparatif Détaillé — ScalpingPro vs Swing (AMC-V8)

Ce document met en lumière les différences d'architecture, de fréquence, de gestion du risque et d'horizon temporel entre le mode **ScalpingPro** et le mode **Swing** au sein du moteur institutionnel `AuctionMarketCore`.

---

## 1. Tableau Comparatif des Paramètres Fondamentaux

| Caractéristique | ScalpingPro (Intraday Haute Confluence) | Swing (Macro Auction Market) |
| :--- | :--- | :--- |
| **Horizon Temporel** | 5 minutes à 60 minutes (Intrasession) | Plusieurs heures à plusieurs jours (Intersession) |
| **Timeframe de Base** | 1 min, 2 min ou 5 min Volumetric | 15 min ou 60 min |
| **Séries HTF Référence** | 15 min / 60 min (EMA 50) | 240 min (4 Heures) / Daily (1440 min) |
| **Fréquence de Trades** | 5 à 10 setups par session | 1 à 4 setups par semaine |
| **Références de Niveaux** | Session courante + Composite 15 jours | Profils clôturés Daily, Weekly, Monthly SQLite |
| **Bandes SD Référence** | Intraday SD ±1 / ±2 | Bandes SD ±2 / ±3 Mois & Semaine Clôturées |
| **Multiplicateur Stop ATR** | $1.75 \times \text{ATR}$ (adapté au micro-bruit) | $2.0 \text{ à } 2.25 \times \text{ATR}$ (respiration macro) |
| **Bornes Stops (ES)** | Min 12 ticks (3 pts) / Max 160 ticks | Min 16 ticks (4 pts) / Max 80 ticks (20 pts) |
| **Bornes Stops (NQ)** | Min 12 ticks (3 pts) / Max 160 ticks | Min 40 ticks (10 pts) / Max 240 ticks (60 pts) |
| **Rapport R/R Visé** | TP1: $1.0\text{R}$, TP2: $2.0\text{R}$ (Min R/R = 1.0) | TP1: $1.5\text{R}$, TP2: $3.0\text{R}$ (Min R/R = 1.5) |
| **Gestion Overnight** | Clôture obligatoire à la fin de session RTH | Maintien de position autorisé avec sizing adapté |
| **Sorties & Trailing** | Trailing ATR intraday | TP1 partiel + Stop Break-Even (+ 1 tick) |
| **Journal Shadow Cible** | `shadow/trades.csv` | `shadow/swing_trades.csv` |

---

## 2. Typologie des Setups Exploités

### Setups ScalpingPro :
1. **Reversals Microstructure :** `FINISHED_AUCTION` (zéro contrat), `NPOC` (Naked POC retest), `FAILED_AUCTION`.
2. **Impulsions Order Flow :** `DELTA_FLIP`, `CUM_DELTA_DIV` (divergence delta cumulé intraday).
3. **Footprint Requis :** Preuve d'absorption ou d'imbalance obligatoire ($\ge 0.30$ d'évidence).

### Setups Swing :
1. **`RejectExtreme` :** Rejet statistique violent des bandes SD ±2 / ±3 Mois/Semaine clôturées.
2. **`ValueReentry` :** Réintégration de la Value Area d'une période clôturée (VAH/VAL) avec visée POC opposé.
3. **`BreakoutRetest` :** Franchissement net d'un niveau institutionnel macro suivi d'un retest défendu.
4. **`MacroReversal` :** Divergence delta/CVD de fond avec absorption macro.
5. **`HtfContinuation` :** Pullback vers FVG ou VWAP institutionnel dans le sens de la tendance 4H.

---

## 3. Règle d'Or d'Isolation

Les deux systèmes partagent les couches d'infrastructures communes de bas niveau (moteur de calcul de volume profile, pont SQLite, parseur XML, pont réseau Telegram), mais leurs **arbres de décision, modèles de scoring et journaux de positions sont totalement étanches et indépendants**.
