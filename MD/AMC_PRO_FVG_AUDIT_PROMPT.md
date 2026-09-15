# PROMPT — Audit complet & amélioration du moteur SMC FVG d’AMC PRO

## Objectif

Réaliser un **audit technique complet, factuel et orienté production** de toute la partie **SMC — Fair Value Gap (FVG)** du repository **AMC-V8**, notamment sur la branche de travail Scalping Pro.

Le but n’est pas de créer immédiatement un nouveau moteur FVG. Il faut d’abord :

1. retrouver précisément toute la logique FVG existante ;
2. cartographier son architecture ;
3. identifier bugs, incohérences et limitations ;
4. vérifier le lifecycle complet des zones ;
5. vérifier l’absence de look-ahead/data leakage ;
6. analyser l’intégration avec SMC, Order Flow, Delta, Footprint, Volume Profile, VWAP et HTF ;
7. vérifier l’intégration N1/N2/N3/N4 et les gates ;
8. proposer les corrections prioritaires ;
9. préparer un plan d’implémentation sans casser les setups existants.

---

# 1. Règles absolues

### Ne rien supposer

Pour chaque fonctionnalité FVG, retrouver le **code réel** :

- fichier exact ;
- classe ;
- méthode ;
- variables ;
- appels entrants/sortants ;
- dépendances.

Si une fonctionnalité n’est pas démontrée par le code, écrire :

> **NOT IMPLEMENTED / NOT VERIFIED**

Ne rien inventer.

### Audit avant modification

Ne modifier aucun fichier pendant la phase d’audit.

Le rapport doit précéder toute modification.

---

# 2. Cartographie complète

Reconstituer le pipeline réel :

```text
Market Data
    ↓
FVG Detection
    ↓
FVG Classification
    ↓
Zone Creation
    ↓
Storage / Registry
    ↓
Zone Update
    ↓
Retest
    ↓
Mitigation
    ↓
Invalidation / Expiration
    ↓
SMC / Order Flow Confluence
    ↓
Candidate Discovery
    ↓
N1 / N2 / N3 / N4
    ↓
ScalpingPro Gates
    ↓
Final Signal
```

Pour chaque étape :

| Élément | À fournir |
|---|---|
| Fichier | chemin exact |
| Classe | nom exact |
| Méthode | nom exact |
| Entrées | données utilisées |
| Sorties | données produites |
| État | persistant/recalculé |
| Appels | méthodes appelées |
| Risque | P0/P1/P2/P3 |

---

# 3. Détection FVG

Identifier la définition réellement utilisée.

Vérifier notamment :

### Bullish

```text
Candle 1 High < Candle 3 Low
```

### Bearish

```text
Candle 1 Low > Candle 3 High
```

Ne pas imposer ces formules si le projet utilise volontairement une autre définition : documenter alors la logique réelle.

Vérifier :

- bougies clôturées ;
- indexation NinjaTrader ;
- `CurrentBar` ;
- `evalOffset` ;
- séries insuffisantes ;
- session boundaries ;
- tick size ;
- MNQ/NQ ;
- bougie en formation.

---

# 4. Look-ahead / Data Leakage — P0

Déterminer si un FVG affiché à l’instant T aurait réellement été connu par AMC à T.

Auditer :

- `evalOffset` ;
- `High[0]` / `Low[0]` ;
- bougie en formation ;
- `Calculate.OnEachTick` ;
- `Calculate.OnBarClose` ;
- historique ;
- Replay ;
- Shadow Mode.

Créer un test anti-look-ahead.

Verdict obligatoire :

```text
PASS / FAIL / PARTIAL
```

---

# 5. Structure d’une zone FVG

Lister les propriétés réellement stockées.

Vérifier au minimum :

```text
Id
Direction
CreatedTime
CreatedBar
Low
High
Midpoint
Size
Age
Active
Retested
Mitigated
Invalidated
Expired
FillPercent
```

Distinguer clairement :

