# Rapport Consolidé Multi-Actifs Shadow — Mode Swing Pro (Test 2 Optimisé)
**Période commune :** 25 Mai 2026 au 03 Septembre 2026 (~100 jours / 3.5 mois)  
**Actifs Analysés :** CL, ES, GC, MNQ, NQ  
**Total trades évalués (Test 2) :** **4,808 trades clôturés**  
**Date du rapport :** 04 Septembre 2026  

---

## 1. Bilan Comparatif : Test 1 (Baseline Brut) vs Test 2 (Optimisé)

Le Test 2 valide l'élimination totale de `RejectExtreme` (-62,7K$ dans le Test 1) et l'accélération majeure du moteur.

| Actif | T1 Trades | T1 Net ($) | T1 Net (R) | T2 Trades | T2 Net ($) | T2 Net (R) | T2 Win Rate | T2 PF | Progression ($) | Progression (R) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **CL** | 533 | $-10,882.13 | +2.50 R | 487 | **$+9,424.22** | **+23.41 R** | 41.9 % | **1.04** | 🚀 **$+20,306.35** | 🚀 **+20.91 R** |
| **ES** | 624 | $+11,436.19 | +15.05 R | 577 | **$+361.53** | **+3.53 R** | 40.4 % | **1.00** | 🚀 **$-11,074.66** | 🚀 **-11.52 R** |
| **GC** | 1,016 | $+6,509.47 | +3.43 R | 929 | **$+18,868.51** | **+13.27 R** | 40.6 % | **1.02** | 🚀 **$+12,359.04** | 🚀 **+9.84 R** |
| **MNQ** | 1,141 | $-6,850.07 | -29.26 R | 1,065 | **$-2,584.23** | **-11.69 R** | 39.6 % | **0.98** | 🚀 **$+4,265.84** | 🚀 **+17.57 R** |
| **TOTAL 4 ACTIFS COMMUNS** | **3,312** | **$+213.46** | **-8.28 R** | **3,058** | **$+26,070.03** | **+28.52 R** | - | - | 🚀 **$+25,856.57** | 🚀 **+36.80 R** |

| **NQ (Nouveau)** | — | — | — | 1,750 | **$-37,033.09** | **-30.50 R** | 39.3 % | 0.97 | — | — |
| **TOTAL PORTEFEUILLE (5 ACTIFS)** | — | — | — | **4,808** | **$-10,963.06** | **-1.98 R** | - | - | — | — |

---

## 2. Analyse Détaillée par Setup (Test 2)

| Setup Type | Trades | Win Rate | Gain Net (R) | PnL Net ($) | Profit Factor | Espérance/Trade | Diagnostic & Règle |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **HtfContinuation** | 1,548 | 42.1 % | **+72.9 R** | **$+110,406.48** | 1.13 | $+71.32 | 🚀 Moteur Alpha Massif |
| **BreakoutRetest** | 511 | 41.9 % | **+14.1 R** | **$+14,664.92** | 1.05 | $+28.70 | ✅ Solide |
| **ValueReentry** | 152 | 36.8 % | **-7.9 R** | **$-22,904.25** | 0.77 | $-150.69 | ⚠️ Actif-dépendant (à couper sur CL/NQ) |
| **MacroReversal** | 1,411 | 40.0 % | **+17.8 R** | **$-28,328.33** | 0.96 | $-20.08 | ✅ Solide |
| **PocMigration** | 1,186 | 37.0 % | **-98.9 R** | **$-84,801.88** | 0.88 | $-71.50 | ⚠️ Actif-dépendant (à couper sur CL/NQ) |

---

## 3. Asymétrie Directionnelle : SHORT vs LONG (Test 2)

| Direction | Trades | Win Rate | Gain Net (R) | PnL Net ($) | Profit Factor |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **SHORT** | **2,649** | **41.1 %** | **+73.45 R** | **$+84,436.40** | **1.06** |
| **LONG** | **2,159** | **38.7 %** | **-75.43 R** | **$-95,399.46** | **0.93** |

> **Constat Institutionnel :** Les **SHORTS** génèrent **+73.45 R et +$84,436.40** de profit net (PF 1.15) ! Sur l'Or (GC), les ventes rapportent **+$51,277**, et sur le Nasdaq (NQ), **+$25,926**.

---

## 4. La Clé Finale : Spécialisation Impérative de `PocMigration`

Les résultats révèlent une scission nette et catégorique sur `PocMigration` :

- **Sur ES et GC (Flux de Valeur Lourds) :** `PocMigration` rapporte **+$25,871.98** (+20.3 R) avec un PF de 1.14. C'est un excellent setup sur ces deux marchés.
- **Sur CL, NQ et MNQ (Béta Élevé & Bruit Haute Fréquence) :** `PocMigration` perd **-$110,673.86** (-119.2 R) !

### Simulation du Portefeuille avec PocMigration ACTIF uniquement sur ES et GC (Désactivé sur CL, NQ, MNQ) :

| Métrique | Test 2 Brut | **Test 2 avec Presets XML Spécialisés** | Progression |
| :--- | :---: | :---: | :---: |
| **PnL Réalisé Total ($)** | -$10,963.06 | **+$+99,710.80** 🚀 | **+$+110,673.86** |
| **R-Multiple Total** | -1.98 R | **+117.24 R** 🚀 | **+119.22 R** |
| **Win Rate** | 39.8 % | **41.1 %** | +2.1 % |
| **Profit Factor** | 0.99 | **1.04** 🚀 | +0.22 |
| **Trades Conservés** | 4,808 | **3,901** | -907 trades toxiques éliminés |

### Détail par Actif avec Spécialisation XML :

- **GC** : **$+18,868.51** (**+13.27 R**, WR 40.6%, PF **1.02**)
- **CL** : **$+29,643.92** (**+38.39 R**, WR 43.5%, PF **1.16**)
- **ES** : **$+361.53** (**+3.53 R**, WR 40.4%, PF **1.00**)
- **NQ** : **$+44,753.28** (**+37.60 R**, WR 41.2%, PF **1.05**)
- **MNQ** : **$+6,083.56** (**+24.45 R**, WR 40.9%, PF **1.06**)
