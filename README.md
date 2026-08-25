# Auction Market Core Pro (AMC-V8)

**AMC-V8** est un système de trading algorithmique institutionnel haute performance conçu pour **NinjaTrader 8** [1]. Il combine l'analyse de la théorie des enchères (*Auction Market Theory*), le **Volume Profile institutionnel multi-périodes** (*Closed References*), les **VWAP clôturés avec bandes d'écart-type ($SD \pm 1, \pm 2, \pm 3$)**, les nœuds de volume (**HVN / LVN**), l'ordre de flux (*Footprint / Delta Analysis*), la structure de marché (*Market Structure SMC*) et une gouvernance stricte des risques.

---

## 🚀 Dernières Mises à Jour & Optimisations (Août 2026)

### 1. Module Volume Profile Institutionnel & VWAP Clôturés
* **Zéro Biais d'Anticipation (*Strict Anti-Lookahead*)** : Séparation stricte entre les accumulateurs live en direct et les profils clôturés immuables (`Jour Précédent`, `Semaine Précédente`, `Mois Précédent`). Les niveaux ne dérivent jamais en cours de session.
* **VWAP Clôturés & Bandes d'Écart-Type ($SD \pm 1\sigma, \pm 2\sigma, \pm 3\sigma$)** : Calcul déterministe du VWAP et de la variance statistique sur les périodes hebdomadaire et mensuelle clôturées.
* **Niveaux de Classe A+ Institutionnels** : Les tests des bandes $SD \pm 2$ et $SD \pm 3$ (support/résistance macro) accordent automatiquement **+12 points** de localisation en $N2$.
* **Modulation Intelligente de Contre-Tendance ($N1$)** : Annulation des malus contre-tendance (`ibMod` et `htfM15`) et octroi d'un bonus de retournement (+2.0 pts) lorsque le prix teste un support ou une résistance macro extrême ($SD \pm 2 / \pm 3$).
* **Filtre Anti-Continuité sur Mur Macro** : Interdiction d'exécuter des ventes directes sur un support $SD -2 / -3$ ou des achats sur une résistance $SD +2 / +3$.
* **Persistance SQLite Locale** : Sauvegarde automatique de l'ensemble des profils, nœuds et métriques dans `amc_volume_profile.db` avec migration automatique du schéma.

### 2. Déverrouillage & Spécialisation des Gates (Scalping Pro)
* **Seuil Minimal d'Alerte** : Calibré à **`50/100`** pour un flux équilibré de 5 à 10 opportunités de qualité par session (Paliers : *Moyen* $\ge 45$, *Fort* $\ge 50$, *Très Fort* $\ge 65$) [2].
* **Spécialisation des Portes par Famille de Setup** : Les setups de flux/momentum (`DELTA_FLIP`, `CUM_DELTA_DIV`, `BREAKOUT_VAH/VAL`) ne sont plus bloqués par l'absence d'absorption passive ($N3$) ou de mèche contre-tendance ($N4$) lorsqu'une impulsion directionnelle de delta est confirmée [3].
* **Levée Intelligente des Portes Secondaires** : Lorsqu'un setup atteint un score global fort ($\ge 50$), les sous-notes marginales non-critiques n'entraînent plus de rejet éliminatoire [2].

### 3. Architecture Avancée du Risque & Stop Loss Dynamique
* **Stop Loss Dynamique Réel (`1.75 ATR`)** : Suppression du bridage artificiel en pips (`MaxStopPips = 0`) au profit d'un dimensionnement adapté à la volatilité de chaque instrument (15 à 40 points sur NQ/MNQ, 2 à 8 points sur ES, etc.) protégé par les niveaux structurels et un buffer de 6 ticks [2].
* **Filtre Anti-Doublon & Anti-Empilement** : Interdiction d'ouvrir un nouveau trade dans le même sens tant qu'une position de même direction est active (`openTrades`), éliminant l'accumulation de pertes consécutives sur les faux départs [3].

### 4. Gestion Adaptative des News & Contexte
* **Mode Pénalité News** : `NewsHardBlock = false` avec pénalité adaptative de **`-15 points`** (`NewsWindowPenalty = 15`) pendant les fenêtres économiques, permettant aux opportunités de très haute conviction d'être exécutées [2].
* **Mode Souple HTF (`HtfSoftMode = true`)** : Les désalignements sur les unités de temps supérieures appliquent une pénalité modulatrice de score sans rejet bloquant [2].
* **Configurations Multi-Actifs Synchronisées** : Alignement complet des 8 fichiers XML de configuration (`MNQ`, `NQ`, `ES`, `MES`, `GC`, `MGC`, `CL`, `MCL`) dans `configs/SCALPING_PRO/`.

