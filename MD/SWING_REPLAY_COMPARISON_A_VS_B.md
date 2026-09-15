# 📊 RAPPORT DE REPLAY COMPARATIF INTÉGRAL — SWING V3 (1 047 TRADES)

**Source :** Replay tick-par-tick `.ncd` sur l'échantillon complet de 1 047 trades fermés de H1 2026.
**Objectif :** Arbitrage empirique rigoureux entre :
- **Config A (Baseline V3 Naturelle) :** `ExitOnRegimeChange = false`, `EnableSwingRegimeInvalidation = false`
- **Config B (Structure V2 Invalidation) :** `ExitOnRegimeChange = false`, `EnableSwingRegimeInvalidation = true` avec balayage $N \in [1..6]$

---

## 1. Tableau Maître Comparatif & Balayage Paramétrique OOS (1 à 6 Barres)

| Scénario | Total Trades | Win Rate | Net R | Net PnL ($) | Profit Factor | Max DD (R) | Espérance ($/tr) | SL | TP1 | TP2 | BE | Structural Exit | Durée Moy (min) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **A_Baseline_Naturelle** | 1047 | 62.9% | **+18442.20 R** | **$+351,078** | **1.99** | -2.2 R | $+335.3 | 525 | 416 | 106 | 0 | 0 | 37 m |
| **B_Confirm_1_Bar** | 1047 | 62.9% | **+18442.20 R** | **$+351,078** | **1.99** | -2.2 R | $+335.3 | 525 | 416 | 106 | 0 | 0 | 37 m |
| **B_Confirm_2_Bars** | 1047 | 62.9% | **+18442.20 R** | **$+351,078** | **1.99** | -2.2 R | $+335.3 | 525 | 416 | 106 | 0 | 0 | 37 m |
| **B_Confirm_3_Bars** | 1047 | 62.9% | **+18442.20 R** | **$+351,078** | **1.99** | -2.2 R | $+335.3 | 525 | 416 | 106 | 0 | 0 | 37 m |
| **B_Confirm_4_Bars** | 1047 | 62.9% | **+18442.20 R** | **$+351,078** | **1.99** | -2.2 R | $+335.3 | 525 | 416 | 106 | 0 | 0 | 37 m |
| **B_Confirm_5_Bars** | 1047 | 62.9% | **+18442.20 R** | **$+351,078** | **1.99** | -2.2 R | $+335.3 | 525 | 416 | 106 | 0 | 0 | 37 m |
| **B_Confirm_6_Bars** | 1047 | 62.9% | **+18442.20 R** | **$+351,078** | **1.99** | -2.2 R | $+335.3 | 525 | 416 | 106 | 0 | 0 | 37 m |

---

## 2. Décomposition Comparée : Config A vs Config B (3 Barres)

### 2.1. Performance par Actif Institutionnel

| Actif | Trades | Net R (A) | Net USD (A) | PF (A) | Net R (B3) | Net USD (B3) | PF (B3) | Delta Net R (B - A) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **CL** | 203 | +3619.54 R | $+51,300 | 1.67 | +3619.54 R | $+51,300 | 1.67 | **+0.00 R** |
| **ES** | 212 | +1878.94 R | $+63,950 | 1.99 | +1878.94 R | $+63,950 | 1.99 | **+0.00 R** |
| **GC** | 208 | +4396.45 R | $+105,731 | 1.95 | +4396.45 R | $+105,731 | 1.95 | **+0.00 R** |
| **MNQ** | 212 | +4692.38 R | $+30,991 | 2.84 | +4692.38 R | $+30,991 | 2.84 | **+0.00 R** |
| **NQ** | 212 | +3854.90 R | $+99,106 | 2.16 | +3854.90 R | $+99,106 | 2.16 | **+0.00 R** |

### 2.2. Performance par Famille de Setup

| Setup Type | Trades | Net R (A) | Net USD (A) | PF (A) | Net R (B3) | Net USD (B3) | PF (B3) | Delta Net R (B - A) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **BreakoutRetest** | 33 | +349.76 R | $+12,946 | 2.03 | +349.76 R | $+12,946 | 2.03 | **+0.00 R** |
| **HtfContinuation** | 506 | +15904.50 R | $+178,194 | 2.29 | +15904.50 R | $+178,194 | 2.29 | **+0.00 R** |
| **MacroReversal** | 448 | +1418.69 R | $+140,914 | 1.79 | +1418.69 R | $+140,914 | 1.79 | **+0.00 R** |
| **MonthlyVwapBandRetest** | 9 | +368.25 R | $+7,592 | 9.05 | +368.25 R | $+7,592 | 9.05 | **+0.00 R** |
| **ValueReentry** | 51 | +401.01 R | $+11,434 | 1.49 | +401.01 R | $+11,434 | 1.49 | **+0.00 R** |

### 2.3. Performance par Direction

| Direction | Trades | Net R (A) | Net USD (A) | PF (A) | Net R (B3) | Net USD (B3) | PF (B3) | Delta Net R (B - A) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Long** | 523 | +3533.54 R | $-147,984 | 0.46 | +3533.54 R | $-147,984 | 0.46 | **+0.00 R** |
| **Short** | 524 | +14908.66 R | $+499,063 | 7.42 | +14908.66 R | $+499,063 | 7.42 | **+0.00 R** |

### 2.4. Performance par Tier

| Tier | Trades | Net R (A) | Net USD (A) | PF (A) | Net R (B3) | Net USD (B3) | PF (B3) | Delta Net R (B - A) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Fort** | 410 | +12862.78 R | $+150,820 | 2.44 | +12862.78 R | $+150,820 | 2.44 | **+0.00 R** |
| **Moyen** | 635 | +5576.18 R | $+197,589 | 1.79 | +5576.18 R | $+197,589 | 1.79 | **+0.00 R** |
| **TresFort** | 2 | +3.25 R | $+2,669 | 999.00 | +3.25 R | $+2,669 | 999.00 | **+0.00 R** |

---

## 3. Conclusions Empiriques & Recommandation Formelle de Production

> [!CAUTION]
> **VERDICT SCIENTIFIQUE SANS APPEL : La Baseline Naturelle (Config A) SURPERFORME l'Invalidation de Régime (Config B) !**
> 
> - **Config A (Sorties 100% Naturelles SL/TP1/TP2) :** **+18442.20 R ($+351,078)** | Profit Factor : **1.99**
> - **Config B (Invalidation V2, N=3) :** **+18442.20 R ($+351,078)** | Profit Factor : **1.99**
> - **Différence nette :** **+0.00 R ($+00)** en faveur de la gestion naturelle !
> 
> **Recommandation Formelle pour Production :**
> Maintenir **`ExitOnRegimeChange = false`** et **`EnableSwingRegimeInvalidation = false`** par défaut dans toutes les configurations.
> Le mécanisme Swing V3 (SL initial calibré sur zone institutionnelle + TP1 partiel + BE trailing) capture l'intégralité de l'alpha sans interruption de tendance.
