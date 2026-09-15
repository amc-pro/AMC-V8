# PROMPT — AMC V8 Swing V3
## Swing Opportunity Manager + Anti-Overtrading + Setup Ranking + Direction/Instrument Specialization

---

## 1. RÔLE

Tu es un **Senior Quant Developer / NinjaTrader 8 / NinjaScript / C# / Order Flow / Auction Market Theory engineer**.

Tu dois intervenir directement sur le repository :

**`amc-pro/AMC-V8`**

Branche de travail :

**`fix/swing-data-pipeline-and-null-safety`**

Objectif :

> améliorer profondément le mode **Swing** de AMC V8 afin de réduire le sur-trading intra-session, sélectionner de meilleures opportunités Swing et augmenter la qualité statistique des trades, **sans dégrader les protections existantes ni introduire d'overfitting**.

---

# 2. CONTEXTE DU PROBLÈME

Le deuxième test Swing NQ/MNQ a montré un résultat global positif mais encore faible :

- NQ : environ +21.5 R
- MNQ : environ +24.1 R
- total : environ +45.6 R
- PF global proche de 1.03
- Win Rate autour de 41 %

Mais le volume de signaux/trades est trop élevé pour un véritable mode Swing.

Le problème observé est que plusieurs bougies successives peuvent générer plusieurs signaux correspondant en réalité à **la même opportunité Swing**.

Exemple indésirable :

```text
10:00  HTF Continuation SHORT → SIGNAL
10:05  HTF Continuation SHORT → SIGNAL
10:10  HTF Continuation SHORT → SIGNAL
10:15  HTF Continuation SHORT → SIGNAL
10:20  HTF Continuation SHORT → SIGNAL
```

Le moteur doit comprendre que ces cinq signaux peuvent représenter **une seule campagne/opportunité Swing**.

---

# 3. OBJECTIF PRINCIPAL

Transformer le modèle actuel :

```text
BAR
 ↓
SETUP DETECTED
 ↓
SCORE
 ↓
SIGNAL
```

en :

```text
MARKET DATA
    ↓
DATA VALIDATION
    ↓
MARKET REGIME
    ↓
SETUP DETECTION
    ↓
ALL VALID CANDIDATES
    ↓
OPPORTUNITY MANAGER
    ↓
CAMPAIGN / DUPLICATE FILTER
    ↓
TIMING QUALITY
    ↓
SETUP × DIRECTION × INSTRUMENT FILTER
    ↓
SCORING
    ↓
CANDIDATE RANKING
    ↓
BEST SWING OPPORTUNITY
    ↓
RISK ENGINE
    ↓
TRADE LIFECYCLE
```

---

# 4. RÈGLE ABSOLUE

NE PAS transformer Swing en ScalpingPro.

Le mode Swing doit rester :

- moins fréquent ;
- plus sélectif ;
- basé sur une opportunité structurelle ;
- capable de conserver une position overnight lorsque permis ;
- sensible au changement de régime ;
- orienté vers des mouvements plus importants ;
- protégé contre le sur-trading.

Le but n'est PAS :

> maximiser le nombre de signaux.

Le but est :

> maximiser l'Expectancy, le Profit Factor et la robustesse OOS tout en réduisant les trades de faible qualité.

---

# 5. AVANT TOUTE MODIFICATION

Inspecter complètement :

- `AuctionMarketCore.Swing.cs`
- `AuctionMarketCore.Swing.Models.cs`
- `AuctionMarketCore.Sniper.cs`
- `AuctionMarketCore.Engine.cs`
- `AuctionMarketCore.Features.cs`
- `VolumeProfile`
- `SwingScorer`
- `SwingRiskManager`
- `PocMigrationAnalyzer`
- gestion du Trade Lifecycle
- SQLite Swing journal
- configuration XML Swing
- `TradingPreset`
- gestion des sessions
- News Filter
- ATR
- PointValue
- Monthly VWAP
- HTF context
- setup detection.

Identifier précisément :

