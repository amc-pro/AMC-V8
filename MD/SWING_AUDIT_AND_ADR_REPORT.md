# Rapport d'Audit Initial, Diagnostic des Dépendances & Architecture Decision Record (ADR) — Système Swing

**Date :** 30 Août 2026  
**Projet :** `amc-pro/AMC-V8` (Moteur `AuctionMarketCore`)  
**Statut :** Phase 1 Complète — Zéro modification de code de production  
**Auteur :** Architecte C# / NinjaTrader 8 Senior  

---

## 1. Contexte & Périmètre de la Phase 1

L'objectif de cette Phase 1 est de réaliser un **audit factuel complet (Zero-Trust)** du moteur `AuctionMarketCore` afin de préparer l'intégration du système **Swing**, sans perturber, dégrader ou modifier silencieusement le fonctionnement institutionnel existant de **`ScalpingPro`**.

Chaque constatation ci-dessous est sourcée avec le fichier exact, le symbole et les lignes de code correspondantes.

---

## 2. Audit Factuel de l'Architecture Existante

### 2.1. Organisation des Classes Partielles & Cycle de Vie NinjaTrader 8

Le moteur principal est structuré en **10 fichiers de classe partielle** dans le namespace `NinjaTrader.NinjaScript.Indicators` :

| Fichier | Rôle & Composants Clés | Lignes |
| :--- | :--- | :--- |
| [`AuctionMarketCore.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L59) | Classe racine (`Indicator`), cycle `OnStateChange`, propriétés publiques, `OnBarUpdate`. | 2 359 |
| [`AuctionMarketCore.Engine.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.Engine.cs#L26) | Moteur de données volumétriques, delta, CVD, micro-structure, OrderFlow VWAP & SD bands. | 3 958 |
| [`AuctionMarketCore.Features.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.Features.cs#L19) | Détection de patterns Footprint (absorption, iceberg, exhaustion, imbalances). | 425 |
| [`AuctionMarketCore.VolumeProfile.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.VolumeProfile.cs#L21) | Intégration Volume Profile V2 (SQLite, profils clôturés Daily/Weekly/Monthly, POC/VAH/VAL). | 454 |
| [`AuctionMarketCore.MarketIntelligence.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.MarketIntelligence.cs#L21) | Contexte de marché multi-facteurs, calendrier news, régime de volatilité. | 448 |
| [`AuctionMarketCore.ScalpingPro.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.ScalpingPro.cs#L49) | Moteur ScalpingPro : `ScalpingProContext`, `WeightedScoreModel`, SMC confluence, footprint evidence. | 1 314 |
| [`AuctionMarketCore.Sniper.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.Sniper.cs#L27) | Moteur Sniper historique N1-N4, scoring, gestion des stops, journal Shadow de trades. | 3 472 |
| [`AuctionMarketCore.Render.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.Render.cs#L26) | Rendu graphique Direct2D/GDI, Dashboard UI, tracés de niveaux VP/VWAP. | 504 |
| [`AuctionMarketCore.Network.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.Network.cs#L26) | Pont réseau TCP/JSON et émetteur Telegram asynchrone sécurisé (`CancellationTokenSource`). | 710 |
| [`AuctionMarketCore.Exports.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.Exports.cs#L24) | Exports CSV temps réel, métriques de session, archivage analytique. | 610 |

#### Cycle de vie NinjaTrader 8 (`OnStateChange`)
* **`State.SetDefaults`** ([`AuctionMarketCore.cs:1316-1514`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1316-L1514)) :
  - Initialise `TradingPreset = SniperMarketPreset.ScalpingPro` ([L1346](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1346)).
  - Configure `Calculate = Calculate.OnEachTick` et `IsSuspendedWhileInactive = false` ([L1322-1325](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1322-L1325)).
  - Appelle `VolumeProfileSetDefaults()` ([L1319](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1319)) et `MarketIntelligenceSetDefaults()` ([L1318](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1318)).
* **`State.Configure`** ([`AuctionMarketCore.cs:1515-1607`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1515-L1607)) :
  - Exécute `ApplyTradingPreset()` ([L1525](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1525)) qui appelle `ApplyScalpingProPreset()` ([L1311](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1311)).
  - Configure les séries de données avec `AddVolumetric()` et `AddDataSeries()` ([L1584-1605](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1584-L1605)).
* **`State.DataLoaded`** ([`AuctionMarketCore.cs:1608-1676`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1608-L1676)) :
  - Initialise les indicateurs sous-jacents : `OrderFlowVWAP`, `ATR(Regime)`, `ATR(Risk)`, `EMA(HTF)`.
  - Instancie `VolumeProfileManager` et charge l'historique SQLite ([`AuctionMarketCore.VolumeProfile.cs:99-115`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.VolumeProfile.cs#L99-L115)).
  - Initialise le journal de trades `JournalWriterService`, le moteur Sniper/ScalpingPro et le pont TCP/Telegram.
* **`State.Terminated`** ([`AuctionMarketCore.cs:1677-1724`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1677-L1724)) :
  - Nettoie les connexions réseau, flushe les signaux en cours vers le journal (`FlushOpenSignalsAtSessionEnd`), libère la mémoire et supprime les objets de dessin Direct2D.

#### Points d'Entrée & Exécution des Barres
* **`OnBarUpdate()`** ([`AuctionMarketCore.cs:1726-1872`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1726-L1872)) :
  - Point d'entrée principal. Gère la détection de clôture de barre (`bool isBarClose = IsFirstTickOfBar && barIdx > 0;` [L1785](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1785)).
  - En mode `EvaluateOnBarClose = true`, l'évaluation s'exécute avec un décalage `evalOffset = 1` sur la barre fermée ([L1801-1808](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1801-L1808)), garantissant une stricte absence de biais lookahead / intrabar repaint.
  - Déclenche `SniperOnEvaluatedBar()` ([`AuctionMarketCore.Sniper.cs:1011`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.Sniper.cs#L1011)), qui à son tour déclenche `ScalpingProOnEvaluatedBar()` ([`AuctionMarketCore.ScalpingPro.cs:977`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.ScalpingPro.cs#L977)) et `ApplyScalpingProPipeline` ([L992](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.ScalpingPro.cs#L992)).
* **`OnMarketData()`** :
  - **Constat factuel** : `OnMarketData` n'est **pas implémenté / non surchargé** dans le projet. L'indicateur opère exclusivement via les événements de barres volumétriques NinjaTrader (`OnBarUpdate`) avec `Calculate.OnEachTick`.

---

### 2.2. Séries Temporelles & Gestion de `BarsInProgress`

Actuellement, dans `State.Configure` ([`AuctionMarketCore.cs:1578-1605`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L1578-L1605)) :
* `BarsArray[0]` : Série primaire du graphique (par exemple 1 min, 2 min ou 5 min).
* `BarsArray[volumetricBarsIndex]` : Série volumétrique (généralement index 0 si le chart est volumétrique, ou index 1 créé via `AddVolumetric`).
* `BarsArray[htfBarsIndex]` : Série HTF minute (par défaut `HtfMinutes = 60`, soit 1H, index 1 ou 2).

> [!NOTE]
> **Opportunité Swing** : Le module Volume Profile V2 ([`VolumeProfile/VolumeProfileModels.cs:11-16`](file:///c:/AMC-Pro/AMC-V8/VolumeProfile/VolumeProfileModels.cs#L11-L16)) supporte déjà nativement les périodes `Daily`, `Weekly` et `Monthly`. Le système Swing pourra donc s'appuyer sur les profils clôturés SQLite multi-périodes sans obliger le chart principal à surcharger des dizaines de séries secondaires intrabar.

---

### 2.3. Moteurs Existants : Volume Profile, VWAP, Delta & SMC

1. **Volume Profile V2 & SQLite** :
   - Fichiers autonomes dans `VolumeProfile/` (`VolumeProfileManager.cs`, `VolumeProfileAnalyzer.cs`, `VolumeProfileRepository.cs`, `VolumeProfileModels.cs`, `VolumeProfileCalculator.cs`).
   - Mémorise et calcule de manière déterministe les POC, VAH, VAL, HVN, LVN sur les sessions clôturées, persistées dans SQLite ([`VolumeProfileRepository.cs`](file:///c:/AMC-Pro/AMC-V8/VolumeProfile/VolumeProfileRepository.cs)).
2. **OrderFlow VWAP & Bandes SD** :
   - Géré par `OrderFlowVWAP` de NinjaTrader dans `AuctionMarketCore.Engine.cs` avec clôture gelée (`prevBarVwap`, `closedVwapSd`).
3. **Smart Money Concepts (SMC) & Footprint** :
   - `AuctionMarketCore.ScalpingPro.cs` dispose d'un `SmcMarketStructureTracker` interne ([L149-536](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.ScalpingPro.cs#L149-L536)) qui suit les swings highs/lows, BOS, CHoCH, Order Blocks et liquidité.

---

### 2.4. Indicateur vs Stratégie & Journal Shadow

* **Nature de `AuctionMarketCore`** : C'est formellement un `NinjaTrader.NinjaScript.Indicators.Indicator` ([`AuctionMarketCore.cs:59`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L59)).
* **Gestion des ordres & Exécution** :
  - L'indicateur ne passe pas d'ordres réels directs au courtier (interdit pour un `Indicator` NT8 standard sans passer par des architectures tierces ou `Strategy`).
  - L'exécution est gérée par le **moteur Shadow Trade** ([`AuctionMarketCore.Sniper.cs:3194-3472`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.Sniper.cs#L3194-L3472)) : chaque signal validé instancie un `TrackedTrade`, calcule les stops/objectifs exacts, simule l'exécution barre par barre avec détection de slippage/MFE/MAE, gère le trailing et exporte le journal vers `shadow/trades.csv` et `shadow/outcomes.csv`.
  - Les signaux sont également diffusés en temps réel via le pont réseau TCP ([`AuctionMarketCore.Network.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.Network.cs)) et Telegram.

---

### 2.5. Configurations Existantes (`configs/SCALPING_PRO/`)

Les 8 configurations de production sont actives et vérifiées :
* `CONFIG_MNQ_SCALPING_PRO.xml`, `CONFIG_NQ_SCALPING_PRO.xml`
* `CONFIG_MES_SCALPING_PRO.xml`, `CONFIG_ES_SCALPING_PRO.xml`
* `CONFIG_MGC_SCALPING_PRO.xml`, `CONFIG_GC_SCALPING_PRO.xml`
* `CONFIG_MCL_SCALPING_PRO.xml`, `CONFIG_CL_SCALPING_PRO.xml`

Chaque XML contient `<TradingPreset>ScalpingPro</TradingPreset>` ([ex. ES:L59](file:///c:/AMC-Pro/AMC-V8/configs/SCALPING_PRO/CONFIG_ES_SCALPING_PRO.xml#L59)) et des paramètres de risque stricts (`MinStopTicks`, `MaxStopTicks`, `RiskPerTradeCurrency`).

---

## 3. Analyse des Risques et Dépendances

| Risque Identifié | Impact Potentiel | Solution d'Architecture Retenue |
| :--- | :--- | :--- |
| **Régression sur ScalpingPro** | Altération des seuils ou signaux scalping de production. | **Isolation totale** : Branchement conditionnel strict basé sur `TradingPreset == SniperMarketPreset.Swing` / `IsSwing`. Aucun paramètre partagé muté. |
| **Lookahead Bias sur Séries HTF** | Faux signaux de backtest sur barres 4H/Daily en cours. | Évaluation stricte sur `CurrentBars[x] - 1` ou données gelées de sessions clôturées SQLite. |
| **Saturation Mémoire / CPU NinjaTrader** | Ralentissement dû au calcul de VP Swing sur 1 an de ticks. | Exploitation du cache SQLite pré-calculé de `VolumeProfileRepository` au lieu de recalculer tick-par-tick. |
| **Corruption des Templates XML NT8** | Crash lors du chargement de templates existants. | Décoration stricte des propriétés internes Swing avec `[XmlIgnore]` et `[Browsable(false)]`. |
| **Stop Irréaliste lors de Gaps Swing** | Faux calcul de R/R lors de franchissement violent overnight. | Moteur de risque Swing avec slippage de gap et recalcul de stop sur barre d'ouverture. |

---

## 4. Architecture Decision Record (ADR)

### Décision 1 : Structure en Classe Partielle Dédiée
* **Choix retenu :** Création de `AuctionMarketCore.Swing.cs` et `AuctionMarketCore.Swing.Models.cs`.
* **Justification :** Maintient la cohérence avec le reste du projet (`AuctionMarketCore.ScalpingPro.cs`, `AuctionMarketCore.VolumeProfile.cs`) tout en évitant les allocations superflues et en permettant un accès direct aux séries de barres sans surcoût d'interfaçage.

### Décision 2 : Extension de l'Énumération `SniperMarketPreset`
* **Choix retenu :** Ajout de la valeur `Swing` dans `SniperMarketPreset` ([`AuctionMarketCore.cs:46-57`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs#L46-L57)).
* **Branchement :**
  ```csharp
  private void ApplyTradingPreset()
  {
      if (TradingPreset == SniperMarketPreset.Swing)
          ApplySwingPreset();
      else
          ApplyScalpingProPreset();
  }
  ```

### Décision 3 : Moteur de Risque Swing Spécifique
* **Choix retenu :** Le dimensionnement de position Swing sera calculé par contrat selon :
  $$\text{Taille} = \max\left(1, \min\left(\text{MaxContractsSwing}, \left\lfloor \frac{\text{RiskPerTradeCurrency}}{\text{StopTicks} \times \text{TickValue} + \text{SlippageBuffer}} \right\rfloor\right)\right)$$
  avec application stricte de `MinStopTicks` et `MaxStopTicks` calibrés pour chaque actif Swing (ES vs NQ vs GC vs CL).

### Décision 4 : Journal Shadow Swing Dédié
* **Choix retenu :** Séparation du journal Shadow de trades dans `shadow/swing_trades.csv` pour ne pas mélanger les statistiques de scalping intraday (5-15 min) avec les statistiques swing (heures / jours).

---

## 5. Validation de la Phase 1 & Préparation de la Phase 2

* **Tests unitaires existants :** Exécutés avec succès (**35/35 PASS** sur `Tests/VolumeProfileTests.csproj`).
* **Intégrité du code :** Aucun fichier de production n'a été modifié lors de cette phase d'audit.
* **Prêt pour la Phase 2 :** 
  - La Phase 2 consistera à créer `AuctionMarketCore.Swing.Models.cs` et les 8 fichiers de configuration sous `configs/SWING/`.

---
*Fin du rapport d'audit et ADR Phase 1.*
