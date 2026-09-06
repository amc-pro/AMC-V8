# AMC-V8 — Plan complet d’audit et d’évolution

**Périmètre :** Market Intelligence · Market Update · Rapport H4 · HVN/LVN · Swing · Scalping Pro

## 1. Objectif

Auditer l’intelligence de marché existante, la fiabiliser, puis construire une couche de décision commune permettant d’améliorer Swing et Scalping Pro sans introduire de look-ahead, de sur-optimisation ou de régression.

Architecture cible :

```text
MARKET INTELLIGENCE
        │
 ┌──────┴────────┐
 │               │
MARKET UPDATE   H4 REPORT
 └──────┬────────┘
        │
   MARKET STATE
        │
 ┌──────┼──────────────┐
 │      │              │
STRUCTURE LOCATION  VOLATILITY
 │      │              │
BOS/   HVN/LVN        ATR
CHOCH  POC/VAH/VAL    Expansion
 │      │              │
 └──────┼──────────────┘
        │
  QUALITY ENGINE
     /       \
  SWING   SCALPING PRO
     \       /
   POSITION MANAGER
```

## 2. Règles impératives

- **Audit avant modification.**
- Figer une baseline avant tout changement.
- Ne pas ajouter d’indicateurs sans preuve d’utilité.
- Aucun calcul ne doit utiliser une donnée future.
- Séparer contexte, structure, localisation, volatilité, setup, entrée et gestion.
- Market Intelligence décrit le marché ; elle ne décide pas seule BUY/SELL.
- Swing et Scalping Pro gardent leurs spécificités.
- Toute modification comportementale doit passer par A/B replay et, si possible, OOS.
- Toute métrique doit avoir une définition explicite.
- Aucun GO live sans preuve de non-régression et rollback.

---

# 3. Phase 0 — Freeze et baseline

### À faire

1. Créer une branche dédiée.
2. Noter le commit SHA de référence.
3. Sauvegarder configuration/XML actifs.
4. Hasher les datasets de replay.
5. Exporter les tests existants.
6. Produire les métriques baseline.

### Métriques

- Trades, Win Rate, Profit Factor
- Expectancy R/USD
- Net R / Net PnL
- Max Drawdown / Recovery Factor
- Avg Win / Avg Loss
- durée
- MAE / MFE

Ventiler par :

- stratégie
- setup
- direction
- instrument
- session
- régime
- tier
- heure
- mois
- exit reason

### Réconciliation obligatoire

Construire une comparaison **TradeId par TradeId** entre CSV et replay :

```text
TradeId
Entry
Exit
Direction
Setup
RealizedR
RealizedUSD
ExitReason
Duration
```

Aucune conclusion statistique ne doit être tirée tant que les métriques ne sont pas réconciliées.

---

# 4. Phase 1 — Audit Market Intelligence

## 4.1 Cartographier le code

Identifier :

- classes/services
- modèles
- calculateurs
- caches
- persistence
- sources de données
- timeframes
- consommateurs

Produire :

```text
Inputs → Calculations → State → Outputs → Persistence → Consumers
```

## 4.2 Inventorier les données

Pour chaque donnée documenter :

| Donnée | Source | TF | Fréquence | Disponibilité | Lookahead | Consommateur |
|---|---|---|---|---|---|---|
| EMA | | | | | | |
| ATR | | | | | | |
| VWAP | | | | | | |
| Structure | | | | | | |
| Profile | | | | | | |
| Regime | | | | | | |
| News | | | | | | |

## 4.3 Audit temporel

Pour chaque valeur vérifier :

- timestamp de création
- timestamp de disponibilité
- barre utilisée
- barre clôturée ou en formation
- recalcul historique
- dépendance future indirecte
- contamination du cache
- déplacement rétroactif d’un niveau

Test obligatoire :

> Rejouer jusqu’à T, puis ajouter T+1/T+2. Toutes les valeurs déjà connues à T doivent rester identiques.

---

# 5. Phase 2 — Audit Market Update

Déterminer si le système est :

- snapshot
- périodique
- événementiel
- hybride

Inventorier les événements réellement présents dans le code, par exemple :