---

## 📊 Fonctionnement Approfondi : Volume Profile, VWAP & Nœuds

```
                       FLUX DE MARCHÉ (TICKS / BARRES VOLUMÉTRIQUES)
                                             │
                                             ▼
                      ┌─────────────────────────────────────────────┐
                      │    Accumulateurs Live Déterministes         │
                      │       - Session Journée RTH / ETH           │
                      │       - Semaine en cours                    │
                      │       - Mois en cours                       │
                      └──────────────────────┬──────────────────────┘
                                             │
                                Clôture de Période (CME)
                                             │
                                             ▼
                      ┌─────────────────────────────────────────────┐
                      │    Profils Clôturés Immuables (Zéro Bias)   │
                      │       - POC, VAH (70%), VAL (70%)           │
                      │       - VWAP & Bandes SD ±1, ±2, ±3         │
                      │       - Nœuds Lissés HVN & LVN              │
                      └──────────────┬──────────────────────────────┘
                                     │
                 ┌───────────────────┴───────────────────┐
                 ▼                                       ▼
    ┌─────────────────────────┐             ┌─────────────────────────┐
    │   Base SQLite Locale    │             │ VolumeProfileAnalyzer   │
    │  amc_volume_profile.db  │             │ (VP LOC / VP CONF / N2) │
    └─────────────────────────┘             └─────────────────────────┘
```

### 1. Les Références Clôturées (*Closed References*)
* **POC (*Point of Control*)** : Prix ayant concentré le volume le plus massif de la période clôturée (accord maximal / *fair value*).
* **VAH (*Value Area High*) & VAL (*Value Area Low*)** : Encadrent **70% du volume total** distribué sur la période.
  * *Inside Value* : Marché en équilibre, propice aux stratégies de retournement vers le POC.
  * *Outside Value (Above VAH / Below VAL)* : Marché en déséquilibre (*imbalance*), propice aux continuations directionnelles ou aux réintégrations agressives.

---

### 2. Les VWAP Clôturés & Bandes d'Écart-Type ($SD \pm 1, \pm 2, \pm 3$)
Le VWAP clôturé hebdomadaire et mensuel représente le barycentre volumétrique officiel de l'institution. Les bandes d'écart-type statistiques sont calculées selon :

$$\text{VWAP} = \frac{\sum (P_i \times V_i)}{\sum V_i}, \quad \sigma = \sqrt{\max\left(0, \frac{\sum (P_i^2 \times V_i)}{\sum V_i} - \text{VWAP}^2\right)}$$

$$\text{Bande } SD \pm k = \text{VWAP} \pm (k \times \sigma) \quad \text{avec } k \in \{1.0, 2.0, 3.0\}$$

| Niveau Statistique | Couverture Gaussienne | Rôle Opérationnel dans AMC-V8 | Impact Scoring |
| :--- | :---: | :--- | :--- |
| **VWAP Clôturé** | Barycentre | Pivot central institutionnel / Règle de polarité | Pivot / Confluence |
| **$SD \pm 1\sigma$** | $68.27\%$ | Frontière de distribution normale standard | Confluence x1 (+2 pts) |
| **$SD \pm 2\sigma$** | $95.45\%$ | **Support / Résistance Macro Majeur** (Mur institutionnel) | **Classe A+ (+12 pts)**, Bonus Mean-Reversion (+2 pts) |
| **$SD \pm 3\sigma$** | $99.73\%$ | **Extrême Statistique Absolu** (Épuisement / Rebond violent) | **Classe A+ (+12 pts)**, Bonus Mean-Reversion (+2 pts) |

* **Comportement Réel Validé (Exemple du 24 Août 2026 sur MNQ)** : Le creux à 28 947.75 a testé précisément la bande **$SD -2$ du VWAP Monthly Clôturé** avant d'engager un puissant rebond de plus de 170 points.

---

### 3. Nœuds de Volume : HVN (*High Volume Node*) & LVN (*Low Volume Node*)
Détectés mathématiquement sur les profils hebdomadaires et mensuels par un **filtre de lissage Gaussien ($\sigma = 2.5\text{ ticks}$)** et calcul de proéminence relative :

