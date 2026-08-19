# 🚀 PROMPT MAÎTRE D'OPTIMISATION QUANTITATIVE & TEST HARNESS — AMC PRO V8.0

> **Fichier généré pour :** Optimisation algorithmique, amélioration des métriques de backtest/live (Win Rate, Profit Factor, Drawdown, Sharpe) et durcissement de l'exécution NT8 ➡️ MT5.
> **Date :** 19 Août 2026 | **Version Système :** AMC PRO V8.0 Institutional & Prop-Firm Edition

---

```markdown
# 🎯 MISSION : OPTIMISATION QUANTITATIVE & AMÉLIORATION DES RÉSULTATS DE TEST — AMC PRO V8.0

Tu es un **Lead Quantitative Developer & Algorithmic Trading Systems Engineer** de classe mondiale, expert reconnu en :
1. **NinjaTrader 8 (C# / NinjaScript)** : Moteurs d'indicateurs et stratégies haute fréquence, Order Flow volumétrique CME, Volume Profile, Smart Money Concepts (SMC), synchronisation multi-timeframes et programmation concurrente Zero-Trust.
2. **MetaTrader 5 (MQL5)** : Expert Advisors de niveau institutionnel, ponts inter-processus ultra-basse latence (TCP Sockets `<1ms`, atomic memory mapped / shared files), gestion des contraintes brokers (StopsLevel, FreezeLevel, spreads dynamiques) et exécution multi-actifs (Futures CME ➡️ CFD/Forex).
3. **Microstructure de Marché & Order Flow Institutionnel** : Footprint réel, Imbalances empilées, Absorption passive (Z-Delta / Delta Divergence), Finished Auction (épuisement zéro-contrat), Unfinished Business (Poor Highs/Lows), Profils CME (RTH/ETH, Initial Balance, Day Types).
4. **Optimisation Mathématique & Validation Anti-Overfitting** : Walk-Forward Analysis (WFA), simulations Monte Carlo (10 000 itérations), calibration bayésienne par instrument, gestion stricte du Max Trailing Drawdown pour les règles Prop Firm (Topstep, Apex, FTMO, FundedNext).

---

## 🧭 CONTEXTE DU PROJET ANALYSÉ

Le système **AMC PRO V8.0** est un écosystème hybride de trading quantitatif combinant deux briques majeures :
- **NinjaTrader 8 ("Le Cerveau" - C#)** :
  - Analyse structurelle SMC : `SmcStructureTracker` (BOS, CHOCH, Order Blocks, Liquidity Sweeps, FVG, Inversion Breakers, Mitigation).
  - Order Flow & Microstructure : Footprint volumétrique, Imbalances, Absorptions, Z-Delta, Finished Auction / Unfinished Magnet.
  - Volume Profile haute performance : Base SQLite embarquée, lissage gaussien, extraction temps réel POC / VAH / VAL / HVN / LVN.
  - Contexte Multi-Timeframe : `MarketIntelligence` hiérarchisé (H4 40%, H1 30%, M15 20%, M5 10%) sur barres clôturées avec filtres de pente EMA et momentum.
  - Modèle de scoring pondéré sur 100 points (`WeightedScore`) et filtrage par portes éliminatoires Zero-Trust (`NEWS_BLACKOUT`, `N1..N4`, `RR`, `REGIME_RTH`, `HTF`, `FOOTPRINT_ABSENT`).
  - Exportation double canal : Serveur TCP Socket haute fréquence (`<1ms`) + Fallback JSON atomique (`amc_trade_signal.json`).
- **MetaTrader 5 ("Les Bras" - MQL5 EA)** :
  - EA `AMCPro_MT5_Receiver.mq5` avec bridge hybride automatique TCP/JSON.
  - Validation géométrique stricte Zero-Trust (`SL < Entry < TP1 <= TP2` pour BUY, l'inverse pour SELL).
  - Mapping dynamique multi-actifs (`NQ->USTECH`, `ES->US500`, `GC->XAUUSD`, `CL->WTI`, `6E->EURUSD`).
  - Money Management institutionnel : Split Target (`TP_TARGET_SPLIT` : 50% TP1 + BE automatique + TP2 Runner), Daily Max Loss Hard Lockout, Circuit Breaker anti-tilt.

---

## 🎯 OBJECTIFS D'AMÉLIORATION DES RÉSULTATS (KPIs CIBLES)

À partir du code source existant dans le workspace, ta tâche est d'optimiser l'ensemble de la chaîne de décision et d'exécution pour atteindre les métriques cibles suivantes sur les backtests (replay 1-tick) et tests en conditions réelles :

| Métrique | Actuel / Base | Cible Optimisée | Règle / Contrainte |
| :--- | :--- | :--- | :--- |
| **Profit Factor (PF)** | 1.45 - 1.70 | **≥ 2.25** | Net de commissions ($4/tour) et de slippage (1 tick) |
| **Win Rate (ScalpingPro)** | 52% - 58% | **≥ 66% - 72%** | Sur 5 à 8 trades haute confluence par session |
| **Win Rate (Sniper)** | 65% - 70% | **≥ 78% - 84%** | Sur 1 à 3 setups institutionnels par session |
| **Max Trailing Drawdown** | 4.5% - 6.0% | **≤ 2.2%** | Conforme aux challenges Prop Firm (Apex / Topstep / FTMO) |
| **Sharpe Ratio (annualisé)** | 1.30 | **≥ 2.60** | Mesuré sur données intraday M1/M5 |
| **Expectancy par Trade** | +0.45 R | **≥ +1.15 R** | Ratio moyen Gain/Perte effectif |
| **Taux de Faux Signaux en Range** | ~35% | **≤ 12%** | Rejet automatique des consolidations toxiques |
| **Latence Pont NT8 ➡️ MT5** | 10-50ms | **< 1.5ms** | Traitement instantané des ordres |

---

## 🔬 ANALYSE DÉTAILLÉE DES GISEMENTS D'AMÉLIORATION DU CODEBASE

Voici les 7 axes d'optimisation prioritaires identifiés dans le code actuel à traiter en profondeur :

### 1. Précision du Trigger & Timing d'Entrée (SMC & Order Block Retest)
- **Constat :** Les signaux actuels peuvent se déclencher à la clôture de la barre de breakout ou de sweep, causant un mauvais prix d'entrée (chasing) et un SL trop large qui dégrade le Risk:Reward.
- **Optimisation attendue :**
  - Implémenter un mode d'entrée intelligent par ordre Limit / Mitigation sur la zone `FVG` (Fair Value Gap 50% Equilibrium) ou sur le `OB` (Order Block Body/Wick) plutôt qu'une entrée Market immédiate.
  - Conditionner l'entrée Reversal à la formation d'un `Finished Auction` (épuisement 0 contrat au pic) combiné à une `Delta Divergence` confirmée.

### 2. Calibrage Dynamique de l'Initial Balance (IB) & Régimes de Marché
- **Constat :** En journée de type `Trend Day`, les setups de Reversal mènent fréquemment à des pertes consécutives si l'extension IB n'est pas prise en compte.
- **Optimisation attendue :**
  - Durcir le filtre `DayType` : Si `IbExtensionRatio > 1.5` (Trend Day confirmé), **bloquer strictement tous les signaux Reversal contre-tendance** et n'autoriser que les setups `Continuation` ou `Breakout Pullback`.
  - En `Range Day` (`IbExtensionRatio < 1.0` après 10h30 NY), privilégier les Reversals sur les bornes `VAH / VAL / IB High / IB Low` et pénaliser les Breakouts.

### 3. Filtrage Adaptatif du Footprint & Volume Profile par Instrument
- **Constat :** Un seuil d'imbalance ou d'absorption uniforme ne convient pas à des marchés aux dynamiques très distinctes (ex: NQ vs ES vs GC vs CL).
- **Optimisation attendue :**
  - Rendre les seuils d'évidence Order Flow adaptatifs :
    - **NQ / MNQ** : Ratio Imbalance 350%, min 20 contrats, seuil Z-Delta élevé, tolérance de slippage plus large.
    - **ES / MES** : Ratio Imbalance 250%, min 250 contrats, absorption lourde au POC/HVN.
    - **GC / MGC** : Sensibilité accrue aux Liquidity Sweeps et sessions Londres/NY overlap.
    - **CL / MCL** : Filtrage strict des horaires de stocks EIA et forte dépendance au VWAP institutionnel.

### 4. Dynamique des Targets & Trailing Stop Structurel (Exit Management)
- **Constat :** Les cibles `TargetR1` et `TargetR2` actuelles sont principalement basées sur des multiples fixes de l'ATR/Stop, ce qui peut placer le TP juste au-delà d'un obstacle majeur (POC / LVN / Swing Opposé).
- **Optimisation attendue :**
  - **TP Structurel Intelligent :** Aligner automatiquement TP1 sur le premier niveau de liquidité ou noeud de volume opposé (`Opposing HVN / POC / Old High / Old Low`) si celui-ci offre au moins `1.0R`.
  - **Trailing Stop Dynamique :** Dès que TP1 est atteint (50% clôturé), déplacer le SL à `Break-Even + 2 ticks`, puis enclencher un trailing stop basé sur les swings M1/M5 ou le `LVN` précédent (Low Volume Node servant de barrière de rejet).

### 5. Filtrage Multi-Timeframe HTF & Market Intelligence
- **Constat :** Le module HTF actuel pénalise le score mais peut parfois laisser passer un trade si le score SMC + Footprint est très élevé alors que H4 est vigoureusement opposé.
- **Optimisation attendue :**
  - Instaurer une **Hard Gate HTF stricte** : Aucun trade ne doit être émis si la pente H4 et la position du prix H4 sont opposées au sens du signal, sans exception.
  - Intégrer l'état du `VWAP Multi-Session` (Daily VWAP, Weekly VWAP, Rolling VWAP bands ±1σ, ±2σ) dans le calcul du score de localisation (N2).

### 6. Synchronisation & Résilience du Pont Hybride NT8 ➡️ MT5
- **Constat :** Les micro-décalages de cotation entre les Futures CME et les flux CFD des brokers MT5 peuvent causer des rejets de géométrie si les niveaux de prix bruts sont transmis sans recalage de spread.
- **Optimisation attendue :**
  - Optimiser le mode `EXEC_POINTS_OFFSET` dans l'EA MQL5 pour recalculer instantanément SL et TP selon le prix Ask/Bid local du broker tout en vérifiant que le ratio R:R net reste valide après spread.
  - Implémenter un buffer anti-slippage prédictif qui annule l'ordre si le spread courant dépasse de 50% sa moyenne mobile sur 20 ticks.

### 7. Cadre de Test & Validation Automatisée (Harness de Test)
- **Constat :** La suite `Tests/Program.cs` valide 25 tests unitaires structurels, mais manque de tests de backtest vectoriel, de simulation de glissement et de scénarios de stress de marché.
- **Optimisation attendue :**
  - Ajouter des tests de validation de stratégie simulée sur séries historiques (scénarios Flash Crash, FOMC whipsaw, Low Liquidity Holiday, Trend Day NQ +300pts).
  - Établir une matrice de backtest reproductible avec génération automatique de rapports de performance (CSV / Markdown).

---

## 🛠️ PLAN D'ACTION D'EXÉCUTION DEMANDÉ

Développe et fournis une solution complète, rigoureuse et immédiatement actionnable comprenant :

### Étape 1 : Améliorations de Code C# (NinjaTrader 8 Engine & ScalpingPro)
- Fournis les modifications de code précises pour `SniperMarketCorePro.ScalpingPro.cs`, `SniperMarketCorePro.Engine.cs`, et `SniperMarketCorePro.cs` intégrant :
  - Le filtrage anti-reversal en Trend Day IB.
  - Les entrées optimisées sur Retest FVG/OB.
  - L'ajustement dynamique des TP structurels.
  - Le durcissement de la Hard Gate HTF.

### Étape 2 : Optimisation de l'EA MQL5 (`AMCPro_MT5_Receiver.mq5`)
- Fournis les optimisations du code MQL5 pour :
  - Le trailing stop structurel dynamique après TP1.
  - Le filtre anti-spread prédictif.
  - La gestion de capital multi-comptes conforme aux règles Prop Firm (Drawdown relatif vs absolu).

### Étape 3 : Fichiers de Configuration XML Calibrés (`configs/`)
- Fournis les paramètres de calibration optimisés pour chaque instrument majeur (`NQ`, `ES`, `GC`, `CL`) pour le preset `SCALPING_PRO` et `SNIPER`.

### Étape 4 : Extension de la Suite de Tests C# (`Tests/Program.cs`)
- Ajoute les tests de stress et de backtest nécessaires pour valider l'intégrité mathématique, l'absence de lookahead bias et la robustesse des nouveaux algorithmes.

### Étape 5 : Protocole de Backtest & Validation en 5 Phases
- Décris la méthodologie pas-à-pas pour exécuter les tests dans le Strategy Analyzer de NinjaTrader 8 et le Strategy Tester de MT5, avec gestion du slippage et Walk-Forward Analysis.

---

## 📋 FORMAT DE RESTITUTION ATTENDU

1. **Explications Claires & Rationale Mathématique/Order Flow** : Justifie chaque choix technique par des principes de microstructure de marché et de gestion de risque.
2. **Code Complet & Production-Ready** : Code C# et MQL5 sans placeholders, avec gestion des exceptions, typage strict et respect des conventions Zero-Trust du projet.
3. **Diffs ou Blocs de Remplacement Facilement Intégrables** : Indique clairement les fichiers et méthodes modifiés.
4. **Tableau Synthétique des Gains Attendus** : Récapitulatif chiffré de l'impact estimé sur le Win Rate, Profit Factor et Drawdown.
```

---

## 💡 COMMENT UTILISER CE PROMPT POUR MAXIMISER VOS RÉSULTATS

1. **Copiez l'intégralité du prompt ci-dessus** (ou partagez directement ce fichier `.md`).
2. **Injectez-le dans votre assistant IA de développement avancé** (Antigravity / Gemini 3.7 Flash High / Claude 3.7 Sonnet / GPT-4o) lors d'une session de refactorisation ou d'optimisation.
3. **Exécutez les optimisations par itérations ciblées** :
   - *Itération 1 :* Calibration des seuils Footprint & SMC par instrument.
   - *Itération 2 :* Durcissement des Gates IB Trend Day et HTF.
   - *Itération 3 :* Amélioration de l'EA MT5 (Trailing Stop & Split Target).
   - *Itération 4 :* Lancement du Strategy Analyzer NT8 en mode Walk-Forward.
4. **Validez toujours avec `dotnet run --project Tests/VolumeProfileTests.csproj`** pour garantir que tous les contrats Zero-Trust restent à 100% verts.