1. où les setups sont générés ;
2. où `EvaluateSwingDirection()` est appelé ;
3. comment les setups sont parcourus ;
4. comment le premier setup valide est choisi ;
5. où intervient `break` ;
6. comment les trades actifs sont mémorisés ;
7. comment un nouveau signal est autorisé ;
8. comment les trades OPEN sont restaurés ;
9. comment les sessions sont détectées ;
10. comment un setup est invalidé.

NE MODIFIER LE CODE QU'APRÈS CET AUDIT.

---

# 6. CORRECTION #1 — ALL VALID CANDIDATES

Le moteur ne doit plus sélectionner immédiatement le premier setup valide.

Le comportement actuel de type :

```csharp
foreach (setup)
{
    if (valid)
    {
        execute;
        break;
    }
}
```

doit être remplacé par une logique conceptuelle :

```text
for each setup
    validate
    calculate candidate
    calculate score
    calculate quality
    add candidate

rank candidates

select best candidate

execute only best candidate
```

Créer si nécessaire :

```csharp
SwingCandidate
```

avec au minimum :

```text
Instrument
Direction
SetupType
Timestamp
StructureId
RegimeId
BaseScore
TimingQuality
RegimeCompatibility
DirectionalQuality
LocationQuality
LateEntryPenalty
ConflictPenalty
FinalQualityScore
Entry
Stop
TP1
TP2
Risk
RR
```

---

# 7. CORRECTION #2 — SWING OPPORTUNITY MANAGER

Créer un composant dédié :

```text
SwingOpportunityManager
```

ou une implémentation équivalente propre dans l'architecture existante.

Responsabilités :

- détecter les opportunités répétitives ;
- identifier une Swing Campaign ;
- empêcher les duplications ;
- gérer le verrouillage d'un setup ;
- gérer le reset ;
- gérer le cooldown ;
- gérer les nouvelles structures ;
- gérer les changements de régime.

---

# 8. SETUP SIGNATURE

Créer une signature déterministe :

```text
Instrument
+
SetupType
+
Direction
+
StructureId
+
RegimeId
+
SwingAnchor
```

Exemple :

```text
NQ|HTF_CONTINUATION|SHORT|BOS_18273|TREND_DOWN|ANCHOR_100
```

Deux candidats ayant la même signature représentent par défaut :

> la même opportunité Swing.

Donc :

```text
premier candidat → ACCEPTABLE
candidats suivants → DUPLICATE
```

---

# 9. SWING CAMPAIGN

Créer la notion :

```text
SwingCampaign
```

Une campagne représente une opportunité Swing complète.

États recommandés :

```text
IDLE
ARMED
CANDIDATE
VALIDATED
ENTERED
ACTIVE
TP1
BE
RUNNER
COMPLETED
INVALIDATED
TIMEOUT
REGIME_CHANGED
COOLDOWN
```

---

# 10. SAME-CAMPAIGN LOCK

Après une entrée :

```text
SetupType = HTF_CONTINUATION
Direction = SHORT
Campaign = ACTIVE
```

le moteur ne doit plus générer de nouvelles entrées pour cette même campagne.

Exemple :

```text
10:00 HTF Continuation SHORT score 62
→ ACCEPTED

10:05 HTF Continuation SHORT score 66
→ BLOCKED: SAME_CAMPAIGN

10:10 HTF Continuation SHORT score 71
→ BLOCKED: SAME_CAMPAIGN
```

IMPORTANT :

Un score supérieur ne doit PAS automatiquement provoquer une nouvelle entrée.

---

# 11. NOUVELLE OPPORTUNITÉ

Une nouvelle entrée peut être autorisée uniquement si au moins une condition structurelle importante apparaît :

```text
NEW_BOS
NEW_CHOCH
NEW_SWING_ANCHOR
REGIME_CHANGE
SETUP_INVALIDATED_THEN_REFORMED
CAMPAIGN_COMPLETED
TIMEOUT
```

Ne pas considérer :

```text
score +5
```

comme une nouvelle opportunité.

---

# 12. COOLDOWN

Ajouter un cooldown configurable :

```text
SwingEntryCooldownBars
```

Valeur initiale de test :

```text
12
```

sur M5.

Mais le cooldown ne doit PAS être le mécanisme principal.

Ordre de priorité :

```text
Campaign Lock
>
Setup Signature
>
Structure Reset
>
Regime Reset
>
Cooldown
```