```text
Volume
  ▲
  │        /\             /\    <─── HVN : Zone d'acceptation / Aimant de prix (Magnet)
  │       /  \   /\      /  \
  │──────/────\_/──\────/────\─── Volume Moyen de la Période
  │            \    \  /
  │             \____\/         <─── LVN : Zone de rejet / Accélération du flux (Vacuum)
  └───────────────────────────────────► Prix
```

* **HVN (*High Volume Node*) — Zones d'Acceptation** :
  * Régions où d'importants échanges ont été négociés dans le passé.
  * **Comportement** : Ralentissement de la vitesse des cours, absorption des ordres agressifs, zone de consolidation / congestion.
* **LVN (*Low Volume Node*) — Zones de Rejet & d'Accélération** :
  * Creux de volume marqués entre deux zones d'acceptation (manque de contrepartie historique).
  * **Comportement** :
    1. *Au premier test* : Rejet dynamique violent (barrière de liquidité).
    2. *En cas de traversée confirmée* : Traversée ultra-rapide (*slippage favorisé / pass-through*) sans résistance.
  * La qualité mathématique $q_{\text{LVN}} \in [0, 1]$ module la note $N2$ jusqu'à **+12 points**.

---

### 4. Moteur d'Inflexion & Régime Adaptatif Multi-Actifs (*Macro-Inflection & Continuous Stretch Engine*)
Le système intègre un moteur continu d'adaptation multi-régimes permettant de capturer les retournements majeurs d'épuisement tout en protégeant le capital lors des vraies tendances lourdes :

* **Reconnaissance Dynamique du Contexte ($N1$)** :
  * Lors d'une journée en forte tendance impulsive (*Trend Day*), les tests de bandes extrêmes ($SD \pm 2 / \pm 3$ ou $|Z_{\text{vwap}}| \ge 2.0\sigma$) sont identifiés comme des **zones d'inflexion macro valides** (+10 pts en $N1$).
  * L'extension d'$IB$ extrême ($IB_{\text{ext}} \ge 2.0$) et le non-chevauchement de Value Area sont valorisés (+6 pts chacun) comme signatures d'élongation terminale.
  * La note de contexte $N1$ passe ainsi de **4.0/30 à 26.0/30**, déverrouillant les opportunités d'inversion statistique.
* **Amortissement Continu d'Étirement dans ScalpingPro** :
  * Les pénalités de contre-tendance (`htfM15`, `ibMod`) s'amortissent progressivement au fur et à mesure que l'élongation augmente ($|Z| \ge 2.0\sigma \rightarrow 0.0$, $|Z| \ge 2.5\sigma \rightarrow +1.0$).
  * Maintien strict du filtre anti-continuation interdisant les achats sous $SD +2/+3$ ou les ventes sur $SD -2/-3$ (`ibMod -= 5.0`).
* **Verrou de Sécurité Anti-Couteau Tombant ($N3 / N4$)** :
  * Aucun trade n'est pris au simple toucher passif d'une bande : l'obligation de preuve par la microstructure et l'Orderflow ($N3 \ge 3.0$ : DeltaFlip validé, divergence CVD, bougie de rejet $\ge 40\%$, ou Finished Auction) reste **infranchissable**, éliminant les faux rebonds sur les instruments directionnels lourds (Gold, Crude Oil).

---

### 5. Indicateurs Dashboard & Confluences : `VP LOC` & `VP CONF`
* **`VP LOC` (*Volume Profile Location*)** : Synthétise la position exacte du prix par rapport à la structure globale (`INSIDE_VALUE`, `ABOVE_VAH`, `BELOW_VAL`, `TEST_POC`, `TEST_SD2_MONTH`, etc.).
* **`VP CONF` (*Volume Profile Confluence*)** : Identifie les intersections multi-temporelles et multi-modèles (ex: $\text{VAL Semaine} + \text{VWAP } SD -2\text{ Mois} + \text{NPOC}$), déclenchant des alertes Telegram prioritaires et le scoring maximal de confluence.

---

## 📂 Structure du Dépôt GitHub

