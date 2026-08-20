# Auction Market Core Pro (AMC-V8)

**AMC-V8** est un système de trading algorithmique institutionnel haute performance conçu pour **NinjaTrader 8** [1]. Il combine l'analyse des profils de volume (*Volume Profile*), l'ordre de flux (*Footprint / Delta Analysis*), la structure de marché (*Market Structure*) et une gouvernance stricte des risques.

---

## 🚀 Dernières Mises à Jour & Optimisations (Août 2026)

* **Optimisation de la Sélectivité (Scalping Pro)** : Le seuil minimal d'alerte a été ajusté à **45/100** pour garantir un flux régulier de 5 à 8 setups par session tout en maintenant un *Profit Factor* élevé [2].
* **Mode Souple HTF (`HtfSoftMode = true`)** : Les désalignements sur les unités de temps supérieures se traduisent désormais par une simple pénalité de score et non par un blocage éliminatoire [2].
* **Réhabilitation de `FINISHED_AUCTION`** : Réintégration de ce setup précoce dans les dérogations de la porte de localisation N2 [3].
* **Gestion Avancée du Risque** : Élargissement du Stop Loss à **1.75 ATR** (avec un buffer de 6 ticks) pour immuniser les positions contre le bruit de marché intra-barre [2].
* **Synchronisation Automatique des News** : Ajout d'un module de récupération en temps réel du calendrier économique (`sync_news.py`) connectable à l'API *Fair Economy* pour automatiser le blocage des trades (`NEWS_BLACKOUT`) lors des annonces majeures (CPI, FOMC, etc.) [4].
* **Outils de Test et Données Historiques** : Intégration d'un dossier `tests_and_data/` contenant des archives de données 1-minute (2022-2025) et des scripts de simulation [5].

---

## 📂 Structure du Dépôt GitHub

```text
AMC-V8/
├── SniperMarketCorePro.cs              # Moteur principal de l'indicateur
├── SniperMarketCorePro.Sniper.cs       # Logique du module Sniper & Journaling (Shadow)
├── SniperMarketCorePro.ScalpingPro.cs  # Implémentation du preset Scalping Pro & Seuils
├── MarketIntelligence/                 # Moteur de rapports de marché et contextes
├── configs/                            # Fichiers de configuration XML par instrument (CL, GC, NQ, ES)
│   └── SCALPING_PRO/                   # Presets et réglages spécifiques Scalping Pro
├── tests_and_data/                     # Outils de test, scripts de news et données historiques
│   ├── long_term_data/                 # Archives ZIP (NQ 1min 2022-2025, Données récentes 5min)
│   ├── sync_news.py                    # Script de synchronisation des annonces économiques
│   └── simulate_blocking.py            # Testeur de la logique de blackout news
├── GUIDE_MARKET_REPLAY.md              # Guide complet pour les tests en Market Replay
└── README.md                           # Documentation générale du projet
```

---

## 🛠️ Installation et Configuration

1. **Compilation NinjaTrader** : Copiez les fichiers source dans votre répertoire personnalisé NinjaTrader 8 (`Documents\NinjaTrader 8\bin\Custom\Indicators\`) et compilez le projet.
2. **Chargement du Preset** : Appliquez l'indicateur `SniperMarketCorePro` sur votre graphique et chargez le preset **Scalping Pro** [2].
3. **Synchronisation Quotidienne des News** : Avant chaque session de trading, exécutez le script pour actualiser les filtres de volatilité :
   ```bash
   python3 tests_and_data/sync_news.py
   ```

---

## 📊 Backtest et Market Replay

Pour valider les performances du système :
* Référez-vous au guide complet **[GUIDE_MARKET_REPLAY.md](./GUIDE_MARKET_REPLAY.md)** pour configurer correctement le **Tick Replay** et importer les données historiques incluses dans le dossier `tests_and_data/long_term_data/` [6].
* Consultez les journaux d'audit (mode Shadow) situés dans `Documents\NinjaTrader 8\bin\Custom\sniper/` pour analyser chaque signal généré (`LONG` / `SHORT`, scores et R-multiples) [7].

---

## Références

[1] Documentation technique du projet AMC-V8, *Architecture institutionnelle*, Août 2026.  
[2] Fichier `SniperMarketCorePro.ScalpingPro.cs`, Paramètres de seuil et de risque.  
[3] Fichier `SniperMarketCorePro.Sniper.cs`, Ligne 2322.  
[4] Script utilitaire `tests_and_data/sync_news.py` (API Fair Economy).  
[5] Dépôt GitHub `amc-pro/AMC-V8`, Dossier `/tests_and_data/`.  
[6] Guide opérationnel `GUIDE_MARKET_REPLAY.md`.  
[7] Système de journalisation Shadow, AMC-V8, Lignes 3110-3185.
