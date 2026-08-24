# Auction Market Core Pro (AMC-V8)

**AMC-V8** est un système de trading algorithmique institutionnel haute performance conçu pour **NinjaTrader 8** [1]. Il combine l'analyse des profils de volume (*Volume Profile*), l'ordre de flux (*Footprint / Delta Analysis*), la structure de marché (*Market Structure*) et une gouvernance stricte des risques.

---

## 🚀 Dernières Mises à Jour & Optimisations (Août 2026)

### 1. Déverrouillage & Spécialisation des Gates (Scalping Pro)
* **Seuil Minimal d'Alerte** : Calibré à **`50/100`** pour un flux équilibré de 5 à 10 opportunités de qualité par session (Paliers : *Moyen* $\ge 45$, *Fort* $\ge 50$, *Très Fort* $\ge 65$) [2].
* **Spécialisation des Portes par Famille de Setup** : Les setups de flux/momentum (`DELTA_FLIP`, `CUM_DELTA_DIV`, `BREAKOUT_VAH/VAL`) ne sont plus bloqués par l'absence d'absorption passive ($N3$) ou de mèche contre-tendance ($N4$) lorsqu'une impulsion directionnelle de delta est confirmée [3].
* **Levée Intelligente des Portes Secondaires** : Lorsqu'un setup atteint un score global fort ($\ge 50$), les sous-notes marginales non-critiques n'entraînent plus de rejet éliminatoire [2].

### 2. Architecture Avancée du Risque & Stop Loss Dynamique
* **Stop Loss Dynamique Réel (`1.75 ATR`)** : Suppression du bridage artificiel en pips (`MaxStopPips = 0`) au profit d'un dimensionnement adapté à la volatilité de chaque instrument (15 à 40 points sur NQ/MNQ, 2 à 8 points sur ES, etc.) protégé par les niveaux structurels et un buffer de 6 ticks [2].
* **Filtre Anti-Doublon & Anti-Empilement** : Interdiction d'ouvrir un nouveau trade dans le même sens tant qu'une position de même direction est active (`openTrades`), éliminant l'accumulation de pertes consécutives sur les faux départs [3].

### 3. Gestion Adaptative des News & Contexte
* **Mode Pénalité News** : `NewsHardBlock = false` avec pénalité adaptative de **`-15 points`** (`NewsWindowPenalty = 15`) pendant les fenêtres économiques, permettant aux opportunités de très haute conviction d'être exécutées [2].
* **Mode Souple HTF (`HtfSoftMode = true`)** : Les désalignements sur les unités de temps supérieures appliquent une pénalité modulatrice de score sans rejet bloquant [2].
* **Configurations Multi-Actifs Synchronisées** : Alignement complet des 8 fichiers XML de configuration (`MNQ`, `NQ`, `ES`, `MES`, `GC`, `MGC`, `CL`, `MCL`) dans `configs/SCALPING_PRO/`.

---

## 📂 Structure du Dépôt GitHub

```text
AMC-V8/
├── SniperMarketCorePro.cs              # Moteur principal de l'indicateur
├── SniperMarketCorePro.Sniper.cs       # Logique du module Sniper & Journaling (Shadow)
├── SniperMarketCorePro.ScalpingPro.cs  # Implémentation du preset Scalping Pro & Seuils
├── SniperMarketCorePro.Engine.cs       # Moteur de calcul des flux, deltas et profils
├── SniperMarketCorePro.Features.cs     # Extraction des features de microstructure
├── MarketIntelligence/                 # Moteur de rapports de marché et contextes
├── VolumeProfile/                      # Gestion et persistance des profils de volume
├── configs/                            # Fichiers de configuration XML par instrument
│   ├── SCALPING_PRO/                   # Presets et réglages spécifiques Scalping Pro
│   ├── SNIPER/                         # Presets et réglages spécifiques Sniper
│   ├── STANDARD/                       # Presets Standard
│   └── SCANNER/                        # Presets Scanner
├── Python/                             # Scripts d'audit, simulations et tests de signaux
├── historical-data/                    # Données de marché haute résolution (MNQ/NQ)
├── shadow/                             # Journaux d'audit et exécutions shadow
├── tests_and_data/                     # Outils de test et synchronisation des news
│   ├── sync_news.py                    # Script de synchronisation des annonces économiques
│   └── simulate_blocking.py            # Testeur de la logique de blackout news
└── README.md                           # Documentation générale du projet
```

---

## 🛠️ Installation et Configuration

1. **Compilation NinjaTrader** : Copiez les fichiers source dans votre répertoire personnalisé NinjaTrader 8 (`Documents\NinjaTrader 8\bin\Custom\Indicators\`) et compilez le projet via l'éditeur NinjaScript.
2. **Chargement du Preset** : Appliquez l'indicateur `SniperMarketCorePro` sur votre graphique et chargez le preset XML correspondant à votre instrument dans `configs/SCALPING_PRO/` [2].
3. **Synchronisation Quotidienne des News** : Avant chaque session de trading, exécutez le script pour actualiser le calendrier économique :
   ```bash
   python tests_and_data/sync_news.py
   ```

---

## 📊 Analyse des Performances & Audit Shadow

Pour analyser et valider les performances du système :
* Exécutez le script d'analyse sur vos journaux d'audit shadow récents :
  ```bash
  python Python/analyze_latest_shadow.py
  ```
* Testez l'impact du stop dynamique et du filtre anti-doublon :
  ```bash
  python Python/test_cooldown_impact.py
  ```
* Consultez les journaux d'audit situés dans `shadow/` pour analyser chaque opportunité détectée, son score pondéré, ses sous-notes ($N1$ à $N4$) et ses $R$-multiples [7].

---

## Références

[1] Documentation technique du projet AMC-V8, *Architecture institutionnelle*, Août 2026.  
[2] Fichier `SniperMarketCorePro.ScalpingPro.cs`, Paramètres de seuil, scoring pondéré et risque.  
[3] Fichier `SniperMarketCorePro.Sniper.cs`, Spécialisation des Gates, gestion du risque et filtres d'émission.  
[4] Script utilitaire `tests_and_data/sync_news.py` (API Fair Economy).  
[5] Dépôt GitHub `amc-pro/AMC-V8`, Dossier `/configs/SCALPING_PRO/`.  
[6] Fichiers de configuration XML institutionnels par instrument.  
[7] Système de journalisation Shadow, `shadow/AuctionMarketCorePro_journal_sniper.csv`.