---

# 13. LIMITES PAR SESSION

Ajouter des paramètres configurables :

```text
SwingMaxEntriesPerSession
SwingMaxLongEntriesPerSession
SwingMaxShortEntriesPerSession
```

Valeur initiale de recherche :

```text
MaxEntriesPerSession = 2
MaxLongEntriesPerSession = 1
MaxShortEntriesPerSession = 1
```

Ces valeurs sont des **valeurs de test**, pas des valeurs définitives.

Ne pas hardcoder.

---

# 14. IMPORTANT — SIGNAL VS ENTRY

Séparer :

```text
Candidate
Alert
Entry
```

Paramètres séparés :

```text
MaxCandidatesPerSession
MaxAlertsPerSession
MaxEntriesPerSession
```

Le budget critique concerne :

```text
MaxEntriesPerSession
```

---

# 15. REJECTED SIGNAL AUDIT

Chaque candidat rejeté doit pouvoir être journalisé.

Ajouter des raisons structurées :

```text
DUPLICATE_CAMPAIGN
SAME_SIGNATURE
COOLDOWN
SESSION_LIMIT
DIRECTION_LIMIT
REGIME_CONFLICT
HTF_CONFLICT
LATE_ENTRY
LOW_TIMING_QUALITY
LOW_SETUP_QUALITY
NEWS_BLOCKED
RISK_BLOCKED
INVALID_ATR_DATA
INVALID_POINT_VALUE
INVALID_CONTEXT
```

Ne jamais simplement :

```text
return;
```

sans raison lorsqu'une opportunité est rejetée.

---

# 16. TIMING QUALITY

Ajouter une couche de qualité temporelle.

Le score actuel mesure principalement la confluence.

Le nouveau modèle doit distinguer :

```text
Confluence Quality
+
Timing Quality
```

Ajouter :

```text
TimingQuality = 0..10
```

et :

```text
LateEntryPenalty = 0..15
```

Objectif :

> empêcher qu'un score très élevé obtenu trop tard dans le mouvement soit considéré comme automatiquement supérieur à une entrée plus précoce et mieux positionnée.

---

# 17. LATE ENTRY DETECTION

Développer une logique robuste permettant de détecter :

```text
EARLY
OPTIMAL
LATE
EXTENDED
```

Exemples de données utilisables :

- distance à VWAP ;
- distance à VAH/VAL/POC ;
- distance au swing anchor ;
- distance à l'ATR ;
- extension du mouvement ;
- bars depuis breakout ;
- bars depuis BOS/CHoCH ;
- delta exhaustion ;
- distance au FVG ;
- position relative dans la range.

Ne pas ajouter un indicateur arbitraire.

Utiliser les données déjà disponibles dans AMC.

---

# 18. SETUP × DIRECTION × INSTRUMENT

Implémenter une couche d'éligibilité AVANT le score.

Concept :

```text
Instrument
    ↓
Setup
    ↓
Direction
    ↓
Eligibility
    ↓
Score
```

Ne pas traiter NQ et MNQ comme des actifs strictement identiques.

Le Core reste commun.

La spécialisation peut être configurable.

---

# 19. NQ — HYPOTHÈSES À TESTER

Les résultats actuels suggèrent :

```text
HTF Continuation SHORT
→ priorité élevée

Macro Reversal SHORT
→ priorité

Breakout Retest SHORT
→ candidat à filtrer fortement

Macro Reversal LONG
→ candidat à filtrer fortement
```

IMPORTANT :

Ce ne sont PAS des règles de production.

Ce sont des hypothèses de recherche.

Les implémenter comme configuration expérimentale :

```text
NQ Setup Matrix
```

et non comme `if` hardcodés.

---

# 20. MNQ — HYPOTHÈSES À TESTER

Les résultats suggèrent :

```text
Macro Reversal LONG/SHORT
→ priorité

Value Reentry
→ candidat intéressant

HTF Continuation
→ à réévaluer

Breakout Retest SHORT
→ faible priorité
```

Le Value Reentry doit rester activé malgré son petit échantillon.

NE PAS conclure qu'il possède un edge définitif avec seulement quelques dizaines de trades.

