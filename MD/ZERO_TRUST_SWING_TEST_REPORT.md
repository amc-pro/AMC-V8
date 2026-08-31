# Rapport de Validation Zero-Trust — Suite de Tests Unitaires & Intégration Swing (AMC-V8)

**Date :** 31 Août 2026  
**Projet :** `amc-pro/AMC-V8` (Moteur `AuctionMarketCore`)  
**Runner :** `dotnet run --project Tests/VolumeProfileTests.csproj`  
**Résultat Global :** **63/63 TESTS RÉUSSIS (100 % PASS)** — 0 Échec  

---

## 1. Matrice des 20 Tests Unitaires Swing

| # | Nom du Test | Données d'Entrée | Comportement Attendu | Résultat Observé | Statut |
| :-: | :--- | :--- | :--- | :--- | :-: |
| **01** | `Test_Swing_01_AntiLookahead_StrictClosedBars` | Contexte `SwingContext` sur barre index 100 | Données immuables, aucune lecture intrabar de barre non confirmée. | `BarIndex=100`, `Close=5005.00` | **PASS** |
| **02** | `Test_Swing_02_Deterministic_VP_Closed_Calculations` | Profil clôturé `ES_DAY_2026-08-28` | POC, VAH, VAL calculés et restitués de façon déterministe. | `POC=5000`, `VAH=5020`, `VAL=4980` | **PASS** |
| **03** | `Test_Swing_03_Closed_VWAP_And_SD_Bands` | Profil `Vwap=5000`, `StdDev=20` | Hiérarchie stricte des bandes SD supérieures et inférieures. | $\text{SD3} > \text{SD2} > \text{SD1} > \text{VWAP}$ | **PASS** |
| **04** | `Test_Swing_04_MarketRegime_Classification` | Énumération `SwingMarketRegime` | 6 régimes distincts (`TrendUp`, `TrendDown`, `Balance`, `Expansion`, `Compression`, `Transition`). | 6 régimes valides | **PASS** |
| **05** | `Test_Swing_05_RejectExtreme_And_ValueReentry_Setups` | Prix à 4965 avec rejet de `Sd2Lower` (4960) | Validation de la précondition `RejectExtreme Long`. | Rejet confirmé sur clôture | **PASS** |
| **06** | `Test_Swing_06_Breakout_Retest_Setup` | Prix à 5030 après retest de `DailyVah` (5020) | Validation du retest haussier au-dessus de VAH. | Setup validé | **PASS** |
| **07** | `Test_Swing_07_SMC_Structure_And_OrderFlow_Validation` | Confluence `BOS`, `CHoCH`, `FVG`, `Delta > 0` | Score pondéré total supérieur au seuil Fort ($\ge 70/100$). | `TotalScore >= 70.0` | **PASS** |
| **08** | `Test_Swing_08_Hybrid_Stop_Atr_And_Structural` | Entrée 5000, Structure 4985 (60t), ATR 10 (80t) | Priorité sécuritaire au stop le plus protecteur (ATR 80t). | Stop = 80 ticks (4980.00) | **PASS** |
| **09** | `Test_Swing_09_PositionSizing_By_TickValue` | Risque $250 ES ($12.50/t) vs Risque $50 MES ($1.25/t) | Calcul exact du nombre de contrats par formule tick-value. | ES: 1 contrat, MES: 1 contrat | **PASS** |
| **10** | `Test_Swing_10_Strict_MinMax_StopTicks_Clamping` | Stops extrêmes : 2 ticks et 200 ticks | Bornage strict entre `MinStopTicks` (16) et `MaxStopTicks` (80). | Clamp 16t et 80t vérifié | **PASS** |
| **11** | `Test_Swing_11_AntiStacking_Protection` | Signal `ES Long` déjà actif | Empêchement de duplication d'exposition dans le même sens. | Trade unique maintenu | **PASS** |
| **12** | `Test_Swing_12_Idempotence_After_Recalculation` | Double évaluation sur même barre clôturée | Identité parfaite du score et de l'état généré. | `Score1 == Score2` | **PASS** |
| **13** | `Test_Swing_13_NewsFilter_And_Severity_Blackout` | `InNewsWindow=true`, `NewsSeverity=2` | Blocage préventif avec code `HIGH_SEVERITY_NEWS_BLOCK`. | Entrée rejetée avec motif exact | **PASS** |
| **14** | `Test_Swing_14_Gaps_And_Rollover_Handling` | Gap d'ouverture de 1.5% | Application d'une pénalité de score ($\ge 10$ points). | `Penalties >= 10.0` | **PASS** |
| **15** | `Test_Swing_15_PartialExits_TP1_TP2_And_BreakEvenTrailing` | Touche TP1 (5030) puis TP2 (5060) | Déplacement stop à Break-Even + 1 tick, puis clôture finale $R \ge 3.0$. | BE+1t validé, $R \ge 3.0$ confirmé | **PASS** |
| **16** | `Test_Swing_16_ScalpingPro_NonRegression_Isolation` | Code source `AuctionMarketCore.cs` | Présence isolée des deux presets et de leurs méthodes d'application. | Isolation stricte confirmée | **PASS** |
| **17** | `Test_Swing_17_XmlConfiguration_Parsing_All_8_Instruments` | 8 XML sous `configs/SWING/` | Présence de `<TradingPreset>Swing</TradingPreset>`, `MinStopTicks`, `MaxStopTicks`. | 8/8 XML conformes | **PASS** |
| **18** | `Test_Swing_18_Deployment_And_Sync_Integrity` | Racine du dépôt | Présence des fichiers `AuctionMarketCore.Swing.cs` et `.Models.cs`. | Fichiers présents | **PASS** |
| **19** | `Test_Swing_19_Path_Security_And_No_Secrets_Leak` | 8 fichiers XML `configs/SWING/` | Absence totale de tokens réels ou chemins absolus machine. | Aucun secret en clair | **PASS** |
| **20** | `Test_Swing_20_No_Dead_Code_Or_Orphaned_Presets` | Fichier `AuctionMarketCore.Swing.cs` | Absence d'anciens noms obsolètes (`AuctionMarketScalpingPro`, etc.). | Code propre et assaini | **PASS** |

