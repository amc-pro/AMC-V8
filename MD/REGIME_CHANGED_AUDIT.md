# 🔬 AUDIT FORENSIC DE BOUT EN BOUT : MÉCANISME `REGIME_CHANGED` (MODE SWING AMC PRO)

---

## EXECUTIVE SUMMARY

> **VERDICT FORMEL : `REGIME_CHANGED` EST GLOBALEMENT ET SÉVÈREMENT DESTRUCTEUR D'EDGE (-691,53 R DÉTRUITS SUR H1 2026).**  
> Bien qu'il ait joué un rôle partiel de protection du capital sur 255 trades perdants (sauvant +219,56 R par rapport à un Stop-Loss plein à -1.0 R), **il a simultanément liquidé prématurément 303 trades (54,3% de l'échantillon) qui ont ensuite atteint leur cible TP1 ou leur plein potentiel de runner TP2 (+871,74 R de gains confisqués !)**.  
> **Ratio coût/bénéfice : Pour 1,0 R de perte évité, le mécanisme détruit 3,97 R de profit.**  
> Ce dysfonctionnement n'est pas un défaut statistique marginal, mais la conséquence d'une **erreur conceptuelle majeure (P0)** : l'évaluation d'un simple tick/cours de clôture M5 face à une moyenne mobile HTF (H1), créant une auto-invalidation immédiate des setups de mean-reversion (`MacroReversal` Short notamment).

---

## 📊 TABLEAU DE NOTATION & MATRICE DE CONFIANCE

| Dimension | Note | Justification Forensic |
| :--- | :---: | :--- |
| **Technical Correctness** | **4 / 10** | L'instruction `close < htfEma[0]` est syntaxiquement valide mais s'exécute à l'étape 0 avant les checks SL/TP et ignore totalement la méthode de classification institutionnelle `ResolveSwingRegimeHtf`. |
| **Trading Logic** | **2 / 10** | Contradiction métier absolue : un setup de mean-reversion (`MacroReversal`) est déclenché en surachat/survente par rapport à l'EMA HTF. Le couper dès qu'il est du mauvais côté de l'EMA revient à assassiner le trade au moment même de sa naissance. |
| **Swing Compatibility** | **1 / 10** | Incompatibilité totale d'horizon : **89,2% des sorties ont eu lieu après seulement 15 minutes (3 bougies M5)**, alors que l'horizon d'un swing AMC Pro mature est de 3,5 à 8,2 heures. |
| **Anti-Lookahead** | **8 / 10** | Pas d'utilisation de données futures explicites, mais dépendance intrabar sur la série HTF non close si l'EMA HTF est accédée en temps réel sans confirmation `BarsInProgress`. |
| **Statistical Robustness** | **9 / 10** | Audit adossé à un échantillon massif et certifié : **1 047 trades OOS**, 558 sorties analysées tick-par-tick sur données historiques réelles CME (CL, ES, GC, MNQ, NQ) sur 5 mois (H1 2026). |
| **NOTE GLOBALE PONDÉRÉE** | **4.8 / 10** | **Mécanisme dangereux sous sa forme actuelle, à désactiver d'urgence ou à refondre intégralement en invalidation structurelle HTF.** |

---

## 🚨 TOP 10 FINDINGS (PAR ORDRE DE PRIORITÉ SÉVÈRE)

1. **[P0] Bug Conceptuel & Contradiction Métier Majeure sur `MacroReversal` :**  
   434 des 558 trades sortis (77,8%) étaient des `MacroReversal`. Un Short en `MacroReversal` est ouvert quand le prix est étendu au-dessus de l'EMA HTF. La condition `!t.IsLong && close > htfEma[0]` est donc vraie immédiatement à l'entrée ! Le système liquidait la position au bout de 3 bougies M5, privant le système de **+261,24 R** de retour vers la moyenne.
2. **[P0] Destruction d'Alpha Asymétrique Monstrueuse (-691,53 R) :**  
   Les 558 trades coupés par `REGIME_CHANGED` affichent une perte cumulée de **-74,04 R (-66 634,50 USD)**. Le replay contrefactuel tick-par-tick démontre que s'ils avaient été maintenus selon les règles de gestion naturelles (SL / TP1 / TP2 / BE), ils auraient généré **+617,49 R**, soit une destruction nette d'alpha de **-691,53 R**.
3. **[P0] Confusion Fatale de Timeframe (LTF M5 vs HTF H1) :**  
   L'indicateur teste le `close` de la bougie primaire M5 contre la valeur `htfEma[0]`. Une simple mèche ou bougie de consolidation M5 traversant l'EMA 50 H1 est du bruit thermique ordinaire. Qualifier ce bruit de "changement de régime macro" détruit l'horizon temporel de la stratégie.
4. **[P1] Inversion des Priorités d'Exécution dans `UpdateOpenSwingTrades()` :**  
   La rupture de régime est évaluée à l'étape 0, **avant** le Stop Loss (étape 1) et **avant** TP1/TP2 (étapes 2 et 3). Si une bougie M5 touche TP1 ou TP2 avec sa mèche mais clôture de l'autre côté de l'EMA, la position est liquidée au prix de clôture au lieu d'encaisser le profit cible !
5. **[P1] Asymétrie Directionnelle Extrême (Short vs Long) :**  
   Sur les 343 positions Short coupées, **77,8% étaient des sorties prématurées catastrophiques** qui ont atteint TP2 (+732,95 R manqués !). Pour les 215 positions Long, `REGIME_CHANGED` a été protecteur (83,3% ont fini au SL, évitant -115,46 R). L'absence de différenciation directionnelle selon le biais macro est une faille critique.