```text
Detected
Active
Touched
Retested
PartiallyFilled
50PercentFilled
FullyFilled
Invalidated
Expired
Consumed
```

Si ces états n’existent pas, le signaler.

---

# 6. Lifecycle

Auditer :

```text
CREATE
  ↓
ACTIVE
  ↓
RETEST
  ↓
MITIGATION
  ↓
FULL FILL
  ↓
INVALIDATION / EXPIRATION
```

Déterminer exactement :

- quand la zone est créée ;
- comment un retest est détecté ;
- wick vs close ;
- premier contact vs contacts multiples ;
- partial fill ;
- 50 % / CE ;
- full fill ;
- invalidation ;
- expiration.

**Purge ≠ mitigation** : vérifier que ces notions ne sont pas confondues.

---

# 7. Storage / Purge / Trimming

Identifier :

- collection/list/dictionary ;
- limite maximale ;
- purge ;
- trimming ;
- ordre des opérations.

Vérifier particulièrement :

> Les zones expirées sont-elles supprimées avant le trimming ?

Tester un scénario avec :

```text
zones expirées
+
zones actives
+
nouvelle zone
```

Objectif : aucune suppression prématurée d’une zone valide.

---

# 8. Anti-duplicate

Vérifier qu’une même condition ne crée pas :

```text
FVG BUY
FVG BUY
FVG BUY
```

pour une même zone.

Auditer :

- ID ;
- timestamp ;
- bar index ;
- direction ;
- prix ;
- signaux répétés sur plusieurs ticks.

Tester spécifiquement en `OnEachTick`.

---

# 9. Qualité d’un FVG

Vérifier si le moteur distingue les FVG selon :

```text
Gap Size
Displacement
Volume
Delta
ZDelta
CVD
ATR
Relative Volume
Body Size
Wick Ratio
Session
HTF
```

Si aucun quality engine n’existe, proposer une architecture sans l’implémenter immédiatement.

Classer idéalement :

```text
LOW
MEDIUM
HIGH
INSTITUTIONAL
```

---

# 10. FVG + SMC

Vérifier l’interaction réelle avec :

```text
BOS
CHOCH
Market Structure
Liquidity Sweep
Liquidity Grab
Order Block
Breaker
Premium / Discount
Equal High
Equal Low
```

Tester notamment :

```text
Bullish BOS + Bullish FVG
Bearish BOS + Bearish FVG
```

et les conflits :

```text
HTF Bearish + LTF Bullish FVG
HTF Bullish + LTF Bearish FVG
```

Documenter la règle réelle.

---

# 11. FVG + Order Block

Vérifier si AMC détecte une confluence :

```text
FVG + Order Block overlap
```

Déterminer si cette confluence :

- augmente réellement le score ;
- crée un candidat ;
- confirme un trigger ;
- ou n’est pas utilisée.

Détecter tout double comptage.

---

# 12. FVG + Order Flow

Auditer les interactions avec :

```text
Delta
ZDelta
Delta Flip
Cumulative Delta
CVD Divergence
Absorption
Imbalance
Exhaustion
Footprint
Volume
```

Tester conceptuellement un LONG :

```text
Bullish FVG
↓
Retest
↓
Selling pressure weakens
↓
Delta positive
↓
ZDelta positive
↓
Absorption
↓
LONG candidate
```

Déterminer si ce pipeline existe réellement.

---

# 13. FVG + Volume Profile

Vérifier :

```text
FVG + POC
FVG + VAH
FVG + VAL
FVG + HVN
FVG + LVN
```

Déterminer si le contexte `inside value / outside value / VAH / VAL / LVN` influence le score ou le trigger.

---

# 14. FVG + VWAP

Vérifier :

```text
Bullish FVG + Price above VWAP
Bearish FVG + Price below VWAP
```

Documenter si VWAP :

- confirme ;
- pénalise ;
- bloque ;
- n’intervient pas.

---

# 15. Multi-Timeframe

