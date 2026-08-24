# Guide Opérationnel : Backtest et Market Replay avec AMC-V8 & Données Historiques

Ce guide présente la méthodologie standard pour configurer, importer et exploiter les données historiques et les modules de l'indicateur **SniperMarketCorePro (AMC-V8)** sous NinjaTrader 8 [1]. Il s'adresse aux traders et aux analystes quantitatifs désireux de valider les performances du preset **Scalping Pro** en conditions de marché réjouées (*Market Replay*) [2].

---

## 1. Prérequis et Architecture des Fichiers

Avant d'initialiser vos sessions de test, assurez-vous d'avoir récupéré les ressources nécessaires depuis votre dépôt distant GitHub (`amc-pro/AMC-V8`), en particulier le dossier `tests_and_data/long_term_data/` [3].

### Tableau récapitulatif des ressources de test

| Ressource | Emplacement dans le Dépôt | Rôle Principal |
| :--- | :--- | :--- |
| **Archives Historiques** | `tests_and_data/long_term_data/` | Fournit les fichiers OHLCV (ex: `NQ_1min_2022_2025.zip`) pour l'alimentation de NinjaTrader [3]. |
| **Outil de Synchronisation** | `tests_and_data/sync_news.py` | Met à jour dynamiquement les fichiers de configuration XML avec les annonces du jour [4]. |
| **Configurations XML** | `configs/SCALPING_PRO/` | Contient les seuils d'alerte, les filtres de score et les paramètres de risque (`StopAtrMultiple = 1.75`) [5]. |

---

## 2. Étape 1 : Importation des Données Historiques dans NinjaTrader 8

Pour que l'indicateur puisse calculer les profils de volume, les zones institutionnelles (VAH, VAL, POC) et les confluences, il est impératif d'importer l'historique de manière propre.

1. **Décompression des archives** : Extrayez le fichier `NQ_1min_2022_2025.zip` (ou tout autre fichier de la semaine) pour obtenir le fichier CSV brut au format standard [3].
2. **Ouverture de l'outil d'import** : Dans NinjaTrader 8, naviguez vers le menu supérieur : **Tools > Import > Historical Data** [6].
3. **Paramétrage de l'importation** :
   * Sélectionnez l'instrument cible (ex: `NQ 09-26` ou contrat continu correspondant).
   * Désignez le chemin du fichier CSV extrait.
   * Validez la structure des colonnes (`Timestamp, Open, High, Low, Close, Volume`).
4. **Validation** : Cliquez sur **Import**. Une fois l'opération terminée, l'historique est enregistré dans la base de données locale de NinjaTrader.

---

## 3. Étape 2 : Configuration du Graphique et du Market Replay

Le module **Scalping Pro** repose sur des analyses fines de carnet d'ordres et de structure (*Footprint / Delta*). Une configuration rigoureuse du graphique est donc requise [2].

* **Activation du Tick Replay** : Ouvrez un graphique sur l'instrument importé, faites un clic droit, puis sélectionnez **Data Series**. Dans les propriétés, **cochez impérativement l'option Tick Replay** [6]. Cette option permet à NinjaTrader de reconstituer les mouvements intra-barre indispensables aux calculs de l'indicateur.
* **Chargement de l'indicateur** : Ajoutez `SniperMarketCorePro` à votre graphique et chargez le preset **Scalping Pro** [1].
* **Lancement du Playback** : Allez dans **Connections > Playback Connection**. Dans le panneau de contrôle qui apparaît, sélectionnez la plage de dates correspondant à vos données importées (ex: une semaine de 2025 ou les données récentes) et cliquez sur le bouton de lecture (Play) [6].

---

## 4. Étape 3 : Gestion des Risques et Filtrage des News

Les récents ajustements apportés au projet garantissent un équilibre optimal entre sélectivité et réactivité :

* **Seuil d'alerte assoupli** : Le paramètre `MinScoreToAlert` est configuré à **45/100**, permettant de capturer les structures de retournement solides (comme `FINISHED_AUCTION`) sans attendre une perfection statistique excessive [5].
* **Protection contre le bruit (Stop Loss)** : Le multiple ATR est fixé à **1.75** avec un buffer de **6 ticks**, mettant vos positions à l'abri des fluctuations intra-barre intempestives [5].
* **Sécurité Économique** : Avant de lancer vos analyses sur des sessions récentes, exécutez le script de synchronisation pour appliquer le blocage des news :
  ```bash
  python3 tests_and_data/sync_news.py
  ```
  *Note : En Market Replay sur des dates anciennes, veillez à renseigner manuellement les horaires des annonces majeures (CPI, FOMC) dans le paramètre `NewsTimesCsv` si vous souhaitez auditer leur impact sur le blocage des trades [4].*

---

## 5. Étape 4 : Analyse des Résultats (Shadow Journal)

Pendant le déroulement du Market Replay, l'indicateur consigne chaque décision et chaque résultat dans des fichiers journaux au format CSV [7].

* **Emplacement des journaux** : `Documents/NinjaTrader 8/bin/Custom/sniper/` [7]
* **Fichiers générés** :
  * `_sniper.csv` : Enregistre tous les candidats détectés (y compris ceux bloqués par les filtres de score ou de news), indiquant explicitement le sens de la position (`LONG` / `SHORT`), le score brut et le motif de rejet éventuel [7].
  * `_outcomes.csv` : Suit l'évolution des trades validés et calcule le ratio R-multiple final pour chaque session [7].

> « L'analyse croisée des fichiers `_sniper.csv` et des graphiques en Replay constitue la méthode la plus rigoureuse pour affiner vos réglages avant le passage en production réelle. » — **AMC-V8 Methodology** [1]

---

## Références

[1] Documentation technique du projet AMC-V8, *Architecture et Moteur Sniper*, Août 2026.  
[2] Fichier source de référence, `SniperMarketCorePro.ScalpingPro.cs`.  
[3] Dépôt GitHub `amc-pro/AMC-V8`, Dossier `/tests_and_data/long_term_data/`.  
[4] Script de synchronisation, `tests_and_data/sync_news.py`.  
[5] Fichiers de configuration XML, Dossier `/configs/SCALPING_PRO/`.  
[6] Manuel utilisateur officiel NinjaTrader 8, *Historical Data Import & Market Replay Guide*.  
[7] Fichiers de journalisation du module, `SniperMarketCorePro.Sniper.cs`, Lignes 3110-3185.
