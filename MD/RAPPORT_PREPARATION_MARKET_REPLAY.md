# Rapport de Préparation Market Replay & Audit Base de Données (AMC-V8)

**Date d'audit :** Septembre 2026  
**Environnement :** NinjaTrader 8 / SQLite / AMC-V8 (`AuctionMarketCore`)  
**Statut global :** ✅ **Moteur et base validés (99 tests unitaires réussis)**

---

## 1. 🔍 État des Lieux de la Base de Données (`amc_volume_profile.db`)

La persistance SQLite est gérée par [`VolumeProfileRepository.cs`](file:///c:/AMC-Pro/AMC-V8/VolumeProfile/VolumeProfileRepository.cs) à l'emplacement standard :  
`C:\Users\<USER>\Documents\NinjaTrader 8\db\amc_volume_profile.db`

### 1.1 Structure des Tables
La base intègre les 4 tables relationnelles avec index optimisés :
* **`vp_profiles`** : Profils clôturés (`DAILY`, `WEEKLY`, `MONTHLY`), Value Area (POC, VAH 70%, VAL 70%), VWAP officiel et bandes statistiques d'écart-type ($SD \pm 1, \pm 2, \pm 3$).
* **`vp_nodes`** : Nœuds de volume lissés par filtre Gaussien (**HVN** = acceptation/aimant, **LVN** = rejet/accélération).
* **`vp_zone_state`** : Cycle de vie et force des zones d'enchères.
* **`swing_trades`** : Journal d'audit et suivi des positions Swing (entrées, stops initiaux/actuels, TP1, TP2, R-multiple réalisé).

---

### 1.2 Cartographie des Données Actuelles par Instrument

| Instrument | Profils Daily | Profils Weekly | Profils Monthly | VWAP & Bandes $SD \pm 1, \pm 2, \pm 3$ | Disponibilité Immédiate en Replay |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **MNQ** | 8 | 1 (W34) | 1 (Juillet 2026) | ✅ **100% Calibré & Actif** | Immédiate |
| **NQ** | 109 | 21 | 4 (dont Juillet 2026) | ✅ **100% Calibré & Actif** | Immédiate |
| **ES / MES** | 3 | 0 | 0 | ⚠️ En cours de constitution | Via pré-chargement chart (30-60 jours) |
| **GC / MGC** | 121 | 25 | 5 | ⚠️ Profils VA présents, VWAP à actualiser | Via pré-chargement chart (30-60 jours) |
| **CL / MCL** | 125 | 25 | 5 | ⚠️ Profils VA présents, VWAP à actualiser | Via pré-chargement chart (30-60 jours) |

#### 💡 Comportement Auto-Apprenant en Replay :
Le moteur [`VolumeProfileManager.cs`](file:///c:/AMC-Pro/AMC-V8/VolumeProfile/VolumeProfileManager.cs) est conçu pour construire et enregistrer automatiquement les profils clôturés dès qu'une session (Jour, Semaine, Mois) se termine pendant le Replay.  
*Pour les instruments ES, GC et CL, il suffit de configurer **`Days to load = 30` à `60` jours** dans NinjaTrader pour que l'historique construise immédiatement les niveaux de référence.*

---

## 2. ❓ Question Clé : Le Graphique Volumétrique est-il obligatoire pour le Swing ?

> **RÉPONSE DIRECTE : NON, ce n'est PAS obligatoire d'avoir un graphique visuel en barres "Volumetric" pour trader ou tester le mode Swing.**

### 2.1 Fonctionnement Architectural Interne
Dans le code source [`AuctionMarketCore.cs`](file:///c:/AMC-Pro/AMC-V8/AuctionMarketCore.cs) (lignes 1601 à 1622) :
* Si votre graphique principal est configuré en **bougies japonaises classiques (Candlesticks)** en **15-Min** ou **60-Min**, le moteur détecte que la série principale (Index 0) n'est pas de type volumétrique.
* Le moteur appelle automatiquement en arrière-plan :
  ```csharp
  AddVolumetric(Instrument.FullName, BarsPeriodType.Minute, VolumetricTimeframe, VolumetricDeltaType.BidAsk, 1);
  ```
* **Résultat visuel & technique** :
  1. Votre écran reste **propre, fluide et lisible** avec des bougies classiques en 15m / 60m.
  2. Le moteur instancie une sous-série volumétrique invisible (Index 1) pour calculer les deltas, les volumes par niveau de prix et les profils de volume.

### 2.2 La Seule Obligation Technique : Le `Tick Replay`
Que votre graphique visuel soit en bougies classiques ou volumétriques, vous **DEVEZ IMPÉRATIVEMENT COCHER `Tick Replay`** dans la configuration de la série de données NinjaTrader (*Data Series > Tick Replay*).  
*Raison :* Sans Tick Replay, NinjaTrader n'alimente pas les données intra-barre nécessaires aux calculs de delta et de distribution par niveau de prix de la sous-série.

---

## 3. ⚖️ Matrice Comparative de Configuration Replay

| Paramètre | Scalping Pro (Intraday) | Swing (Macro Auction Market) |
| :--- | :--- | :--- |
| **Type de Graphique Visuel** | Volumetric (Order Flow / Footprint) ou 1m standard | **Candlesticks Standard (Bougies classiques 15m / 60m)** |
| **Timeframe Principal** | 1-min, 2-min ou 5-min | **15-min ou 60-min** |
| **Séries HTF de Contexte** | 15-min / 60-min (EMA 50) | **240-min (4 Heures) / Daily (1440-min)** |
| **Option Tick Replay** | ✅ **Obligatoire** | ✅ **Obligatoire** (pour la sous-série interne) |
| **Fichier de Configuration** | `configs/SCALPING_PRO/CONFIG_<SYM>_SCALPING_PRO.xml` | `configs/SWING/CONFIG_<SYM>_SWING.xml` |
| **Seuil d'Alerte (Score)** | `MinScoreToAlert = 50.0` (Paliers 45 / 50 / 65) | `SwingMinScoreToAlert = 50.0` (Silver 50, Gold 65, Très Fort 80) |
| **Stop Loss Multiplicateur** | $1.75 \times \text{ATR}$ (dynamique micro-bruit) | **$2.0 \text{ à } 2.25 \times \text{ATR}$ (respiration macro)** |
| **Bornes Stops Clamping** | ES : 12-160 ticks \| NQ : 12-160 ticks | ES : 16-80 ticks (4-20 pts) \| NQ : 40-240 ticks (10-60 pts) |
| **Objectifs Visés** | TP1: $1.0\text{R}$, TP2: $2.0\text{R}$ | **TP1: $1.5\text{R}$, TP2: $3.0\text{R}$** |
| **Maintien Overnight** | ❌ Clôture obligatoire fin de RTH | ✅ **Autorisé (`SwingAllowOvernightHold = true`)** |
| **Journal d'Audit** | `shadow/trades.csv` | `shadow/swing_trades.csv` + SQLite `swing_trades` |

---

## 4. 🚀 Protocole Opérationnel Pas-à-Pas

### Étape 1 : Compilation NinjaScript
Les fichiers sources ont été synchronisés dans le répertoire NinjaTrader.  
Dans NinjaTrader 8 : Ouvrez l'éditeur NinjaScript (**Tools > New NinjaScript Editor**) et appuyez sur **F5** pour compiler.

### Étape 2 : Configuration du Graphique (Chart)
1. Ouvrez un graphique sur l'instrument souhaité (ex: `NQ 09-26` ou `ES 09-26`).
2. Faites un clic droit > **Data Series** :
   * **Timeframe** : 1-Min / 5-Min (pour Scalping Pro) OU 15-Min / 60-Min (pour Swing).
   * **Days to load** : `30` à `60` jours.
   * **Tick Replay** : ☑ **Cocher la case**.
3. Ajoutez l'indicateur **`AuctionMarketCore`**.
4. Clic droit sur l'indicateur > **Presets > Load** :
   * Choisissez le fichier XML correspondant dans `configs/SCALPING_PRO/` ou `configs/SWING/`.

### Étape 3 : Lancement du Market Replay
1. Allez dans **Connections > Playback Connection**.
2. Dans la fenêtre de contrôle Replay, sélectionnez la période de test.
3. Lancez la lecture (Play) à vitesse contrôlée ($5\times$ à $50\times$).

### Étape 4 : Analyse et Audit des Résultats
Après votre session de Replay, exécutez les scripts Python d'analyse automatique :
```bash
# Analyse détaillée des signaux et du R-multiple
python Python/analyze_latest_shadow.py

# Audit de rentabilité et filtrage des faux départs
python Python/analyze_profitability.py

# Vérification de l'état de la base SQLite
python Python/check_db_status.py
```

---

## 5. 🛡️ Résumé des Règles d'Or

1. **Isolation stricte** : Ne combinez pas les presets ScalpingPro et Swing sur le même onglet de graphique. Créez un onglet dédié par mode opératoire.
2. **Priorité aux confluences macro** : En Swing, les opportunités de classe A+ proviennent principalement du rejet des bandes $SD \pm 2 / \pm 3$ du VWAP Mensuel clôturé et de la réintégration de Value Area (`ValueReentry`).
3. **Protection du capital** : Le filtre anti-doublon et anti-empilement (`openTrades`) garantit qu'aucune seconde position dans le même sens n'est prise tant que la première est active.