```text
REGIME_CHANGED
STRUCTURE_CHANGED
VWAP_CHANGED
VALUE_AREA_CHANGED
HVN_CREATED
LVN_CREATED
LIQUIDITY_EVENT
VOLATILITY_CHANGED
SESSION_CHANGED
NEWS_EVENT
```

Ne rien supposer : vérifier.

## Event Contract cible

```text
EventType
Timestamp
Instrument
Timeframe
PreviousState
NewState
Confidence
Reason
SourceBar
```

Tester :

- doublons
- événements manqués
- contradictions
- ordre temporel
- idempotence
- persistence
- consommation par Swing
- consommation par Scalping Pro

---

# 6. Phase 3 — Audit Rapport H4

## Inventaire

Identifier précisément ce qui existe déjà :

```text
Direction
Trend
Regime
Structure
Momentum
Volatility
VWAP
Value Area
POC
HVN
LVN
Key Levels
Liquidity
Bias
Confidence
```

## H4 Market State

Évaluer la pertinence de :

```text
BULLISH
BEARISH
NEUTRAL
TRANSITION
EXPANSION
EXHAUSTION
BALANCE
```

Le H4 doit être un contexte, pas un filtre BUY/SELL binaire.

## H4 Confidence

Si pertinent, construire un score explicable :

```text
Confidence = f(direction, structure, momentum, volatility, location)
```

Le score doit être :

- borné
- déterministe
- décomposable
- backtestable
- non calibré sur l’OOS

---

# 7. Phase 4 — Audit HVN/LVN / Volume Profile

## 7.1 Identifier le calcul réel

Documenter :

- source du volume
- granularité
- bin size
- fenêtre
- session
- journée
- rolling profile
- profil H4
- POC
- VAH
- VAL
- HVN
- LVN

## 7.2 Stabilité

Tester l’impact de l’ajout de nouvelles barres sur :

- POC
- VAH/VAL
- HVN
- LVN

Vérifier qu’un niveau historiquement connu ne change pas à cause de données futures, sauf si le modèle définit explicitement un niveau dynamique.

## 7.3 Classification de localisation

Créer, après audit :

```text
ABOVE_VALUE
INSIDE_VALUE
BELOW_VALUE
NEAR_POC
NEAR_VAH
NEAR_VAL
NEAR_HVN
INSIDE_HVN
NEAR_LVN
CROSSING_LVN
OUTSIDE_PROFILE
UNKNOWN
```

Cette classification ne doit pas devenir automatiquement un signal.

## 7.4 Edge

Mesurer :

```text
Setup × Location
Direction × Location
Regime × Location
Session × Location
H4Bias × Location
```

---

# 8. Phase 5 — Market State unifié

Après audit uniquement.

Modèle cible :

```text
MarketState
{
    Instrument
    Timestamp
    H4State
    H1State
    LTFState
    Regime
    StructureState
    LocationState
    VolatilityState
    SessionState
    LiquidityState
    NewsState
    Bias
    Confidence
}
```

Règle fondamentale :

> MarketState décrit le marché. Il ne décide pas directement d’entrer.

---

# 9. Phase 6 — Quality Engine

États possibles :

```text
UNKNOWN
WATCH
READY
CONFIRMED
DEGRADED
INVALIDATED
```

Dimensions :

```text
Context Quality
Structure Quality
Location Quality
Volatility Quality
Timing Quality
Liquidity Quality
Risk/Reward Quality
Conflict Penalty
```

Construire un score explicable et limiter le nombre de paramètres.

Objectif : identifier les contextes à edge, pas fabriquer un système sur-paramétré.

---

# 10. Phase 7 — Intégration Swing

Swing doit exploiter prioritairement :

- H4
- H1
- structure
- localisation
- HVN/LVN
- régime
- volatilité

## HtfContinuation

Analyser :

```text
H4 alignment
H1 alignment
Location
Structure
Volatility
Tier
Direction
```

## MacroReversal

Ne pas l’invalider simplement parce qu’il est contre une EMA HTF.

Privilégier une vraie invalidation :

- cassure structurelle
- CHOCH opposé confirmé
- rupture du niveau structurel
- autre preuve validée

Principe :

```text
Regime deterioration != Structural invalidation
```