Identifier les timeframes réellement utilisés.

Séparer :

```text
HTF FVG
LTF FVG
```

Tester :

```text
HTF bullish FVG + LTF bullish FVG
```

versus :

```text
HTF bearish FVG + LTF bullish FVG
```

Auditer :

- synchronisation ;
- timestamp ;
- propagation ;
- look-ahead ;
- données futures.

---

# 16. FVG → Candidate Discovery

Question critique :

> Le FVG crée-t-il directement un candidat ou sert-il uniquement de feature/confluence ?

Tracer exactement :

```text
FVG
 ↓
Candidate ?
 ↓
SetupType
 ↓
Direction
 ↓
N1/N2/N3/N4
```

Identifier toutes les méthodes qui transforment une condition FVG en signal.

---

# 17. Scoring

Construire une matrice :

| Feature | N1 | N2 | N3 | N4 | Trigger | Penalty |
|---|---:|---:|---:|---:|---:|---:|
| FVG | ? | ? | ? | ? | ? | ? |
| BOS | ? | ? | ? | ? | ? | ? |
| Delta | ? | ? | ? | ? | ? | ? |
| ZDelta | ? | ? | ? | ? | ? | ? |
| Order Block | ? | ? | ? | ? | ? | ? |

Détecter :

- double comptage ;
- bonus redondants ;
- contradictions ;
- score artificiellement gonflé ;
- score artificiellement pénalisé.

---

# 18. Scalping Pro Gates

Vérifier particulièrement :

```text
DELTA_FLIP
CUM_DELTA_DIV
BREAKOUT
N3
N4
HTF
ATR
Risk/RR
```

Question essentielle :

> Un FVG de haute qualité peut-il être rejeté uniquement parce qu’une gate N3/N4 non pertinente est insuffisante ?

Comparer cette logique avec les gates spécialisées déjà présentes pour les setups Order Flow/Momentum.

---

# 19. Shadow Mode

Vérifier que Shadow Mode journalise :

```text
FVG CREATED
timestamp
direction
low
high
midpoint
quality
HTF
BOS
OB
Delta
ZDelta
CVD
status
```

Puis :

```text
FVG RETEST
FVG MITIGATED
FVG INVALIDATED
FVG EXPIRED
```

Et finalement :

```text
Candidate Created
Candidate Accepted
Candidate Rejected
Reject Reason
```

Vérifier que Shadow Mode et Live Engine utilisent la même temporalité.

---

# 20. Tests historiques obligatoires

Utiliser les données MNQ déjà utilisées dans le projet.

### Test A — LONG

```text
MNQ
19 août 2026
15H25–15H35
```

Analyser :

```text
FVG
+
Delta reversal
+
ZDelta
+
Candidate LONG
```

### Test B — SHORT

```text
MNQ
19 août 2026
16H30–16H40
```

Analyser :

```text
FVG
+
Delta
+
Candidate SHORT
```

### Test C

FVG retest + partial fill.

### Test D

FVG 50 % mitigation.

### Test E

Full fill / invalidation.

### Test F

Duplicate detection sous plusieurs ticks.

### Test G

Look-ahead / replay.

### Test H

FVG proche d’une session boundary.

---

# 21. Critères d’acceptation

## Détection

- [ ] Bullish FVG correct
- [ ] Bearish FVG correct
- [ ] aucune donnée future
- [ ] `evalOffset` respecté

## Lifecycle

- [ ] création
- [ ] retest
- [ ] partial mitigation
- [ ] 50 % mitigation
- [ ] full fill
- [ ] invalidation
- [ ] expiration

## Storage

- [ ] purge correcte
- [ ] trimming correct
- [ ] aucune suppression prématurée
- [ ] anti-duplicate

## Confluence

- [ ] BOS/CHOCH
- [ ] Order Block
- [ ] Delta
- [ ] ZDelta
- [ ] CVD
- [ ] Absorption
- [ ] Footprint
- [ ] Volume Profile
- [ ] VWAP
- [ ] HTF