---

# 21. HTF GATE PAR SETUP

Ne pas utiliser une seule règle HTF globale.

Proposition :

```text
HTF Continuation
→ HTF alignment HARD

Macro Reversal
→ HTF alignment SOFT

Breakout Retest
→ HTF alignment MEDIUM

Value Reentry
→ HTF alignment CONTEXTUAL
```

Rendre cette logique configurable.

---

# 22. SCORE

Ne pas simplement augmenter :

```text
SwingMinScoreToAlert
```

Le score final devrait conceptuellement être :

```text
FinalQualityScore =
    BaseScore
  + TimingQuality
  + RegimeCompatibility
  + DirectionalQuality
  + LocationQuality
  - LateEntryPenalty
  - ConflictPenalty
```

Conserver le score original pour comparaison :

```text
BaseScore
```

et ajouter :

```text
FinalQualityScore
```

Cela permettra d'étudier l'effet réel de chaque couche.

---

# 23. TIER

Ne pas utiliser le Tier comme filtre principal.

Le Tier doit rester utile pour :

- Telegram ;
- reporting ;
- classification ;
- statistiques.

Mais la décision d'entrée doit dépendre principalement de :

```text
Eligibility
+
Setup
+
Direction
+
Regime
+
Timing
+
Quality
+
Risk
```

---

# 24. POC MIGRATION

Pour NQ/MNQ :

```text
EnablePocMigration = false
```

doit rester le comportement par défaut pendant cette phase.

NE PAS réintroduire POC Migration dans l'optimisation principale.

Il sera étudié séparément.

---

# 25. ATR — ZERO TRUST

Supprimer progressivement les fallbacks financiers artificiels du type :

```text
TickSize * 10
TickSize * 40
```

Si ATR nécessaire mais invalide :

```text
NO TRADE
Reason = INVALID_ATR_DATA
```

Ne jamais fabriquer un ATR fictif pour permettre une entrée.

---

# 26. POINT VALUE — ZERO TRUST

Ne pas utiliser une valeur arbitraire de fallback pour le PointValue.

Si le PointValue est inconnu ou invalide :

```text
NO TRADE
Reason = INVALID_POINT_VALUE
```

Le système doit privilégier :

```text
capital safety
```

à :

```text
signal generation
```

---

# 27. TRADE LIFECYCLE

Vérifier le lifecycle complet :

```text
CANDIDATE
 ↓
VALIDATED
 ↓
ENTERED
 ↓
ACTIVE
 ↓
TP1
 ↓
BE
 ↓
TP2 / RUNNER
 ↓
TRAILING
 ↓
EXITED
```

avec :

```text
INVALIDATED
TIMEOUT
NEWS_BLOCKED
RISK_BLOCKED
REGIME_CHANGED
```

---

# 28. MAX HOLDING TIME

Le Swing peut conserver une position overnight lorsque configuré.

Mais il faut éviter des positions historiques restant OPEN indéfiniment.

Ajouter si absent :

```text
SwingMaxBarsInTrade
```

et/ou une logique :

```text
TIMEOUT
```

par setup.

Le timeout doit dépendre de la nature du setup lorsque possible.

Exemple conceptuel :

```text
Breakout Retest
→ durée plus courte

Macro Reversal
→ durée intermédiaire

HTF Continuation
→ durée plus longue
```

Ne pas fixer arbitrairement les valeurs finales avant analyse.

---

# 29. REGIME CHANGE

Une position Swing active doit pouvoir réagir à une rupture de régime.

Exemple :

```text
NQ SHORT
HTF Continuation
        ↓
HTF regime changes bullish
        ↓
REGIME_CHANGED
        ↓
reduce / exit / invalidate
```

Le comportement exact doit respecter le Risk Engine existant et être configurable.

Ne pas casser le mécanisme SL/TP.

---

# 30. SESSION MANAGEMENT

Le Swing ne doit pas générer une multitude d'entrées pendant une même session simplement parce que plusieurs bougies satisfont les conditions.

Implémenter :

```text
SessionStart
SessionActive
SessionEntryCount
SessionLongCount
SessionShortCount
SessionEnd
```

Reset propre à chaque nouvelle session.