La détérioration peut protéger/dégrader ; l’invalidation structurelle peut sortir.

## Dynamic Structure

Vérifier :

- monotonicité
- déplacement uniquement favorable
- cohérence avec les swings
- absence de recul du stop
- comportement après TP1
- nouveaux swings
- gaps

---

# 11. Phase 8 — Intégration Scalping Pro

Priorités :

- microstructure
- timing
- session
- M15/M5
- VWAP
- HVN/LVN
- liquidité
- volatilité
- lifecycle du setup

Lifecycle possible :

```text
OBSERVE
→ ARM
→ TRIGGER
→ CONFIRM
→ ENTER
→ MANAGE
→ EXIT
```

Mesurer :

- MFE
- MAE
- time-to-MFE
- time-to-MAE
- excursion après entrée
- distance VWAP
- distance HVN/LVN
- distance liquidité

---

# 12. Phase 9 — No-Trade Engine

Créer une capacité explicite à répondre :

```text
NO TRADE
```

Raisons possibles après validation :

```text
LOW_CONTEXT_QUALITY
BAD_LOCATION
CONFLICTING_HTF
LOW_VOLATILITY
EXCESSIVE_VOLATILITY
NEAR_MAJOR_HVN
POOR_RR
NEWS_RISK
SESSION_UNFAVORABLE
STRUCTURE_UNCONFIRMED
DUPLICATE_SETUP
```

Chaque rejet doit être journalisé et explicable.

---

# 13. Phase 10 — Edge Matrix

Construire :

```text
Setup
× Direction
× Instrument
× Session
× H4 State
× Location
× Volatility
× Tier
```

Pour chaque cellule avec suffisamment d’observations :

- N
- WR
- PF
- Expectancy R
- Net R
- DD
- MFE
- MAE
- durée

But : identifier les contextes réellement favorables.

---

# 14. Phase 11 — Validation statistique

## A/B

```text
A = comportement actuel
B = comportement modifié
```

Comparer à données identiques.

Exiger autant que possible :

- mêmes TradeIds
- mêmes entrées
- même coût
- mêmes paramètres non concernés

## N-tests

Pour une confirmation/hystérésis :

```text
N=1
N=2
N=3
N=4
N=5
N=6
```

Mais uniquement si chaque chemin est réellement exercé.

## Counterfactual

Analyser après les sorties :

```text
+1h
+2h
+4h
+8h
+12h
+24h
```

Le counterfactual sert au diagnostic ; il ne constitue pas à lui seul une performance tradable.

## OOS

Séparer :

```text
TRAIN
VALIDATION
OUT-OF-SAMPLE
```

Ne jamais calibrer sur l’OOS.

---

# 15. Phase 12 — Tests

## Unitaires

- MarketState
- H4
- HVN/LVN
- POC/VAH/VAL
- structure
- regime
- QualityScore
- events
- hysteresis
- Dynamic Structure
- No-Trade

## Temporalité

- closed-bar only
- no future data
- replay
- invariance après ajout de données futures

## Intégration

```text
Market Intelligence → Market Update
Market Update → Swing
Market Update → Scalping Pro
H4 → Swing
HVN/LVN → Swing
HVN/LVN → Scalping
```

## Adversariaux

- gaps
- rollover
- news
- marché plat
- expansion brutale
- faux breakout
- CHOCH immédiat
- événements simultanés
- données manquantes
- événements inversés
- duplicate events

## Idempotence

Rejouer la même séquence deux fois doit donner le même état final.

---

# 16. Phase 13 — Observabilité

Chaque décision importante doit être reconstructible.

Journaliser :

```text
Timestamp
Instrument
Strategy
Setup
MarketState
H4State
Regime
Structure
Location
HVN/LVN
QualityScore
Decision
DecisionReason
RejectedReasons
RiskState
```

Exemple :

```text
Decision = NO_TRADE
Quality = 61
Reasons:
- H4 aligned
- structure confirmed
- near major HVN
- RR insufficient
```

---

# 17. Phase 14 — Performance technique

Mesurer :

- CPU
- allocations
- mémoire
- fréquence de recalcul
- cache hit rate
- durée replay
- coût Volume Profile
- coût Rapport H4