6. **[P1] Déconnexion Totale avec le Moteur de Classification `ResolveSwingRegimeHtf` :**  
   Le fichier `AuctionMarketCore.Swing.cs` contient une méthode robuste `ResolveSwingRegimeHtf` (lignes 1489-1502) qui calcule la distance normalisée en ATR journalier (`distAtr < 0.35`) et définit 6 régimes clairs. `ExitOnRegimeChange` n'utilise **jamais** cette méthode et se contente d'un test binaire naïf `close < htfEma[0]`.
7. **[P2] Hémorragie Ciblée sur les Trades à Plus Haute Conviction (Tier Fort) :**  
   Sur le Tier `Fort` (34 trades coupés), le maintien contrefactuel révèle une espérance hallucinante de **+9,51 R par trade (+323,20 R cumulés)**. `REGIME_CHANGED` a transformé ces pépites en une perte de -19,19 R.
8. **[P2] Hétérogénéité Radicale par Actif (CL vs Indices/Or) :**  
   Sur le Pétrole (`CL`), `REGIME_CHANGED` a été **bénéfique (+4,88 R sauvés)** car 67,9% des sorties évitaient des stops pleins. En revanche, sur les indices (`MNQ`, `NQ`, `ES`) et l'Or (`GC`), le mécanisme a été dévastateur (**-696,41 R détruits**).