---

## 2. Matrice des 5 Tests d'Intégration Stateful & Persistance SQLite

| # | Nom du Test | Données d'Entrée | Comportement Attendu | Résultat Observé | Statut |
| :-: | :--- | :--- | :--- | :--- | :-: |
| **21** | `Test_Swing_Integration_SQLite_Persistence_And_Reload` | Position `TRD_ES_001` de 2 contrats avec passage TP1 et TP2 | Sauvegarde atomique SQLite, restauration exacte après réouverture, mise à jour des contrats restants et purge après clôture. | Rechargement 1 trade actif, contrats $2 \rightarrow 1 \rightarrow 0$ | **PASS** |
| **22** | `Test_Swing_Integration_TwoStep_Partial_Exit_TP1_BE_TP2` | Entrée 5000 (2 contrats), TP1=5030, TP2=5060 | Sortie 50% à TP1 ($+\$1500$), stop trailé à $5000.25$ (BE+1t), sortie finale TP2 ($+\$3000$). Gain total $=\$4500$, $R=2.25$. | TP1 50% soldé, BE+1t actif, $R=2.25$ | **PASS** |
| **23** | `Test_Swing_Integration_Stop_Before_TP1_Full_Loss` | Entrée NQ 18000 (2 contrats), Stop initial 17950 | Déclenchement du Stop initial avant TP1 : perte totale de $-1.0R$ ($-\$2000$). | $R=-1.0$, Perte $-\$2000$ validée | **PASS** |
| **24** | `Test_Swing_Integration_Dynamic_News_And_Gap_Penalty` | News sévère (sécurité 2) et gap d'ouverture de 2.0% | Blocage dur de l'entrée pendant news et pénalité de score $\ge 10$ pts sur gap. | `HIGH_SEVERITY_NEWS_BLOCK` & Pénalité validées | **PASS** |
| **25** | `Test_Swing_Integration_Overnight_Session_Transition` | Position active de 2 contrats à la clôture de session CME | Maintien intact du trade en statut `ACTIVE` pour la session suivante. | Statut `ACTIVE`, 2 contrats préservés | **PASS** |

---

## 3. Matrice des 3 Tests POC Migration Model (6ème Setup)

| # | Nom du Test | Données d'Entrée | Comportement Attendu | Résultat Observé | Statut |
| :-: | :--- | :--- | :--- | :--- | :-: |
| **26** | `Test_PocMigration_Analyzer_Detects_Upward_Drift` | 4 profils Daily consécutifs montants ($5000 \rightarrow 5015 \rightarrow 5030 \rightarrow 5050$) | Détection direction Long, 3 transitions consécutives, drift 200 ticks, force $\ge 60/100$, `IsMigrationValid = true`. | Long, 3 sessions, 200t drift, Force $\ge 60$ | **PASS** |
| **27** | `Test_PocMigration_Analyzer_Rejects_Inconsistent_Drift` | 4 profils en zigzag ($5000 \rightarrow 5020 \rightarrow 5010 \rightarrow 5030$) | Rejet de la migration (`IsMigrationValid = false`) faute de consistance directionnelle $\ge 3$ sessions. | Rejeté avec succès | **PASS** |
| **28** | `Test_PocMigration_Setup_Scoring_And_Preconditions` | Contexte migration Long sur pullback (prix près du POC dans la VA) | Validation préconditions, score $\ge 60/100$, rejet si achat hors VA (chase) ou direction opposée. | Validé sur pullback, rejeté sur chase | **PASS** |

---

## 4. Synthèse de Couverture Globale

* **Suite Volume Profile V7.9 :** 35/35 PASS
* **Suite Swing Unitaire V8.0 :** 20/20 PASS
* **Suite Swing Intégration Stateful & SQLite V8.0 :** 5/5 PASS
* **Suite POC Migration Model V8.0 :** 3/3 PASS
* **Total Exécuté :** **63/63 PASS (100%)**
* **Temps d'Exécution Global :** ~1.10 seconde