## Strategy

- [ ] FVG ne déclenche pas aveuglément un trade
- [ ] FVG peut renforcer un setup valide
- [ ] pas de double comptage
- [ ] FVG haute qualité pas injustement bloqué par une gate non pertinente

## Shadow Mode

- [ ] CREATED
- [ ] RETEST
- [ ] MITIGATED
- [ ] INVALIDATED
- [ ] EXPIRED
- [ ] Candidate / Reject reason

---

# 22. Livrable final

Produire un rapport avec :

## A. Executive Summary

- état actuel ;
- forces ;
- problèmes critiques ;
- recommandation.

## B. Architecture actuelle

Diagramme réel du pipeline.

## C. Cartographie du code

| File | Class | Method | Responsibility | Risk |
|---|---|---|---|---|

## D. Bugs

Classer :

```text
P0 Critical
P1 High
P2 Medium
P3 Low
```

## E. Look-Ahead Audit

```text
PASS / FAIL / PARTIAL
```

## F. Lifecycle Audit

```text
PASS / FAIL / PARTIAL
```

## G. Confluence Audit

```text
PASS / FAIL / PARTIAL
```

## H. Scoring/Gates Audit

Identifier doubles comptages et contradictions.

## I. Historical Tests

Présenter les résultats des cas MNQ.

## J. Plan de correction

Pour chaque correction :

```text
Priority
File
Method
Problem
Correction
Risk
Test
Acceptance Criteria
```

## K. Architecture cible

Proposer, si nécessaire :

```text
FVG Detector
      ↓
FVG Registry
      ↓
FVG Lifecycle Manager
      ↓
FVG Quality Engine
      ↓
SMC Confluence Engine
      ↓
Order Flow Confirmation
      ↓
Candidate Discovery
      ↓
ScalpingPro Gates
```

## L. Plan d’implémentation

```text
Phase 1 — Audit / instrumentation
Phase 2 — P0 fixes
Phase 3 — Lifecycle
Phase 4 — Quality
Phase 5 — SMC confluence
Phase 6 — Order Flow confluence
Phase 7 — Shadow Mode
Phase 8 — Historical validation
Phase 9 — Live validation
```

---

# 23. Contraintes de sécurité du projet

Ne pas transformer immédiatement le FVG en nouveau modèle de stratégie.

Préserver :

```text
DELTA_FLIP
CUM_DELTA_DIV
BREAKOUT
Volume Profile
VWAP
Footprint
N1/N2/N3/N4
Shadow Mode
Market Intelligence
Market Report
```

Toute correction doit être :

```text
minimal
isolated
testable
rollbackable
```

Ne modifier les seuils ou gates que si l’audit démontre précisément le problème.

---

# 24. Verdict final obligatoire

Terminer par :

```text
FVG ENGINE STATUS

Detection:        🟢 / 🟡 / 🔴
Lifecycle:        🟢 / 🟡 / 🔴
Mitigation:       🟢 / 🟡 / 🔴
Invalidation:     🟢 / 🟡 / 🔴
Expiration:       🟢 / 🟡 / 🔴
Anti-duplicate:   🟢 / 🟡 / 🔴
Anti-lookahead:   🟢 / 🟡 / 🔴
SMC Confluence:   🟢 / 🟡 / 🔴
Order Flow:       🟢 / 🟡 / 🔴
MTF:              🟢 / 🟡 / 🔴
Scoring:          🟢 / 🟡 / 🔴
Gates:            🟢 / 🟡 / 🔴
Shadow Mode:      🟢 / 🟡 / 🔴

OVERALL:
🟢 Production Ready
🟡 Needs Corrections
🔴 Critical Redesign Required
```

Puis donner :

> **Les 5 corrections les plus importantes à implémenter en priorité.**

Ne conclure qu’après avoir cité les fichiers, classes et méthodes réellement inspectés.
