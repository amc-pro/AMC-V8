# Guide de Déploiement NinjaTrader 8, Market Replay & Procédure de Rollback — Système Swing

Ce document détaille la procédure de déploiement en production, les étapes de vérification en Market Replay sous NinjaTrader 8 et la procédure de retour arrière (rollback).

---

## 1. Déploiement dans NinjaTrader 8

### 1.1. Synchronisation des Fichiers Source
Copiez l'ensemble des fichiers C# du dossier racine du dépôt vers le répertoire NinjaScript de NinjaTrader 8 :
* **Répertoire cible NT8 :** `Documents\NinjaTrader 8\bin\Custom\Indicators\`
* **Fichiers à copier :**
  - `AuctionMarketCore.cs`
  - `AuctionMarketCore.Swing.cs`
  - `AuctionMarketCore.Swing.Models.cs`
  - `AuctionMarketCore.ScalpingPro.cs`
  - `AuctionMarketCore.Sniper.cs`
  - `AuctionMarketCore.Engine.cs`
  - `AuctionMarketCore.Features.cs`
  - `AuctionMarketCore.VolumeProfile.cs`
  - `AuctionMarketCore.MarketIntelligence.cs`
  - `AuctionMarketCore.Render.cs`
  - `AuctionMarketCore.Network.cs`
  - `AuctionMarketCore.Exports.cs`
  - Dossier `VolumeProfile/` et `MarketIntelligence/`

### 1.2. Compilation NinjaScript
1. Ouvrez **NinjaTrader 8**.
2. Appuyez sur `F5` ou ouvrez l'éditeur de code (**Tools -> New -> NinjaScript Editor**).
3. Cliquez sur **Compile** (ou `F5`).
4. Vérifiez que la barre d'état affiche **"NinjaScript files generated successfully"** sans avertissement ni erreur.

### 1.3. Installation des Templates XML
Copiez les fichiers de configuration XML depuis `configs/SWING/` vers le dossier de templates de NinjaTrader 8 :
* **Répertoire cible templates NT8 :** `Documents\NinjaTrader 8\templates\Indicator\AuctionMarketCore\`
* **Fichiers XML :**
  - `CONFIG_ES_SWING.xml`, `CONFIG_MES_SWING.xml`
  - `CONFIG_NQ_SWING.xml`, `CONFIG_MNQ_SWING.xml`
  - `CONFIG_GC_SWING.xml`, `CONFIG_MGC_SWING.xml`
  - `CONFIG_CL_SWING.xml`, `CONFIG_MCL_SWING.xml`

---

## 2. Configuration d'un Graphique Swing sous NinjaTrader 8

1. **Création du Chart :**
   - Ouvrez un graphique sur l'instrument souhaité (ex. `ES 09-26` ou `NQ 09-26`).
   - Périodicité principale : **15 Minute** (ou Volumetric 15 Minute).
   - Données à charger : **60 à 90 Jours** (pour permettre l'agrégation des profils clôturés SQLite).
2. **Application du Template :**
   - Clic droit sur le graphique -> **Indicators** (`Ctrl+I`).
   - Sélectionnez `AuctionMarketCore`.
   - Cliquez sur **Template -> Load** et sélectionnez `CONFIG_ES_SWING` (ou selon l'actif).
   - Vérifiez que `TradingPreset` est positionné sur `Swing`.

---

## 3. Protocole de Validation en Market Replay

Exécutez au minimum **3 sessions de test** en Market Replay pour observer les comportements :

| Session Type | Date / Session Replay Conseillée | Comportement Swing Attendu |
| :--- | :--- | :--- |
| **Trend Day (Tendance Forte)** | Exemple : Session FOMC / CPI ou cassure macro | Déclenchement de setups `HtfContinuation` et `BreakoutRetest`. Respect de la tendance HTF 4H. |
| **Balance Day (Range / Équilibre)** | Session de consolidation après expansion | Déclenchement de `RejectExtreme` sur SD ±2/±3 et `ValueReentry` vers le POC. |
| **News & Gap Day** | Session avec gap d'ouverture > 1% | Pénalisation du score, blocage pendant la fenêtre de news majeure. |

### Vérification du Journal Shadow :
Ouvrez le fichier `Documents\NinjaTrader 8\shadow\swing_trades.csv` et vérifiez :
* Horodatages d'entrée et de sortie cohérents.
* Respect des stops en ticks et du R/R minimal ($\ge 1.5R$).
* Passage à Break-Even (+ 1 tick) après franchissement de TP1.

---

## 4. Procédure de Rollback (Retour Arrière Immédiat)

Si une anomalie survient en simulation :
1. **Désactivation instantanée :** Changez simplement le paramètre `TradingPreset` de `Swing` à `ScalpingPro` sur vos graphiques ou rechargez le template `CONFIG_ES_SCALPING_PRO`.
2. **Restauration du code source :**
   ```bash
   git checkout master
   # ou restauration de la branche précédente
   git checkout origin/feat/auction-market-core
   ```
3. Recompilez dans NinjaTrader 8 (`F5`).