```text
AMC-V8/
├── SniperMarketCorePro.cs              # Moteur principal de l'indicateur & Intégration NinjaTrader
├── SniperMarketCorePro.Sniper.cs       # Logique du module Sniper, N1 Contexte Adaptatif, N2 Localisation & Shadow Journal
├── SniperMarketCorePro.ScalpingPro.cs  # Modulateurs N1, Scoring pondéré, Amortissement continu & Détection extrêmes VWAP
├── SniperMarketCorePro.Engine.cs       # Moteur de calcul des flux, deltas, CVD et microstructure
├── SniperMarketCorePro.Features.cs     # Extraction des patterns de footprint & absorption
├── SniperMarketCorePro.VolumeProfile.cs# Gestion des événements VP, contextes et alertes Telegram
├── SniperMarketCorePro.Render.cs       # Rendu graphique WPF et affichage du Dashboard
├── VolumeProfile/                      # Moteur Volume Profile autonome
│   ├── VolumeProfileModels.cs          # Modèles de données (ClosedVolumeProfile, Nodes, RefLevel)
│   ├── VolumeProfileCalculator.cs      # Calcul déterministe POC, VA 70%, VWAP, SD1/2/3, HVN/LVN
│   ├── VolumeProfileRepository.cs      # Persistance SQLite, tables et migration de schéma
│   ├── VolumeProfileManager.cs         # Transitions de sessions (RTH/Jour/Sem/Mois) et cache RAM
│   └── VolumeProfileAnalyzer.cs        # Analyse de proximité, confluences et VP LOC / VP CONF
├── Tests/                              # Suite de tests de production (.NET Core)
│   ├── Program.cs                      # 35 tests unitaires validant calculs, SQLite, scoring et inflexion
│   └── VolumeProfileTests.csproj       # Projet de tests automatisés
├── MD/                                 # Guides techniques et documentation approfondie
│   └── VOLUME_PROFILE_GUIDE.md         # Manuel complet Volume Profile, mathématiques et playbooks
├── configs/                            # Fichiers de configuration XML institutionnels par instrument
│   ├── SCALPING_PRO/                   # Presets Scalping Pro (MNQ, NQ, ES, MES, GC, MGC, CL, MCL)
│   ├── SNIPER/                         # Presets Sniper
│   └── STANDARD/                       # Presets Standard
├── Python/                             # Scripts d'audit de performance et analyse de signaux
├── historical-data/                    # Données de marché haute résolution (Ticks / 1-Minute)
├── shadow/                             # Journaux d'audit de production (Shadow Outlines)
└── README.md                           # Documentation générale du projet
```

---

## 🛠️ Installation et Validation

### 1. Compilation & Tests Automatisés
Le projet intègre une suite complète de **35 tests unitaires de non-régression** (Volume Profile, VWAP Clôturé, SD Bands, SQLite, Inflexion Macro N1, Amortissement Continu, SMC, FVG, Footprint, Dashboard) :
```powershell
dotnet run --project Tests/VolumeProfileTests.csproj
```

### 2. Déploiement dans NinjaTrader 8
1. Copiez les fichiers `.cs` et les dossiers `VolumeProfile/` et `MarketIntelligence/` dans le répertoire d'indicateurs personnalisés de NinjaTrader 8 :
   ```text
   Documents\NinjaTrader 8\bin\Custom\Indicators\
   ```
2. Compilez via l'éditeur NinjaScript (touche `F5`).
3. Appliquez l'indicateur `SniperMarketCorePro` sur votre graphique (ex: `MNQ` ou `NQ` en 1-minute / 5-minutes volumétrique).
4. Chargez le preset XML correspondant à votre instrument dans `configs/SCALPING_PRO/`.

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
* Consultez les journaux d'audit situés dans `shadow/AuctionMarketCorePro_journal_sniper.csv` pour analyser chaque opportunité détectée, son score pondéré, ses sous-notes ($N1$ à $N4$) et ses $R$-multiples.

---

## Références

[1] Documentation technique du projet AMC-V8, *Architecture institutionnelle*, Août 2026.  
[2] Fichier `SniperMarketCorePro.ScalpingPro.cs`, Paramètres de seuil, scoring pondéré, modulateurs VWAP et risque.  
[3] Fichier `SniperMarketCorePro.Sniper.cs`, Spécialisation des Gates, Confluences Classe A+ et gestion du risque.  
[4] Module `VolumeProfile/`, Modèles mathématiques, calcul déterministe et persistance SQLite.  
[5] Document `MD/VOLUME_PROFILE_GUIDE.md`, Manuel complet Volume Profile et playbooks d'intervention.  
[6] Dépôt GitHub `amc-pro/AMC-V8`, Dossier `/configs/SCALPING_PRO/`.  
[7] Système de journalisation Shadow, `shadow/AuctionMarketCorePro_journal_sniper.csv`.