Vérifier également les transitions overnight.

---

# 31. NEWS

Conserver :

```text
major news = hard block
```

si cela correspond au comportement Swing attendu.

Différencier :

```text
major
moderate
minor
```

si les données disponibles le permettent.

Ne pas remplacer un véritable calendrier économique par uniquement des heures hardcodées si une meilleure source existe déjà dans AMC.

---

# 32. ANTI-LOOKAHEAD

OBLIGATION ABSOLUE :

Aucune nouvelle fonctionnalité ne doit introduire :

- future bar data ;
- future VP ;
- future VWAP ;
- future HTF state ;
- future structure ;
- future delta ;
- future regime.

Le calcul doit utiliser uniquement les données disponibles au moment de l'évaluation.

---

# 33. IDEMPOTENCE

Un même bar évalué plusieurs fois ne doit pas générer plusieurs campagnes ou plusieurs entrées.

Exigence :

```text
same bar
+
same setup
+
same structure
=
same decision
```

Le moteur doit être idempotent.

---

# 34. PERSISTENCE

Si `SwingOpportunityManager` ou `SwingCampaign` contient un état nécessaire à la continuité :

vérifier sa persistance/restauration.

Après redémarrage NinjaTrader :

```text
ACTIVE CAMPAIGN
```

ne doit pas devenir automatiquement :

```text
NEW CAMPAIGN
```

et générer une nouvelle entrée.

---

# 35. THREAD / STATE SAFETY

Respecter les contraintes NinjaTrader.

Ne pas introduire :

- race conditions ;
- collections modifiées pendant itération ;
- état non initialisé ;
- accès invalide à BarsArray ;
- accès avant DataLoaded ;
- DateTime.MinValue converti incorrectement ;
- SQLite concurrent unsafe.

---

# 36. CONFIGURATION

Toutes les nouvelles options doivent être configurables.

Créer une section claire :

```text
SWING OPPORTUNITY MANAGEMENT
```

Exemple :

```xml
<SwingOpportunityManagement>
    <Enabled>true</Enabled>
    <SameCampaignLock>true</SameCampaignLock>
    <RequireNewStructureForReentry>true</RequireNewStructureForReentry>
    <RequireRegimeChangeForOppositeReentry>true</RequireRegimeChangeForOppositeReentry>
    <EntryCooldownBars>12</EntryCooldownBars>
    <MaxEntriesPerSession>2</MaxEntriesPerSession>
    <MaxLongEntriesPerSession>1</MaxLongEntriesPerSession>
    <MaxShortEntriesPerSession>1</MaxShortEntriesPerSession>
</SwingOpportunityManagement>
```

Adapter au système XML existant au lieu de créer une seconde architecture de configuration.

---

# 37. COMPATIBILITÉ

OBLIGATION :

Le mode :

```text
ScalpingPro
Sniper
```

ne doit pas être modifié fonctionnellement par cette correction.

Le nouveau mécanisme doit être isolé :

```text
IsSwing
```

ou architecture équivalente.

Tester explicitement :

```text
Swing
ScalpingPro
Sniper
```

---

# 38. LOGGING

Ajouter des logs structurés pour :

### ACCEPTED

```text
SWING_CANDIDATE_ACCEPTED
```

### REJECTED

```text
SWING_CANDIDATE_REJECTED
```

avec :

```text
Reason
Setup
Direction
Score
FinalQualityScore
TimingQuality
CampaignId
StructureId
Regime
```

### ENTRY

```text
SWING_ENTRY
```

### CAMPAIGN

```text
SWING_CAMPAIGN_CREATED
SWING_CAMPAIGN_ACTIVE
SWING_CAMPAIGN_INVALIDATED
SWING_CAMPAIGN_COMPLETED
SWING_CAMPAIGN_TIMEOUT
```

---

# 39. MÉTRIQUES À AJOUTER AU REPORT

Le rapport Swing doit afficher :

```text
Total candidates
Accepted candidates
Rejected candidates

Rejected:
    Duplicate
    Same Campaign
    Cooldown
    Session Limit
    Direction Limit
    Late Entry
    Regime Conflict
    HTF Conflict
    Risk Block
    Invalid Data

Campaigns
Average trades per campaign
Entries per session

Setup × Direction
Setup × Instrument
Setup × Regime
Setup × Session
Score bucket
Timing bucket
```