Optimiser seulement après mesure.

---

# 18. Phase 15 — Documentation

Créer/mettre à jour :

```text
MD/MARKET_INTELLIGENCE_AUDIT.md
MD/MARKET_UPDATE_AUDIT.md
MD/H4_REPORT_AUDIT.md
MD/HVN_LVN_AUDIT.md
MD/MARKET_STATE_ARCHITECTURE.md
MD/QUALITY_ENGINE.md
MD/SWING_INTEGRATION.md
MD/SCALPING_PRO_INTEGRATION.md
MD/NO_TRADE_ENGINE.md
MD/A_B_REPLAY_RESULTS.md
MD/EDGE_MATRIX.md
MD/VALIDATION_PROTOCOL.md
```

Chaque document doit distinguer :

- comportement actuel
- problème
- hypothèse
- changement
- test
- résultat
- décision

---

# 19. Ordre d’exécution

1. Freeze + baseline
2. Audit Market Intelligence
3. Audit Market Update
4. Audit H4
5. Audit HVN/LVN
6. Réconciliation historique
7. Définition MarketState
8. Implémentation minimale MarketState
9. Quality Engine
10. No-Trade Engine
11. Intégration Swing
12. Intégration Scalping Pro
13. Observabilité
14. Tests
15. A/B replay
16. OOS
17. Paper trading
18. Activation progressive

---

# 20. Critères GO / NO-GO

## GO

- baseline réconciliée
- dataset identifié et hashé
- aucun lookahead
- tests verts
- replay déterministe
- A/B correctement isolé
- nouvelles règles réellement exercées
- amélioration statistiquement crédible
- DD acceptable
- OOS confirmé
- logs suffisants
- rollback possible

## NO-GO

- dataset non réconcilié
- métriques contradictoires
- logique nouvelle jamais exercée
- lookahead possible
- amélioration uniquement in-sample
- changement non isolé
- petit échantillon insuffisant
- logique inexpliquable
- absence de rollback

---

# 21. Point critique du chantier actuel

**Avant toute optimisation supplémentaire de Swing, résoudre définitivement l’incohérence entre le CSV historique et le replay A/B.**

Le replay doit démontrer :

- identité exacte du dataset
- hash
- correspondance TradeId
- mêmes entrées/sorties
- même RealizedR
- même PnL USD
- mêmes ExitReason
- différences A/B au niveau TradeId
- nombre réel de `STRUCTURAL_REGIME_INVALIDATION`
- effet réel de N=1..6

Tant que ce point n’est pas résolu, aucune conclusion sur l’edge de Regime Invalidation ne doit justifier une activation live.

---

# 22. Principe directeur

Le système doit chercher à :

> reconnaître les contextes dans lesquels les setups ont historiquement un avantage, éviter les contextes médiocres et adapter la gestion lorsque le contexte se détériore.

Priorité :

```text
MEILLEURE INFORMATION
        ↓
MEILLEUR CONTEXTE
        ↓
MEILLEURE SÉLECTIVITÉ
        ↓
MEILLEUR TIMING
        ↓
MEILLEURE GESTION
        ↓
VALIDATION STATISTIQUE
```

Éviter :

```text
INDICATEUR → FILTRE → FILTRE → PARAMÈTRE → SUR-OPTIMISATION
```

---

# 23. Checklist finale

- [ ] Baseline figée
- [ ] Dataset hashé
- [ ] TradeId réconciliés
- [ ] Market Intelligence audité
- [ ] Market Update audité
- [ ] H4 audité
- [ ] HVN/LVN audité
- [ ] Lookahead testé
- [ ] MarketState défini
- [ ] Quality Engine défini
- [ ] No-Trade Engine défini
- [ ] Swing intégré
- [ ] Scalping Pro intégré
- [ ] Dynamic Structure validé
- [ ] Regime ≠ Structure respecté
- [ ] Tests unitaires
- [ ] Tests intégration
- [ ] Tests adversariaux
- [ ] Replay déterministe
- [ ] A/B validé
- [ ] OOS validé
- [ ] Edge Matrix produite
- [ ] Observabilité complète
- [ ] Documentation à jour
- [ ] Rollback testé
- [ ] GO/NO-GO
