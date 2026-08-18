# 🎯 Guide Complet : Test Rapide & Backtest de Performance AMC PRO V8.0

Ce guide détaille la procédure étape par étape pour exécuter un **test de performance (backtest / shadow testing)** sur n'importe quel instrument et n'importe quelle période historique (ex: 3 mois : Mai, Juin, Juillet) afin d'obtenir immédiatement le **Taux de Réussite (Win Rate %)**, le **Profit Factor** et le **Gain Net en Multiple de Risque ($R$)**.

---

## 📌 Principe de Fonctionnement

Le système repose sur deux composantes interconnectées :
1. **NinjaTrader 8 (Moteur de Calcul C#)** : Fait défiler les barres historiques, applique les 4 niveaux de filtres/gates (`N1` Contexte, `N2` Localisation, `N3` Microstructure, `N4` Trigger, Blackout News), valide les trades, calcule les niveaux précis d'Entrée / Stop Loss / TP1 / TP2 et consigne chaque résultat dans des fichiers journaux CSV.
2. **Script d'Analyse Python ([analyze_journal.py](file:///c:/Users/andro/Downloads/volumeprofile/AMC_PRO_V8.0/analyze_journal.py))** : Lit instantanément les fichiers CSV et produit un rapport d'audit institutionnel complet (Win Rate, Profit Factor, métriques par mois, par grade, par setup).

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Graphique NinjaTrader 8                                  │
│    - Instrument (NQ, ES, GC, CL, MNQ...)                   │
│    - Données historiques (ex: 90 à 110 jours)               │
│    - Indicateur SniperMarketCorePro (Shadow Journal Actif)  │
└──────────────────────────────┬──────────────────────────────┘
                               │ Recalcul automatique (F5)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Fichiers Journaux Générés (Documents/NinjaTrader 8/)     │
│    - AuctionMarketCorePro_journal_sniper.csv (Candidats)    │
│    - AuctionMarketCorePro_journal_sniper_outcomes.csv (TP/SL)│
└──────────────────────────────┬──────────────────────────────┘
                               │ Exécution du script
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Script Python (analyze_journal.py)                       │
│    - Taux de Réussite Global (Win Rate %)                   │
│    - Profit Factor & Espérance Mathématique E[R]            │
│    - Rapport Mensuel (Mai, Juin, Juillet)                   │
│    - Performance par Grade (A+, A, B, C / FORT, MOYEN...)   │
│    - Performance par Setup & Filtres Anti-Bruit             │
└─────────────────────────────────────────────────────────────┘
```

---

## 🚀 Procédure Étape par Étape

### 📝 Étape 1 : Configurer la Série de Données dans NinjaTrader 8

1. Lancez **NinjaTrader 8**.
2. Ouvrez un graphique sur l'instrument que vous souhaitez analyser (ex: `NQ`, `MNQ`, `ES`, `GC` ou `CL`).
3. Faites un **clic droit** sur le graphique puis cliquez sur **`Data Series...`** (ou raccourci **`Ctrl + F`**).
4. Dans la fenêtre de configuration :
   - **Data Series Type** : Choisissez votre timeframe de travail (ex: `1 Minute`, `5 Minute` ou `Volumetric`).
   - **Load data based on** :
     - Option A : Choisissez **`Days`** et indiquez **`110`** jours (pour couvrir les 3 à 4 derniers mois).
     - Option B : Choisissez **`Custom range`** et entrez la date de début `01/05/2026` et date de fin `31/07/2026`.
5. Cliquez sur **OK**.

---

### ⚙️ Étape 2 : Configurer l'Indicateur SniperMarketCorePro

1. Sur le graphique, faites un **clic droit** ➔ **`Indicators...`** (ou raccourci **`Ctrl + I`**).
2. Ajoutez l'indicateur **`SniperMarketCorePro`**.
3. Dans le panneau de droite des propriétés :
   - **Groupe `00. Preset`** : Sélectionnez le profil à tester (ex: `ScalpingPro` ou `Sniper` ou `Standard`).
   - **Groupe `Sniper 07. Journal`** : Cochez **`Journal Sniper (shadow mode)`** (`EnableShadowJournal = true`).
   - *(Optionnel)* : Si vous souhaitez tester toutes les configurations sans blocage pour de la recherche pure, vous pouvez basculer **`Mode d'execution`** sur **`Research`**.
4. Cliquez sur **OK**.

---

### 🔄 Étape 3 : Lancer le Calcul Historique

1. Appuyez sur la touche **`F5`** (ou faites un clic droit sur le graphique ➔ **`Reload NinjaScript`**).
2. NinjaTrader va charger et analyser toutes les barres de la période sélectionnée.
3. Pendant ce chargement, le moteur enregistre les données dans :
   - `C:\Users\<VotreNom>\Documents\NinjaTrader 8\AuctionMarketCorePro_journal_sniper.csv`
   - `C:\Users\<VotreNom>\Documents\NinjaTrader 8\AuctionMarketCorePro_journal_sniper_outcomes.csv`

---

### 📊 Étape 4 : Lancer l'Analyse Automatique des Résultats

1. Ouvrez un terminal **PowerShell** ou **Invite de commandes** dans le dossier du projet :
   ```powershell
   cd c:\Users\andro\Downloads\volumeprofile\AMC_PRO_V8.0
   ```
2. Exécutez le script d'analyse :
   ```powershell
   python analyze_journal.py
   ```
3. *(Optionnel)* Si vous voulez analyser un fichier CSV spécifique :
   ```powershell
   python analyze_journal.py "C:\Users\andro\Documents\NinjaTrader 8\AuctionMarketCorePro_journal_sniper_outcomes.csv"
   ```

---

## 📈 Guide d'Interprétation des Résultats

Lorsque vous exécutez le script, voici comment lire les sections clés :

### 1. Indicateurs Globaux de Performance (KPIs)
* **Total Trades** : Nombre d'opportunités déclenchées sur la période.
* **Gagnants (TP1 + TP2)** : Nombre et pourcentage de trades ayant touché au minimum la Cible 1 ou la Cible 2.
* **Pertes (Stop Loss)** : Nombre et pourcentage de sorties au Stop Loss.
* **Gain Net Total (en R)** : Somme totale des multiples de risque engrangés. *(Ex: +220 R signifie un gain de 220 fois votre risque unitaire par trade).*
* **Profit Factor (PF)** : Rapport Gains Bruts / Pertes Brutes.
  * `PF < 1.0` : Stratégie déficitaire.
  * `1.0 < PF < 1.5` : Stratégie modérément profitable.
  * `PF > 1.5` : **Stratégie solide et robuste**.
  * `PF > 2.0` : **Performance institutionnelle d'élite**.
* **Espérance $E[R]$ / trade** : Gain moyen attendu à chaque prise de position. Une espérance $> +0.30 R$ est considérée comme excellente en trading actif.

### 2. Tableaux de Décomposition
* **📅 Performance Mensuelle** : Permet de vérifier la régularité mois par mois (Mai, Juin, Juillet) et de s'assurer qu'aucun mois n'est lourdement négatif.
* **🏆 Performance par Grade** : Permet d'analyser la sélectivité. Les grades `TRESFORT` et `FORT` doivent idéalement afficher le meilleur Win Rate et Profit Factor.
* **🎯 Performance par Pattern / Setup** : Identifie vos meilleurs déclencheurs (`FINISHED_AUCTION`, `DELTA_FLIP`, `CUM_DELTA_DIV`...) et ceux à éviter ou à filtrer (`OPEN_DRIVE_FAILURE`...).
* **🧭 Performance par Sens (LONG vs SHORT)** : Révèle si la stratégie surperforme à l'achat ou à la vente selon le régime de marché.

---

## 🛠️ Astuces & Bonnes Pratiques

### Vider le journal pour un test vierge
Si vous changez d'instrument ou de configuration et souhaitez repartir de zéro :
1. Fermez NinjaTrader 8 ou supprimez les anciens fichiers de journal dans :
   `C:\Users\<VotreNom>\Documents\NinjaTrader 8\`
2. Les fichiers `AuctionMarketCorePro_journal_sniper*.csv` seront recréés proprement lors du prochain recalcul.

### Tester différents presets
Vous pouvez comparer les résultats entre les différents profils du projet :
- **`ScalpingPro`** : Recommandé pour le trading réel, équilibre idéal entre sélectivité et opportunités (~5-10 setups/jour).
- **`Sniper`** : Ultra-sélectif, recherche uniquement les confluences maximales.
- **`Scanner`** / **`Research`** : Permissif, idéal pour cartographier tous les flux et calibrer les filtres.