---

# 40. TESTS UNITAIRES OBLIGATOIRES

Créer des tests pour :

### Test 1

Même setup sur 5 bougies :

```text
1 accepted
4 rejected
```

### Test 2

Même setup + même structure :

```text
1 accepted
subsequent blocked
```

### Test 3

Nouvelle structure :

```text
new candidate accepted
```

### Test 4

Regime change :

```text
campaign invalidated
new campaign allowed
```

### Test 5

Cooldown :

```text
candidate blocked
after cooldown → allowed
```

### Test 6

Session limit :

```text
2 entries
3rd blocked
```

### Test 7

Long/Short limit :

```text
1 long
second long blocked
short still potentially allowed
```

### Test 8

Idempotence :

```text
same bar evaluated twice
→ one decision
```

### Test 9

ATR invalid :

```text
NO TRADE
INVALID_ATR_DATA
```

### Test 10

PointValue invalid :

```text
NO TRADE
INVALID_POINT_VALUE
```

### Test 11

Restart:

```text
active campaign persisted
→ no duplicate entry
```

### Test 12

ScalpingPro isolation :

```text
Swing changes
→ ScalpingPro behavior unchanged
```

---

# 41. TESTS DE RÉGRESSION

Avant de modifier les paramètres :

rejouer les tests existants.

Objectif :

```text
existing tests = PASS
```

Aucune régression tolérée sur :

- VP ;
- VWAP ;
- SMC ;
- Order Flow ;
- ATR ;
- Risk ;
- SQLite ;
- News ;
- anti-lookahead ;
- lifecycle.

---

# 42. MÉTHODOLOGIE DE PERFORMANCE

NE PAS optimiser tout en même temps.

Procéder par étapes :

## Test A

Uniquement :

```text
All Candidates
+
Best Candidate Ranking
```

---

## Test B

Ajouter :

```text
Campaign Lock
```

---

## Test C

Ajouter :

```text
Cooldown
```

---

## Test D

Ajouter :

```text
Session Entry Limits
```

---

## Test E

Ajouter :

```text
Timing Quality
+
Late Entry Penalty
```

---

## Test F

Ajouter :

```text
Setup × Direction × Instrument
```

---

## Test G

Setup-specific HTF Gate.

---

## Test H

Risk / TP uniquement après stabilisation des entrées.

---

# 43. NE PAS OPTIMISER TOUTES LES VARIABLES

Ne pas faire :

```text
MinScore
ATR
TP
SL
Cooldown
Session
HTF
VWAP
VP
SMC
```

simultanément.

Tester des groupes cohérents.

Cette approche est volontairement conforme aux recommandations de NinjaTrader concernant l'optimisation incrémentale et la prévention de l'overfitting.

---

# 44. CRITÈRES DE SUCCÈS

Une modification ne doit pas être acceptée uniquement parce que :

```text
Net Profit ↑
```

Elle doit être évaluée avec :

```text
Profit Factor
Expectancy
Avg R
Win Rate
Max Drawdown
Recovery
Trade Count
R per Session
R per Campaign
Setup stability
Long/Short stability
NQ/MNQ stability
OOS
Walk Forward
```

Objectifs de recherche :

```text
PF > 1.15
```

puis :

```text
PF > 1.25
```

avec :

```text
DD controlled
```

et surtout une amélioration qui survit au test OOS.

---

# 45. WALK-FORWARD

Après stabilisation du moteur :

```text
TRAIN
 ↓
OPTIMIZE
 ↓
TEST OOS
 ↓
MOVE WINDOW
 ↓
REPEAT
```

Ne jamais choisir les paramètres finaux uniquement sur le meilleur résultat historique.

Le Walk-Forward de NinjaTrader repose précisément sur l'optimisation d'un segment historique puis le test sur le segment temporel suivant non utilisé pour l'optimisation.

---

# 46. OOS FINAL

Réserver une période complètement indépendante :

```text
FINAL HOLDOUT
```