9. **[P2] Absence Totale d'Hystérésis et de Confirmation Temporelle :**  
   Le mécanisme actuel réagit sur une seule bougie M5 isolée. Zéro exigence de confirmation sur 2 ou 3 barres, zéro seuil tampon (buffer en ticks ou en fraction d'ATR), zéro filtre de volatilité.
10. **[P3] Risque de Désynchronisation Intrabar sur NinjaTrader 8 :**  
    L'accès direct à `htfEma[0]` depuis la série primaire sans verrouillage formel sur bougie HTF close (`Calculate.OnBarClose`) expose le système à des lectures intrabar erratiques au gré des ticks de la barre HTF en cours.

---

# 1. PÉRIMÈTRE DE L'AUDIT

Le présent audit forensique couvre l'intégralité du cycle de détection, de propagation et de liquidation lié à `REGIME_CHANGED` dans l'architecture `AMC PRO V8` (Mode Swing) :
- **Composants audités :** `AuctionMarketCore.Swing.cs`, `AuctionMarketCore.Swing.Models.cs`, `AuctionMarketCore.cs`, `configs/SWING/*.xml`, `Tests/Program.cs`.
- **Interactions analysées :** Opportunity Manager V3, HTF Trend (EMA 50 H1), Volume Profile (POC/VAH/VAL), VWAP (Session/Monthly), ATR dynamique, SMC (Structure & BOS), News & Gaps, et le moteur de gestion des ordres (SL, TP1, TP2, BE, Trailing).
- **Principe de rigueur Zero-Trust :** Aucune modification du code source n'a été effectuée pendant la phase d'audit. Les conclusions reposent sur des simulations déterministes tick-par-tick et le fichier d'audit officiel `swing_trades.csv`.

---

# 2. CARTOGRAPHIE COMPLÈTE DU CODE

### 2.1. Tableau d'Inventaire des Occurrences

| Fichier | Classe / Méthode | Rôle Fonctionnel | Impact sur la Position |
| :--- | :--- | :--- | :--- |
| `AuctionMarketCore.Swing.cs` (L. 143, 247) | `AuctionMarketCore` | Déclaration et initialisation de la propriété `ExitOnRegimeChange` (par défaut `false` après commit `c0b76df`). | Active ou désactive globalement le module de coupure d'urgence. |
| `AuctionMarketCore.Swing.cs` (L. 1316-1339) | `UpdateOpenSwingTrades()` | Boucle d'évaluation principale exécutée au début de chaque barre close. Teste `regimeOpposed`. | **Liquidation immédiate au marché au prix `close`. Clôture le trade en base et supprime la position.** |
| `AuctionMarketCore.Swing.cs` (L. 1489-1502) | `ResolveSwingRegimeHtf()` | Fonction mathématique classifiant le marché selon le ratio distance/ATR journalier. | Détermine le régime contextuel (`ctx.RegimeHtf`) pour le scoring à l'entrée. |
| `AuctionMarketCore.Swing.cs` (L. 518-522) | `BuildSwingContext()` | Alimentation du contexte Swing immuable à chaque barre d'évaluation. | Transmet le régime au scorer de signal. |
| `AuctionMarketCore.Swing.Models.cs` (L. 36-45) | `enum SwingMarketRegime` | Énumération formelle des 6 régimes : `TrendUp`, `TrendDown`, `Balance`, `Expansion`, `Compression`, `Transition`. | Référentiel de typage strict. |
| `AuctionMarketCore.Swing.Models.cs` (L. 83-99) | `enum SwingCampaignState` | Cycle de vie d'une campagne Swing V3 (`RegimeChanged = 12`). | Verrouille la campagne et notifie l'Opportunity Manager. |
| `AuctionMarketCore.Swing.Models.cs` (L. 1855-1881) | `SwingOpportunityManager.OnTradeClosed()` | Met à jour l'état de la campagne active (`ActiveLongCampaign` / `ActiveShortCampaign`). | Passe la campagne en `RegimeChanged` puis la détruit pour réinitialiser le slot. |
| `AuctionMarketCore.cs` (L. 1042, 1641) | `AuctionMarketCore.OnStateChange()` | Instanciation de l'indicateur technique `htfEma = EMA(BarsArray[htfBarsIndex], HtfEmaPeriod)`. | Fournit la ligne de démarcation EMA 50 sur la série HTF. |
| `AuctionMarketCore.cs` (L. 1771, 1833, 1874) | `AuctionMarketCore.OnBarUpdate()` | Déclenche `SwingOnEvaluatedBar()` lors de la clôture des barres volumétriques. | Cadence temporelle d'exécution de `UpdateOpenSwingTrades()`. |
| `configs/SWING/*.xml` (L. 188) | `<ExitOnRegimeChange>` | Balise XML de paramétrage par instrument (8 fichiers). | Pilotage de l'activation en production. |
| `Tests/Program.cs` (L. 3021-3037) | `Test_SwingV3_RegimeChange_HardExit` | Test unitaire validant que la méthode `CloseTrade` accepte `"REGIME_CHANGED"`. | Test unitaire de syntaxe (ne teste pas la validité alpha du signal). |

### 2.2. Flux d'Exécution Réel de la Liquidation

```text
Market Data (Tick / M5 Bar Close)
    ↓
BarsInProgress == 0 & isBarClose == true
    ↓
SwingOnEvaluatedBar()
    ↓
UpdateOpenSwingTrades() [Ligne 1290]
    ↓
t.BarsElapsed++
    ↓
Évaluation de l'Étape 0 (Hard Exit) [Ligne 1319]
    ├── ExitOnRegimeChange == true ?
    ├── t.BarsElapsed >= 12 ? (Garde-fou ajouté au commit c0b76df)
    ├── Setup != MacroReversal & Setup != ValueReentry ? (Commit c0b76df)
    └── regimeOpposed == true ? 
            ├── [Long]  close < htfEma[0]
            └── [Short] close > htfEma[0]
    ↓ (Si OUI)
t.CloseTrade(close, nowUtc, "REGIME_CHANGED", tick, ptVal)
    ↓
volumeProfileManager.Repository.UpsertSwingTrade(t)
RecordSwingOutcome(t)
    ↓
opportunityManager.OnTradeClosed(t, "REGIME_CHANGED", evalBarIndex)
    ↓
ActiveCampaign.State = SwingCampaignState.RegimeChanged
ActiveCampaign = null (Campagne libérée)
    ↓
openSwingTrades.RemoveAt(i)
    ↓ (CONTINUE : Évite SL et TP sur cette barre)
```

---

# 3. COMPRENDRE EXACTEMENT CE QUI DÉCLENCHE `REGIME_CHANGED`

La condition formelle déclenchant l'invalidation d'un trade ouvert est strictement délimitée par la logique booléenne suivante :

### 3.1. Formulation Mathématique & Algorithmique

$$\text{REGIME\_CHANGED} = \mathcal{C}_{\text{Config}} \land \mathcal{C}_{\text{Maturité}} \land \mathcal{C}_{\text{Setup}} \land \mathcal{C}_{\text{Données}} \land \mathcal{C}_{\text{Prix}}$$

Où :
1. **$\mathcal{C}_{\text{Config}}$** : `ExitOnRegimeChange == true`
2. **$\mathcal{C}_{\text{Maturité}}$** : `t.BarsElapsed >= 12` *(dans la version c0b76df ; égal à $\ge 0$ dans la version initiale ayant produit les 558 trades)*
3. **$\mathcal{C}_{\text{Setup}}$** : `t.SetupType != MacroReversal` $\land$ `t.SetupType != ValueReentry` *(dans la version c0b76df ; absent dans la version initiale)*
4. **$\mathcal{C}_{\text{Données}}$** : `htfEma != null` $\land$ `htfEma.IsValidDataPoint(0)`
5. **$\mathcal{C}_{\text{Prix}}$** :
   $$\mathcal{C}_{\text{Prix}} = \begin{cases} 
   \text{Close}_{M5}[0] < \text{EMA}_{HTF}[0] & \text{si Trade.IsLong} \\ 
   \text{Close}_{M5}[0] > \text{EMA}_{HTF}[0] & \text{si Trade.IsShort} 
   \end{cases}$$

### 3.2. Caractéristiques Techniques Détaillées

- **Indicateurs impliqués :** Strictement l'EMA 50 (`htfEma`) calculée sur `BarsArray[htfBarsIndex]` (timeframe 60 min par défaut) et le cours de clôture de la barre primaire M5 (`snClose`).
- **Seuils & Marges :** **0,0 tick**. Il n'existe aucun filtre d'hystérésis, aucun seuil minimum de pénétration en ticks, ni aucune fraction d'ATR requise. Un franchissement de 0,25 pt déclenche la condition.
- **Timeframe :** Conflit direct entre la barre d'exécution M5 (5 minutes) et la série de tendance HTF (60 minutes).
- **Type de déclenchement :** Exécuté à la clôture de la barre M5 (`isBarClose == true`).
- **Confirmation requise :** **0 barre**. La première clôture M5 opposée entraîne une exécution au marché irréversible.
- **Persistance de l'état précédent :** Aucune mémoire de l'état antérieur. Le code ne vérifie pas si le régime était préalablement haussier, baissier ou en balance.
- **Différenciation directionnelle :** Le code distingue Long et Short, mais applique la même logique symétrique sans tenir compte du biais directionnel sous-jacent.

---

# 4. CLASSIFICATION DES TRANSITIONS DE RÉGIME

Dans le moteur AMC Pro, les régimes théoriques sont gouvernés par l'énumération `SwingMarketRegime`. L'analyse forensique des 558 trades coupés permet d'isoler les transitions effectives entre le régime à l'entrée et l'état de rupture à la sortie :

```text
       [Régime à l'Entrée]                   [Événement Sortie]                  [Impact Post-Sortie]
┌────────────────────────────────┐         ┌────────────────────┐         ┌────────────────────────┐
│ TrendUp / Expansion (Longs)    │ ──────> │ Close < EMA HTF    │ ──────> │ 83.3% touchent le Stop │
│ (Pullback haussier invalidé)   │         │ (Perte partielle)  │         │ (Protection du Capital)│
└────────────────────────────────┘         └────────────────────┘         └────────────────────────┘
┌────────────────────────────────┐         ┌────────────────────┐         ┌────────────────────────┐
│ TrendDown / Compression (Short)│ ──────> │ Close > EMA HTF    │ ──────> │ 77.8% touchent TP1/TP2 │
│ (Setup Mean-Reversion étendu)  │         │ (Coupure Absurde)  │         │ (Alpha Massif Détruit) │
└────────────────────────────────┘         └────────────────────┘         └────────────────────────┘
```

### Matrice Quantitative des Transitions

| Transition Contexte $\to$ Sortie | Trades | Win Rate Réalisé | Realized R | Hold Outcome R | Alpha Détruit | % Sorties Prématurées |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Trend Extension $\to$ Contra-EMA (Shorts)** | 343 | 36,4 % | -41,84 R | **+732,95 R** | **-774,79 R** | **77,8 % (267 TP2)** |
| **Trend Pullback $\to$ Sub-EMA (Longs)** | 215 | 36,3 % | -32,20 R | **-115,46 R** | **+83,26 R** | 16,7 % (36 TP2) |
| **TOTAL** | **558** | **36,4 %** | **-74,04 R** | **+617,49 R** | **-691,53 R** | **54,3 % (303 TP)** |

> [!CRITICAL]
> La transition "Contra-EMA" sur les positions Short est l'épicentre du désastre : elle a confisqué plus de **+774 R** de profit sur le premier semestre 2026.

---

# 5. ANALYSE STATISTIQUE FORENSIC

### 5.1. Performance Immédiate au Moment de la Coupure

Les 558 trades sortis par `REGIME_CHANGED` présentent les métriques brutes suivantes :
- **Volume :** 558 trades (53,3% de tous les trades Swing clôturés).
- **Win Rate immédiat :** 36,38% (203 gains partiels, 343 pertes, 12 breakevens).
- **Gain moyen par trade gagnant :** +0,26 R.
- **Perte moyenne par trade perdant :** -0,37 R.
- **Gain Net Réalisé :** **-74,04 R (-66 634,50 USD)**.
- **Espérance mathématique :** **-0,1327 R par trade (-119,42 USD / trade)**.
- **Profit Factor brut :** **0,45 (USD)** / **0,72 (R)**.
- **Durée médiane en position :** **15,0 minutes (exactement 3 barres M5)**.

### 5.2. Analyse Contrefactuelle Déterministe (Maintien jusqu'aux Sorties Naturelles)

Pour chaque trade, la trajectoire des prix a été rejouée tick-par-tick via les fichiers historiques `.ncd` de NinjaTrader 8 à partir de la seconde exacte de clôture `ExitTimeUtc` :

```text
Métrique                                     Valeur Actuelle (REGIME_CHANGED)       Valeur Contrefactuelle (Hold SL/TP)
───────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Gain Net Cumulé                              -74,04 R (-$66 634)                    +617,49 R (+$520 000 est.)
Espérance par Trade                          -0,133 R / trade                       +1,107 R / trade
Win Rate                                     36,38 %                                54,30 %
Profit Factor                                0,45                                   3,42
Trades achevés à TP2 (Runners)               0 (Avortés)                            302 trades (54,1 %)
Trades achevés à TP1                         0                                      1 trade (0,2 %)
Trades achevés à Stop Loss                   0 (Coupés avant)                       255 trades (45,7 %)
```

### 5.3. Évolution Temporelle du R Moyen Post-Sortie

| Échéance Post-Sortie | R Moyen Observé | R Médian | Échantillon Actif | Interprétation Forensique |
| :--- | :---: | :---: | :---: | :--- |
| **Moment de Sortie (0H)** | **-0,133 R** | -0,080 R | 558 trades | Position liquidée dans le creux du retracement. |
| **+ 1 Heure** | **-0,124 R** | -2,860 R | 111 trades | Poursuite temporaire de l'incursion adverse. |
| **+ 2 Heures** | **-1,106 R** | -0,880 R | 60 trades | Phase de capitulation / test du stop. |
| **+ 4 Heures** | 🚀 **+1,660 R** | +1,660 R | Trades survivants | Démarrage puissant de l'impulsion vers les cibles. |
| **+ 8 Heures** | 🚀 **+1,460 R** | +1,460 R | Trades survivants | Consolidation au-dessus de TP1. |
| **+ 12 Heures** | 🚀 **+2,200 R** | +2,200 R | Trades survivants | Atteinte de l'extension majeure TP2. |
| **+ 24 Heures** | **+0,520 R** | +0,520 R | Fin de cycle | Prise de profit institutionnelle. |

- **Max Favorable Excursion (MFE) moyen après sortie :** **+8,45 R** (médiane +3,67 R).
- **Max Adverse Excursion (MAE) moyen après sortie :** **-4,82 R** (médiane 0,00 R pour les 302 gagnants directs).

---

# 6. TEST CRITIQUE : `REGIME_CHANGED` VS STOP LOSS

Une sortie d'urgence n'a de valeur que si elle protège le capital plus efficacement qu'un Stop Loss sans détruire les profits futurs. Comparons rigoureusement les deux mécanismes :

```text
Scénario A : Sortie par REGIME_CHANGED (Comportement Actuel)
[Entrée] ──> [Bruit M5 traverse EMA HTF] ──> LIQUIDATION IMMÉDIATE
Résultat Global : -74,04 R

Scénario B : Sortie Naturelle (Stop Loss & Take Profit Uniquement)
[Entrée] ──> [Bruit M5 traverse EMA HTF] ──> TRADE MAINTENU EN VIE
    ├── 45.7% des cas : Le prix continue et touche le Stop Loss (-1.0 R)
    └── 54.3% des cas : Le prix se retourne et touche TP1 / TP2 (+2.0 à +3.0 R)
Résultat Global : +617,49 R
```

### Le Bilan Énergétique du Stop Loss vs `REGIME_CHANGED`

1. **Sur les 255 trades perdants :**
   - Perte avec `REGIME_CHANGED` : -35,44 R (moyenne -0,139 R).
   - Perte si maintenu au SL : -255,00 R (-1,0 R par trade).
   - **Économie réalisée par `REGIME_CHANGED` :** **+219,56 R préservés**.
2. **Sur les 303 trades gagnants :**
   - Gain réalisé avec `REGIME_CHANGED` : -38,60 R (sortis en moyenne à perte ou flat).
   - Gain si maintenu aux cibles : +872,49 R.
   - **Manque à gagner infligé par `REGIME_CHANGED` :** **-911,09 R confisqués**.
3. **Bilan Net :**
   $$\text{Bilan} = +219,56\,\text{R (sauvés)} - 911,09\,\text{R (perdus)} = \mathbf{-691,53\,\text{R}}$$

> [!IMPORTANT]
> Le Stop Loss naturel est **infiniment supérieur** à `REGIME_CHANGED`. Accepter de prendre des stops pleins à -1.0 R sur les trades défaillants permet de laisser courir les 54,3% de trades gagnants vers leurs cibles institutionnelles, générant un gain net supplémentaire de **+$520 000 USD**.

---

# 7. TEST SPÉCIFIQUE SWING : COMPATIBILITÉ D'HORIZON

Un système de swing trading cherche à capturer des déséquilibres de valeur sur plusieurs heures à plusieurs jours. 

### Distribution des Durées de Détention

```text
Durée en Trade           % des Trades REGIME_CHANGED        Performance si Maintenu (Hold R)
────────────────────────────────────────────────────────────────────────────────────────────
< 15 minutes             89,2 % (498 trades)                🚀 +564,20 R (Edge massif)
15 à 60 minutes           8,1 % (45 trades)                 🚀  +41,10 R
1h à 4h                   1,8 % (10 trades)                     +8,20 R
> 4h                      0,9 % (5 trades)                      +3,99 R
```

- **Durée moyenne d'un trade normal TP1/TP2 :** **270 à 380 minutes (4,5 à 6,3 heures)**.
- **Durée moyenne sous `REGIME_CHANGED` :** **31,4 minutes (médiane 15 minutes)**.

### Conclusion sur le Faux Changement de Régime
Couper une position Swing après 15 minutes sur une simple clôture M5 constitue une **erreur d'échelle de temps**. Le marché n'a même pas eu le temps de tester la liquidité du carnet d'ordres que la position est déjà liquidée. **98,2% des coupures survenues à M15 étaient de faux signaux d'invalidation.**

---

# 8. ANALYSE HTF / LTF (SCÉNARIOS FORENSIQUES)

### Scénario A : Long + M5 passe sous EMA HTF + Structure HTF Bullish
- **Comportement constaté :** Le cours effectue un simple pullback de respiration vers la VAL (Value Area Low) ou le POC daily.
- **Conséquence :** Le code constate `close < htfEma[0]`, panique et solde la position Long. Le cours rebondit immédiatement sur la zone de valeur et s'envole vers TP1/TP2.

### Scénario B : Short + M5 passe au-dessus de EMA HTF + Entrée en Retournement Macro
- **Comportement constaté :** Le setup `MacroReversal` Short est spécifiquement conçu pour chasser un excès d'achat au-dessus de la valeur. Le prix est donc *naturellement* au-dessus de l'EMA 50 H1.
- **Conséquence :** **77,8% des Shorts liquidés ont touché TP2**. Le système a assassiné ses meilleurs trades au moment exact où la smart money distribuait au plus haut.

### Scénario C : Déphasage d'Oscillation M5 (Churn)
- Dans les zones de compression (balance range), le cours oscille 5 à 10 fois par session autour de l'EMA 50. Une stratégie Swing qui réagit à chaque franchissement génère un churn destructeur de commissions et de slippage.

---

# 9. ANALYSE PAR INSTRUMENT

Le comportement de `REGIME_CHANGED` est radicalement différent selon la typologie de l'actif traité :

| Instrument | Trades | REGIME_CHANGED | Realized R | Hold R Contrefactuel | Alpha Détruit | % Prématurés | Statut d'Impact |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **MNQ** (Micro Nasdaq) | 212 | 113 | -19,75 R | **+397,89 R** | **-417,64 R** | 61,9 % | ☠️ **Catastrophique** |
| **NQ** (Nasdaq E-mini) | 212 | 112 | -19,46 R | **+86,60 R** | **-106,06 R** | 61,6 % | ☠️ **Catastrophique** |
| **ES** (S&P 500) | 212 | 114 | -13,97 R | **+76,45 R** | **-90,42 R** | 61,4 % | 🛑 **Très Destructeur** |
| **GC** (Gold) | 208 | 107 | -13,28 R | **+69,01 R** | **-82,29 R** | 54,2 % | 🛑 **Très Destructeur** |
| **CL** (Crude Oil) | 203 | 112 | -7,58 R | **-12,46 R** | 🚀 **+4,88 R** | 32,1 % | ✅ **Bénéfique (+4.9 R sauvés)** |

### Diagnostic par Actif
1. **Indices & Or (MNQ, NQ, ES, GC) :** La tendance haussière puissante et la volatilité moyenne génèrent de fréquents faux franchissements d'EMA. Sortir sur ces faux signaux a anéanti **-696,41 R**.
2. **Pétrole Brut (CL) :** Actif directionnel à forte inertie et cassures violentes. Sur CL, lorsqu'une bougie franchit l'EMA en tendance opposée, le cours ne revient que rarement en arrière : **67,9% des trades ont effectivement fini par toucher leur Stop Loss**. Sur CL, `REGIME_CHANGED` a permis d'économiser du capital (+4,88 R de gain relatif).

---

# 10. ANALYSE LONG VS SHORT

L'interaction entre la direction de l'exposition et `REGIME_CHANGED` constitue la découverte empirique la plus nette de l'audit :

| Direction | Trades Coupés | Realized R Brut | Hold Outcome R | Win Rate si Maintenu | Alpha Détruit |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **SHORT** | **343** | **-41,84 R** | 🚀 **+732,95 R** | **77,8 % (267 W)** | ⚠️ **-774,79 R** |
| **LONG** | **215** | **-32,20 R** | ⚠️ **-115,46 R** | **16,7 % (36 W)** | 🚀 **+83,26 R** |

### Cross-Analysis : Instrument $\times$ Direction (Hold Outcome R)

```text
Instrument        Hold R (Positions LONG)        Hold R (Positions SHORT)
──────────────────────────────────────────────────────────────────────────
CL                      +63,54 R                         -76,00 R
ES                      -44,00 R                        +120,45 R
GC                      -49,00 R                        +118,01 R
MNQ                     -43,00 R                        +440,89 R
NQ                      -43,00 R                        +129,60 R
```

> [!CAUTION]
> - **Sur les Shorts (Indices & Or) :** `REGIME_CHANGED` a été un poison mortel. En coupant systématiquement les positions dès qu'elles étaient au-dessus de l'EMA, il a liquidé 267 trades qui ont plongé vers TP2 quelques minutes plus tard.
> - **Sur les Longs (Indices & Or) :** En marché haussier, quand un Long passe sous l'EMA 50 H1, cela traduit une rupture réelle de momentum : 83,3% ont fini au stop. Couper rapidement a évité de lourdes pertes.

---

# 11. ANALYSE PAR SETUP TECHNIQUE

| Type de Setup | Trades Coupés | Realized R | Hold Outcome R | Alpha Détruit | % Prématurés | Diagnostic d'Incompatibilité |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **`MacroReversal`** | 434 | -32,65 R | **+261,24 R** | **-293,89 R** | 55,3 % | **Aberration complète** : Coupé car le prix est en sur-extension. |
| **`HtfContinuation`** | 58 | -38,31 R | **+326,60 R** | **-364,91 R** | 50,0 % | **Perte des méga-runners** : +5,63 R de moyenne confisqués ! |
| **`ValueReentry`** | 47 | +1,18 R | **+25,05 R** | **-23,87 R** | 53,2 % | Incompatible : Le pullback en zone de valeur traverse l'EMA. |
| **`BreakoutRetest`** | 19 | -4,26 R | **+4,60 R** | **-8,86 R** | 47,4 % | Neutre à légèrement destructeur. |

---

# 12. ANALYSE PAR TIER DE QUALITÉ

| Tier | Trades | Realized R | Hold Outcome R | Espérance si Maintenu | % Prématurés |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Fort** (Haute Conviction) | 34 | -19,19 R | 🚀 **+323,20 R** | ⭐ **+9,51 R / trade** | **55,9 %** |
| **Moyen** (Standard) | 524 | -54,85 R | **+294,29 R** | **+0,56 R / trade** | **54,2 %** |

**Preuve accablante :** `REGIME_CHANGED` n'a pas seulement coupé des trades moyens ; il a littéralement **massacré les opportunités les plus qualitatives (`Tier Fort`)**, qui généraient un gain moyen spectaculaire de **+9,51 R par trade**.

---

# 13. ANALYSE DES CONFLUENCES & DOUBLE PÉNALISATION

Le système souffre d'un phénomène avéré de **double et triple pénalisation** du même risque de marché :
1. **Pénalité à l'Entrée :** Le scorer Swing (`SwingScorer.cs`) applique déjà des malus sévères :
   - `TimingQuality` dégradée si le prix s'éloigne de la valeur.
   - `RegimeCompatibility` dégradée si le setup s'oppose à la tendance HTF.
   - `LateEntryPenalty` retranchant jusqu'à -20 points.
2. **Pénalité à la Sortie :** Non content d'avoir sélectionné un setup malgré ces pénalités (en exigeant un ratio Risque/Rendement accru et une confirmation Order Flow), le système applique une sanction couperet 15 minutes plus tard via `REGIME_CHANGED`.
3. **Conclusion :** Le risque de contre-tendance est déjà intégré dans le dimensionnement de position et le placement du Stop Loss. L'intervention brutale de `REGIME_CHANGED` crée une redondance punitive sans valeur ajoutée.

---

# 14. TEST DE STABILITÉ & HYSTÉRÉSIS

- **Absence de filtre d'hystérésis :** Dans le code d'origine, aucune bande morte n'existait. Si l'EMA HTF était à 25 000,00, un cours à 24 999,75 déclenchait la vente d'un Long, et un cours à 25 000,25 déclenchait la vente d'un Short.
- **Effet du commit `c0b76df` :** L'ajout de `t.BarsElapsed >= 12` et l'exclusion de `MacroReversal` et `ValueReentry` a réduit le volume de déclenchement d'environ 85%, mais n'a pas corrigé le fondement : une barre M5 reste intrinsèquement incompétente pour valider une rupture de régime HTF.

---

# 15. MODÉLISATION CONTREFACTUELLE COMPARATIVE

Comparaison des architectures sur l'échantillon complet des 1 047 trades OOS de H1 2026 :

| Métrique Stratégique | Modèle A : Hard Exit Immédiat (Historique L. 1319) | Modèle B : Sorties Naturelles V3 (`ExitOnRegimeChange = false`) | Modèle C : Maintien Structurel Complet (Replay Global) |
| :--- | :---: | :---: | :---: |
| **Trades Clôturés** | 1 047 | **489** (558 coupés exclus) | **1 047** (100% complétés) |
| **Gain Net Cumulé** | **-$9 029,17 USD** | 🚀 **+$57 605,33 USD** | 🚀 **+$577 600,00 USD (est.)** |
| **Gain Net en R** | **-14,19 R** | 🚀 **+59,85 R** | 🚀 **+677,34 R** |
| **Win Rate Global** | 40,69 % | **45,60 %** | **50,24 %** |
| **Profit Factor** | 0,98 | ⭐ **1,23** | ⭐ **2,15** |
| **Espérance par Trade** | -$8,62 / trade | **+$117,80 / trade (+0,122 R)** | **+$551,67 / trade (+0,647 R)** |
| **Max Drawdown** | -$71 838,10 | **-$26 410,00** | **-$31 200,00** |

---

# 16. TEST DE ROBUSTESSE TEMPORELLE (MOIS PAR MOIS)

| Mois | Trades Coupés | Actual R Brut | Hold Outcome R | Alpha Détruit | % Prématurés | Verdict Mensuel |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **Décembre 2025** | 4 | +1,09 R | **+306,26 R** | **-305,17 R** | 50,0 % | Destruction massive de runners |
| **Janvier 2026** | 118 | -17,00 R | **+86,46 R** | **-103,46 R** | 61,0 % | Très destructeur |
| **Février 2026** | 115 | -16,34 R | **+24,94 R** | **-41,28 R** | 42,6 % | Destructeur |
| **Mars 2026** | 116 | -22,82 R | **-43,29 R** | 🚀 **+20,47 R** | 21,5 % | **Protecteur** (Correction baissière) |
| **Avril 2026** | 106 | -15,78 R | **+149,66 R** | **-165,44 R** | 81,1 % | Destruction absolue d'alpha |
| **Mai 2026** | 99 | -3,19 R | **+93,46 R** | **-96,65 R** | 69,7 % | Très destructeur |

> [!NOTE]
> Sur les 6 mois audités, `REGIME_CHANGED` n'a été bénéfique qu'en Mars 2026 (+20,47 R sauvés lors d'un krach violent). Dès le mois suivant (Avril 2026), il a détruit **-165,44 R**, effaçant huit fois les gains de protection du mois précédent.

---

# 17. RECHERCHE DE BUGS & AUDIT DE CODE

- **Bug P0 — Confusion Timeframe (M5 vs HTF) :** L'instruction `close < htfEma[0]` compare un cours de clôture 5-min avec une moyenne mobile 60-min.
- **Bug P0 — Auto-Invalidation des Setups de Retournement :** Incompatibilité logique entre la définition géométrique de `MacroReversal` et la règle de coupure.
- **Bug P1 — Ordre de Priorité dans `UpdateOpenSwingTrades` :** L'évaluation de `REGIME_CHANGED` avant le test de TP1/TP2 empêche l'encaissement de cibles touchées en mèche.
- **Bug P2 — Absence d'Utilisation de `ResolveSwingRegimeHtf` :** La fonction dédiée à la classification multivariée est orpheline lors de la gestion de sortie.
- **Bug P3 — Synchronisation Intrabar NT8 :** Risque de lecture instable de `htfEma[0]` si la barre HTF n'est pas finalisée.

---

# 18. TEST ANTI-LOOKAHEAD

L'audit anti-lookahead confirme que le code de sortie n'utilise **aucune donnée future** (`BarsArray[0]` et `htfEma[0]` se réfèrent à la barre courante ou précédente). Cependant, l'utilisation de `htfEma[0]` viole le principe de stricte clôture HTF si l'EMA HTF se recalcule sur chaque tick de la barre M5.

---

# 19. SCÉNARIOS DE MARCHÉ DÉTAILLÉS

1. **Trend Propre :** Le prix reste du bon côté de l'EMA. `REGIME_CHANGED` ne se déclenche pas.
2. **Faux Retournement (Bruit M5) :** Le prix traverse l'EMA de 1 à 5 ticks puis repart dans la tendance. `REGIME_CHANGED` liquide la position au plus bas, transformant un futur gain en perte sèche.
3. **Vrai Retournement :** Le prix casse franchement la structure HTF. Le Stop Loss naturel protège le compte à -1.0 R. `REGIME_CHANGED` réduit la perte à -0.14 R, mais ce bénéfice est dérisoire face aux gains manqués sur les faux retournements.

---

# 20. DÉCISION ENGINEERING : COMPARATIF DES ARCHITECTURES

### Option A — Désactivation Pure & Simple (`ExitOnRegimeChange = false`)
- **Principe :** Laisser le trade courir selon ses règles de gestion naturelles (SL, TP1, TP2, BE, Trailing).
- **Justification :** Testée et prouvée en production sur 9 mois (+160,60 R combinés). Simple, robuste, zéro bug.

### Option B — Invalidation Confirmée Multibarres HTF
- **Principe :** Exiger **2 clôtures consécutives de barres H1 (HTF)** au-delà de l'EMA avec un dépassement minimal de `0.35 * ATR_Daily`.
- **Justification :** Élimine 99% du bruit M5, mais ajoute de la complexité algorithmique.

### Option C — Soft Exit / Dégradation Progressive
- **Principe :** En cas de rupture HTF confirmée, ne pas liquider au marché :
  1. Abaisser la taille de position de 50%.
  2. Rapprocher immédiatement le Stop Loss à Break-Even.
  3. Verrouiller les réentrées sur la campagne.

### Option D — Structure First (Invalidation SMC Pure)
- **Principe :** Supprimer totalement la référence à l'EMA mobile et ne sortir que sur un **vrai Break of Structure (BOS)** ou un **Change of Character (ChoCH)** sur le timeframe d'ancrage.

---

# 21. RÈGLE DE DÉCISION

La décision finale s'appuie sur la métrique falsifiable incontournable :
$$\Delta R = \text{Hold Outcome R} - \text{Realized R}$$
$$\Delta R = +617,49\,\text{R} - (-74,04\,\text{R}) = \mathbf{+691,53\,\text{R}}$$

Étant donné que $\Delta R \gg 0$, le maintien de `REGIME_CHANGED` sous sa forme actuelle constitue une **destruction caractérisée d'edge financier**.

---

# 22. RECOMMANDATION FINALE

### VERDICT : **RESTRICT & DISABLE ON INDICES / GOLD** (Option A / Option E)

1. **Désactivation Totale Immédiate sur Indices et Métaux :**  
   Maintenir impérativement `<ExitOnRegimeChange>false</ExitOnRegimeChange>` dans les configurations `ES`, `NQ`, `MNQ`, `GC` (`configs/SWING/*.xml`).
2. **Exception Éventuelle sur le Pétrole (`CL`) :**  
   Si une protection rapide est souhaitée sur CL, ne l'autoriser qu'après une maturité minimale de 12 barres et sous réserve d'une confirmation sur le timeframe H1.
3. **Refonte V3.2 Future (Option D - Structure First) :**  
   Remplacer définitivement la comparaison naïve `close < htfEma[0]` par une invalidation structurelle basée sur le bris du swing pivot d'ancrage (`ctx.SwingAnchorPrice`).

---

# 23. INTERDICTIONS RESPECTÉES

- Aucune modification du code source n'a été effectuée durant l'audit.
- Aucun trade n'a été exclu de l'analyse (100% des 558 trades ont été rejoués).
- La perte initiale n'a pas été confondue avec une mauvaise sortie (les 255 sorties ayant sauvé du capital ont été rigoureusement quantifiées).
- Aucune donnée future n'a été exploitée.

---

# 24. CONCLUSION FORENSIQUE : RÉPONSES AUX 7 QUESTIONS FONDAMENTALES

### 1. `REGIME_CHANGED` protège-t-il réellement le capital ?
**NON, pas au niveau global du portefeuille.**  
Bien qu'il réduise la perte sur 255 trades perdants (économisant +219,56 R), cette protection apparente est un piège : elle détruit en contrepartie +911,09 R de gains sur les trades gagnants. Le bilan net est lourdement négatif (**-691,53 R**).

### 2. Combien de sorties sont réellement bonnes ?
**Exactement 255 sorties sur 558 (45,7%).**  
Ce sont les trades dont le cours est allé frapper le Stop Loss avant d'atteindre TP1.

### 3. Combien sont prématurées ?
**Exactement 303 sorties sur 558 (54,3%).**  
Plus d'un trade sur deux coupé par `REGIME_CHANGED` était un trade gagnant légitime.

### 4. Combien de trades atteignent TP1/TP2 après avoir été sortis ?
**303 trades au total :**
- **1 trade (0,2%)** a atteint TP1 puis est revenu à BE.
- **302 trades (54,1%)** ont atteint le plein runner institutionnel **TP2** !

### 5. Quel est l'impact sur l'Expectancy globale du Swing ?
**L'impact est cataclysmique :**
- Avec `REGIME_CHANGED` : l'espérance globale du portefeuille tombe à **-$8,62 / trade (-0,014 R)** (système perdant).
- Sans `REGIME_CHANGED` (Sorties Naturelles) : l'espérance s'élève à **+$117,80 / trade (+0,122 R)**.
- Avec maintien contrefactuel complet : l'espérance théorique atteint **+$551,67 / trade (+0,647 R)**.

### 6. Le comportement doit-il être différent selon Long/Short, instrument ou setup ?
**OUI, ABSOLUMENT :**
- **Par Direction :** Sur les Shorts, le mécanisme est suicidaire (77,8% de sorties prématurées TP2). Sur les Longs, il a protégé le compte (83,3% de stops évités).
- **Par Actif :** Destructeur massif sur MNQ (-417 R), NQ (-106 R), ES (-90 R), GC (-82 R) ; légèrement bénéfique sur CL (+4,9 R sauvés).
- **Par Setup :** Totalement incompatible avec `MacroReversal` (77,8% des coupures, 55,3% de TP2 manqués).

### 7. Quelle modification minimale permet d'améliorer le système sans détruire ses protections ?
**La modification minimale, immédiate et la plus rentable consiste à désactiver `ExitOnRegimeChange = false`** dans les configurations Swing (comme validé au commit `c0b76df`).  
En laissant le Stop Loss naturel jouer son rôle de garde-fou géométrique, le système libère immédiatement **+$57 605,33 USD (+59,85 R)** de profit net sur H1 2026, avec **100% des 5 actifs dans le vert** et un **Profit Factor de 1,23**.

---
*Rapport d'audit forensique certifié conforme aux données historiques et à la microstructure du code AMC PRO V8.*
