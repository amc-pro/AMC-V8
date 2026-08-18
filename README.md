# 🎯 AMC PRO — Sniper Market Core Pro (V7.8)

**AMC PRO** est un système algorithmique de trading haute confluence combinant la puissance d'analyse Order Flow & SMC de **NinjaTrader 8** avec l'exécution automatique multi-actifs de **MetaTrader 5**.

---

## 📌 Présentation Générale

Le projet est composé de deux briques principales connectées par un pont fichier JSON ultra-rapide (< 50ms) :

1. **NinjaTrader 8 ("Le Cerveau" - C#)** :
   - Analyse structurelle Smart Money Concepts (**SMC**) : Break of Structure (**BOS**), Change of Character (**CHOCH**), Order Blocks (**OB**), Liquidity Sweeps, Fair Value Gaps (**FVG**).
   - Analyse du Volume Profile & Flux d'Ordres CME : Footprint, Absorption, Imbalances empilées, Ticks Delta synthétiques, Delta cumulatif.
   - Système de **Scoring de Confluence Multi-facteurs** (N1 Contexte, N2 Localisation, N3 Microstructure, N4 Trigger).
   - **Protection Blackout News (Hard Gate & Pénalités)** : Blocage automatique et strict des signaux lors des annonces économiques majeures.
   - Génération d'alertes, export JSON atomique et notifications Telegram / Webhooks.

2. **MetaTrader 5 Receiver EA ("Les Bras" - MQL5)** :
   - Robot récepteur d'ordres écoutant le fichier JSON partagé (`amc_trade_signal.json`).
   - Validation du score et des grades (A+, A, B, C / Moyen, Fort, Très Fort).
   - Mapping dynamique des contrats Futures CME (GC, NQ, ES, CL) vers les symboles CFD/Forex du courtier MT5 (`XAUUSD`, `USTECH`, `US500`, `WTI`).
   - Money Management automatique (calcul du lot selon le % de risque sur le capital ou lot fixe).
   - Prise d'ordre, gestion du Trailing Stop, filtrage du spread et inversion automatique de position.

---

## 🏗️ Architecture du Système

```
┌─────────────────────────────────────────┐
│     NinjaTrader 8 (AMC PRO - C#)        │
│  - Analyse SMC (BOS, CHOCH, OB, FVG)    │
│  - Order Flow & Delta (Volume/Footprint)│
│  - Scoring de Confluence Multi-facteurs │
│  - Filtre & Hard Gate Blackout News     │
└────────────────────┬────────────────────┘
                     │ Export Fichier Atomique (JSON)
                     ▼
┌─────────────────────────────────────────┐
│   Pont Local MT5 (Common/Files)         │
│   "amc_trade_signal.json"               │
└────────────────────┬────────────────────┘
                     │ Polling 100ms (OnTimer)
                     ▼
┌─────────────────────────────────────────┐
│   MetaTrader 5 EA (AMCPro Receiver)     │
│  - Validation du Score & Grade          │
│  - Mapping Symboles (GC->XAUUSD, etc.)  │
│  - Money Management & Prise d'Ordres    │
└─────────────────────────────────────────┘
```

---

## 📁 Structure des Fichiers du Projet

```
SniperMarketCorePro_V7.8/
├── SniperMarketCorePro.cs              # Déclarations, paramètres globaux, OnBarUpdate & alertes
├── SniperMarketCorePro.Engine.cs       # Moteur de risque, calcul des targets, portes et quotas
├── SniperMarketCorePro.ScalpingPro.cs  # Preset ScalpingPro, scoring pondéré, validateur Footprint & SMC
├── SniperMarketCorePro.Exports.cs     # Exportation publique des signaux et pont fichier JSON MT5
├── SniperMarketCorePro.Network.cs     # Transport des notifications Telegram et Webhooks HTTP
├── SniperMarketCorePro.Render.cs      # Dashboard graphique SharpDX / Direct2D à 60 FPS
├── SniperMarketCorePro.Features.cs    # Gestion des presets et fonctionnalités avancées
├── SniperMarketCorePro.Sniper.cs      # Moteur Sniper, gates de sécurité, gestion news et journal
├── MarketIntelligence/                 # Module d'analyse de marché & Telegram Dispatcher
│   ├── MarketSnapshot.cs              # Instantané d'état du marché
│   ├── MarketStructureAnalyzer.cs     # Analyseur de structure SMC
│   ├── TelegramDispatcher.cs          # Dispatcher Telegram thread-safe avec déduplication
│   └── ...
├── mt5_receiver/
│   └── AMCPro_MT5_Receiver.mq5        # EA Récepteur MT5 avec Dashboard visuel
├── configs/                            # Presets de configurations XML par instrument
│   ├── SCALPING_PRO/                  # Configurations dédiées au preset Scalping Pro (NQ, MNQ, ES, etc.)
│   ├── STANDARD/                      # Configurations standard
│   └── ...
└── README.md                           # Documentation du projet
```

---

## ⚙️ Les Presets d'Analyse AMC PRO

| Preset | Seuil Score | Footprint | Marché Cible | Description |
| :--- | :--- | :--- | :--- | :--- |
| **`Standard`** | Relâché | Optionnel | Tous | Profil d'analyse globale et de recherche. |
| **`Sniper`** | Renforcé (72/100) | Strict | Futures CME | Très sélectif (2-4 setups/session), haute précision. |
| **`Scanner`** | Modéré (55/100) | Optionnel | Futures CME | Observation et recherche d'opportunités (large flux). |
| **`ScalpingPro`** | 35/100 | Obligatoire | Futures CME | Trading réel haute confluence (5-10 setups/session). |
| **`ScalpingPro`** | 35/100 minimum* | SMC + Footprint + Volume Profile | Futures CME / flux volumétrique | Mode scalping unifié et déterministe. |

---

## ⚡ Concept & Fonctionnement Détaillé du Preset Scalping Pro

Le preset **Scalping Pro** est le profil d'exécution réelle d'AMC PRO. Il est spécialement conçu pour éliminer le "bruit" des marchés en ciblant **5 à 10 setups de haute probabilité par session**.

### 1. Philosophie & Objectif Métier
- **Profil Scanner** : Très permissif (~15-30 alertes/jour), risquant de générer des faux signaux en marché d'hésitation.
- **Profil Sniper** : Ultra-sélectif (~2 alertes/session), risquant d'omettre des opportunités valides.
- **Profil Scalping Pro** : Équilibre optimal entre **fréquence, sélectivité et vitesse d'exécution** avec un ratio Risk/Reward minimum de 1.0 à 2.0.

### 2. Le Pipeline de Validation en 9 Étapes
Chaque bougie est évaluée à travers un entonnoir de décision séquentiel :
```
Contexte (N1) ➡️ Market Structure (SMC) ➡️ Liquidity (Sweeps) ➡️ Order Block 
➡️ Footprint (Order Flow) ➡️ Volume ➡️ Momentum (Delta) ➡️ Risk (ATR/SL/TP) ➡️ Alerte (Tier)
```

### 3. Modèle de Scoring Pondéré (Sur 100 Points)

#### A. Variante `ScalpingPro` (Futures CME avec Footprint réel) :
- 🏗️ **Structure SMC (30%)** : BOS, CHOCH, Order Blocks, Liquidity Sweeps, Fair Value Gaps (**FVG**), **FVG Inversion Breakers**, Mitigation.
- 👣 **Footprint / Order Flow (30%)** : Imbalances empilées, Absorptions passives, Delta cohérent, **Finished Auction** (épuisement zéro-contrat), **Unfinished Business** (aimants de liquidité Poor High/Low) (**Footprint obligatoire**).
- 📊 **Volume Profile (15%)** : Rang relatif de volume de la barre par rapport au profil global.
- 🚀 **Momentum & Delta (15%)** : Z-Delta, vitesse du flux d'ordres, déclencheur N4.
- 🌐 **Contexte Global & Initial Balance (10%)** : Alignement N1, biais Market Intelligence (M5/M15), **Régime Initial Balance (IB)** (Trend Day vs Range Day).

#### B. ScalpingPro unifié
- 🏗️ **Structure SMC (35%)** (BOS, CHOCH, OB, FVG, Inversion Breakers)
- 🚀 **Momentum & Delta (20%)**
- 📊 **Volume Profile (20%)**
- 🌐 **Contexte Global & Initial Balance (15%)**
- 👣 **Footprint Synthétique (10%)** (*Footprint optionnel*)

### 4. Niveaux d'Alertes & Grades (Tiers)
Chaque signal qualifié est classé selon son niveau de confluence :
- ⚪ **AUCUN** (< 35 pts) : Signal rejeté.
- 🔵 **MOYEN** (35 à 45 pts) : Setup valide, confluence minimale.
- 🟡 **FORT / SILVER** (46 à 65 pts) : Setup solide, alignement multi-facteurs.
- 🔴 **TRÈS FORT / GOLD** (66+ pts) : Setup institutionnel premium (alignement SMC + Order Flow optimal).

---

## 📰 Filtre & Protection Blackout News (Économique)

Le système intègre un module de filtrage des horaires d'annonces économiques pour éviter les pièges de volatilité artificielle et d'écartement de spread :

| Paramètre | Description | Valeur par Défaut |
| :--- | :--- | :--- |
| **`NewsHardBlock`** | Mode Hard Gate : bloque totalement (`GateFailed = NEWS_BLACKOUT`) tout signal pendant la fenêtre. | `true` |
| **`NewsBlackoutMinutes`** | Fenêtre de blackout en minutes avant et après l'annonce. | `5` à `10` min |
| **`NewsTimesCsv`** | Horaires des annonces clés au format `HHMM` (fuseau du graphique). | `0830,1000,1400,1430` |
| **`NewsWindowPenalty`** | Pénalité de score appliquée si le mode Hard Gate est désactivé. | `5` pts (Sniper) / `1` pt |
| **`NewsWeekdaysOnly`** | Active le filtre uniquement du lundi au vendredi. | `true` |

---

## 🛡️ Fonctionnalités de Sécurité & Gates Éliminatoires

Chaque candidat doit franchir des **Gates de sécurité** strictes avant d'être transmis :

- 🔒 **`NEWS_BLACKOUT`** : Signal survenu pendant une fenêtre d'annonce économique majeure.
- 🔒 **`N1_CONTEXTE` / `N2_LOCALISATION` / `N3_MICROSTRUCTURE` / `N4_TRIGGER`** : Score insuffisant sur l'un des 4 piliers d'analyse.
- 🔒 **`RR`** : Ratio Risque/Rendement inférieur au seuil minimum exigé (`MinRiskReward`).
- 🔒 **`REGIME_RTH`** : Signal émis hors session autorisée lorsque `SniperRthOnly = true`.
- 🔒 **`HTF`** : Désalignement avec la tendance supérieure en mode strict (`HtfStrictMode = true`).
- 🔒 **`FOOTPRINT_ABSENT`** : Rejet automatique des setups Reversal sans confirmation de flux d'ordres réel.
- **Contrôle d'Âge du Signal (`InpMaxSignalAgeSec`)** : Rejet des signaux de plus de 120s côté MT5.
- **Filtrage du Spread Maximum (`InpMaxSpreadPoints`)** : Suspension des ordres si le spread s'élargit.
- 🛡️ **Sorties Échelonnées & Break-Even (`TP_TARGET_SPLIT`)** : Clôture de 50% du lot à TP1 + déplacement automatique du SL à Break-Even (+ buffer de sécurité).
- 🚨 **Daily Max Loss Hard Lockout (`InpEnableDailyMaxLoss`)** : Blocage total des nouveaux signaux et clôture d'urgence si la perte du jour atteint le seuil toléré (ex: -2.5%).
- ⏸️ **Circuit Breaker Anti-Tilt (`InpEnableCircuitBreaker`)** : Mise en pause automatique après $N$ pertes consécutives (ex: 3 pertes = 90 min de pause).

---

## 🚀 Guide d'Installation & Configuration

### 1. Configuration Côté NinjaTrader 8
1. Copier tous les fichiers `.cs` et le dossier `MarketIntelligence/` dans le répertoire NinjaTrader 8 :  
   `Documents\NinjaTrader 8\bin\Custom\Indicators\`
2. Recompiler la solution dans NinjaTrader 8 (touche **F5** dans le NinjaScript Editor).
3. Attacher l'indicateur `SniperMarketCorePro` sur votre graphique volumétrique (ex: 5 min Footprint / Volumetric).
4. Dans la section **14. Pont MT5 Auto-Trading** des paramètres :
   - `Activer Pont MT5 (Socket TCP <1ms)` : `true` (Diffusion instantanée en mémoire).
   - `Port TCP Serveur Localhost` : `18888`.
   - `Activer Pont MT5 (Fichier JSON)` : `true` (Canal de secours / fallback automatique).
   - `Nom du Fichier Signal` : `amc_trade_signal.json`.

### 2. Configuration Côté MetaTrader 5
1. Ouvrir `mt5_receiver/AMCPro_MT5_Receiver.mq5` dans MetaEditor (F4) et appuyer sur **F7** pour le compiler.
2. Glisser l'EA `AMCPro_MT5_Receiver` sur votre graphique MT5.
3. Dans les paramètres d'entrée de l'EA :
   - **Mode Pont NT8** : `BRIDGE_AUTO` (Tente la connexion Socket TCP <1ms en priorité, bascule sur le Fichier si NT8 n'est pas prêt).
   - `InpTcpHost` : `127.0.0.1` | `InpTcpPort` : `18888`.
   - Choisir le mode de TP : `TP_TARGET_SPLIT` (Recommandé Prop Firm), `TP_TARGET_1` ou `TP_TARGET_2`.
   - Vérifier le mapping selon votre courtier (`InpSymbol_GC` = `XAUUSD`, `InpSymbol_NQ` = `USTECH`, etc.).
   - Configurer le risque par trade (`InpRiskPercent` ou `InpFixedLot`) et le Daily Max Loss (`InpDailyMaxLossPercent`).
   - Activer l'**AutoTrading** (bouton vert) dans la barre d'outils MT5.

---

## 🔄 Mapping des Symboles (Futures CME ➡️ CFD MT5)

| Symbole NT8 (Futures) | Symbole MT5 Broker (CFD / Forex) | Instrument |
| :--- | :--- | :--- |
| `GC` / `MGC` | `XAUUSD` / `GOLD` | Or (Gold) |
| `NQ` / `MNQ` | `USTECH` / `NAS100` | Nasdaq 100 |
| `ES` / `MES` | `US500` / `SPX500` | S&P 500 |
| `CL` / `MCL` | `WTI` / `USOIL` | Pétrole (Crude Oil) |
| `6E` | `EURUSD` | Euro / US Dollar |
| `6B` | `GBPUSD` | Livre Sterling / US Dollar |
| `FDAX` | `GER40` / `DAX` | Allemagne 40 (DAX) |

---

## 📄 Licence & Copyright

Copyright © 2026 AMC Pro Auto-Trading. Tous droits réservés.