Aucun paramètre ne doit être ajusté à partir de cette période.

Le résultat final doit être présenté séparément.

---

# 47. RAPPORT OBLIGATOIRE

Créer :

```text
MD/SWING_V3_OPPORTUNITY_MANAGER_IMPLEMENTATION.md
```

Contenu :

1. problème initial ;
2. architecture avant ;
3. architecture après ;
4. fichiers modifiés ;
5. nouvelles classes ;
6. nouveaux paramètres ;
7. Setup Signature ;
8. Campaign logic ;
9. ranking ;
10. timing quality ;
11. session controls ;
12. tests ;
13. résultats avant/après ;
14. régressions ;
15. risques ;
16. prochaines étapes.

---

# 48. NE PAS FAIRE

Interdictions :

- ne pas ajouter des indicateurs inutiles ;
- ne pas augmenter simplement MinScore ;
- ne pas supprimer arbitrairement les pertes ;
- ne pas désactiver lundi uniquement parce qu'il est mauvais ;
- ne pas désactiver des heures uniquement sur ce dataset ;
- ne pas optimiser NQ et MNQ comme s'ils étaient identiques ;
- ne pas supprimer Value Reentry à cause de son faible nombre de trades ;
- ne pas réactiver POC Migration sur NQ/MNQ ;
- ne pas introduire de lookahead ;
- ne pas modifier ScalpingPro ;
- ne pas hardcoder les résultats du backtest ;
- ne pas créer de règles spécifiques impossibles à expliquer ;
- ne pas optimiser sur le Holdout.

---

# 49. ORDRE D'EXÉCUTION OBLIGATOIRE

Exécuter exactement dans cet ordre :

```text
1. AUDIT
   ↓
2. IMPLEMENT OPPORTUNITY MANAGER
   ↓
3. IMPLEMENT ALL-CANDIDATE RANKING
   ↓
4. IMPLEMENT CAMPAIGN LOCK
   ↓
5. IMPLEMENT STRUCTURE RESET
   ↓
6. IMPLEMENT COOLDOWN
   ↓
7. IMPLEMENT SESSION LIMIT
   ↓
8. IMPLEMENT TIMING QUALITY
   ↓
9. IMPLEMENT SETUP × DIRECTION × INSTRUMENT
   ↓
10. IMPLEMENT SETUP-SPECIFIC HTF
   ↓
11. ZERO-TRUST ATR / POINT VALUE
   ↓
12. TESTS
   ↓
13. COMPILE
   ↓
14. REGRESSION TEST
   ↓
15. BACKTEST
   ↓
16. PERFORMANCE COMPARISON
   ↓
17. OOS
   ↓
18. WALK-FORWARD
```

---

# 50. LIVRABLE FINAL

À la fin, fournir un rapport contenant obligatoirement :

### Code

```text
files modified
classes added
methods added
configuration added
```

### Architecture

```text
Before
After
```

### Performance

```text
Baseline
Test A
Test B
Test C
...
```

### Statistiques

```text
Trades
R
PF
WR
Expectancy
Max DD
Avg R
```

### Diagnostic

```text
Why trades decreased
Why PF improved or worsened
Which setup improved
Which setup degraded
Which direction improved
Which instrument improved
```

### Validation

```text
Unit Tests
Integration Tests
Regression Tests
OOS
Walk Forward
```

### Conclusion

Classer le résultat :

```text
REJECT
PROMISING
ACCEPT FOR OOS
ROBUST CANDIDATE
```

---

# 51. RÈGLE FINALE

Le résultat recherché n'est pas :

```text
plus de signaux
```

mais :

```text
moins de trades inutiles
+
meilleure sélection
+
meilleur timing
+
meilleure qualité par opportunité
+
DD inférieur
+
PF supérieur
+
robustesse OOS
```

Le principe directeur doit être :

> **Swing doit raisonner en OPPORTUNITÉS et en CAMPAGNES, pas en BOUGIES.**

Avant toute modification définitive, expliquer les changements proposés, vérifier qu'ils ne cassent pas l'architecture existante, puis implémenter progressivement.

**Ne jamais sacrifier la robustesse technique ou la validité statistique pour obtenir un meilleur backtest historique.**