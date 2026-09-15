# Plan d’implémentation — Current Monthly VWAP Band Retest

**Projet :** `AuctionMarketCore`  
**Branche cible recommandée :** `feat/auction-market-current-monthly-vwap-retest`  
**Statut initial :** à implémenter en mode Shadow/Simulation  
**Setup proposé :** `CurrentMonthlyVwapBandRetest`

> **Objectif :** détecter les retests confirmés de la bande `Monthly SD+1` en tendance haussière pour générer un candidat **LONG**, et les retests confirmés de la bande `Monthly SD-1` en tendance baissière pour générer un candidat **SHORT**, en utilisant le VWAP et les bandes du mois courant, donc des niveaux dynamiques.

## 1. Règle fonctionnelle

Le setup ne doit jamais se déclencher sur un simple contact intrabar. Il doit distinguer une acceptation au-delà de la bande, un retest et une clôture de confirmation.

### 1.1. Signal Long

Un candidat Long peut être créé lorsque les conditions suivantes sont satisfaites :

1. La tendance HTF est haussière.
2. Le prix est au-dessus du `CurrentMonthlyVwap`.
3. Le prix a précédemment clôturé au-dessus de `CurrentMonthlySd1Upper`.
4. La barre de retest touche ou pénètre légèrement la bande SD+1 dans une tolérance définie.
5. La barre clôture au-dessus de SD+1.
6. La bougie de confirmation est haussière ou présente un rejet haussier valide.
7. Le niveau n’a pas déjà été retesté au-delà du nombre maximal autorisé.
8. Les conditions news, gap et volatilité sont acceptables.
9. Le stop, la cible et le ratio rendement/risque sont valides.
10. Aucune position Long Swing équivalente n’est déjà active.

```text
LONG si :
TrendHTF = BULLISH
AND Close_Previous > CurrentMonthlySd1Upper_Previous
AND Low_Current <= CurrentMonthlySd1Upper_Current + RetestTolerance
AND Close_Current > CurrentMonthlySd1Upper_Current
AND Close_Current > Open_Current
AND MonthlyVwapSlope >= MinMonthlyVwapSlopeLong
AND NewsBlock = false
AND GapFilter = valid
AND RiskReward >= MinRiskReward
```

### 1.2. Signal Short

Un candidat Short peut être créé lorsque les conditions suivantes sont satisfaites :

1. La tendance HTF est baissière.
2. Le prix est sous le `CurrentMonthlyVwap`.
3. Le prix a précédemment clôturé sous `CurrentMonthlySd1Lower`.
4. La barre de retest touche ou pénètre légèrement la bande SD-1.
5. La barre clôture sous SD-1.
6. La bougie de confirmation est baissière ou présente un rejet baissier valide.
7. Le niveau n’a pas été invalidé ou excessivement retesté.
8. Les conditions news, gap et volatilité sont acceptables.
9. Le stop, la cible et le ratio rendement/risque sont valides.
10. Aucune position Short Swing équivalente n’est déjà active.

```text
SHORT si :
TrendHTF = BEARISH
AND Close_Previous < CurrentMonthlySd1Lower_Previous
AND High_Current >= CurrentMonthlySd1Lower_Current - RetestTolerance
AND Close_Current < CurrentMonthlySd1Lower_Current
AND Close_Current < Open_Current
AND MonthlyVwapSlope <= MaxMonthlyVwapSlopeShort
AND NewsBlock = false
AND GapFilter = valid
AND RiskReward >= MinRiskReward
```

Le terme à utiliser dans le code et les journaux est `SHORT`, et non `SELL LONG`.

## 2. Gestion du VWAP Monthly courant

Le VWAP Monthly courant évolue pendant le mois. Cette caractéristique doit être traitée explicitement afin d’éviter des décisions ambiguës et des résultats de backtest trompeurs.

Le calcul doit être effectué uniquement à partir des données disponibles jusqu’à la barre évaluée. Il ne faut jamais utiliser la clôture du mois futur ni un profil mensuel finalisé pour simuler une décision antérieure.

Les valeurs à ajouter au contexte Swing sont :

```text
CurrentMonthlyVwap
CurrentMonthlySd1Upper
CurrentMonthlySd1Lower
CurrentMonthlySd2Upper
CurrentMonthlySd2Lower
CurrentMonthlyVwapSlope
CurrentMonthlyVwapSessionStartUtc
CurrentMonthlyVwapLastUpdateUtc
```

Le contexte doit aussi préciser si le niveau est valide et calculable :

```text
HasCurrentMonthlyVwap
HasCurrentMonthlyBands
CurrentMonthlyDataBars
```

Si les données sont insuffisantes, le setup doit être bloqué et journalisé avec la raison `MONTHLY_VWAP_DATA_INSUFFICIENT`.

## 3. Snapshot du retest

Même si les bandes restent dynamiques, le moteur doit conserver un snapshot lorsqu’un candidat est créé. Cela permet de comparer le résultat avec le niveau réellement observé au moment du signal.

```text
MonthlyVwapAtSetup
MonthlySd1UpperAtSetup
MonthlySd1LowerAtSetup
MonthlyVwapSlopeAtSetup
RetestDistanceTicks
SetupDetectedBarIndex
SetupDetectedTimeUtc
```

Le snapshot doit être utilisé pour l’audit, le journal et l’analyse postérieure. Le moteur doit préciser si le stop et les objectifs utilisent la bande dynamique au moment de l’entrée ou le snapshot du signal.

## 4. Tolérance et confirmation

La tolérance de retest ne doit pas être uniquement fixe pour tous les instruments. Elle devrait être configurable en ticks et plafonnée par une fraction de l’ATR :

```text
RetestToleranceTicks = min(ConfiguredToleranceTicks,
                           ATRCurrent / TickSize × MaxRetestAtrFraction)
```

Paramètres recommandés à rendre configurables :

| Paramètre | Rôle |
|---|---|
| `EnableCurrentMonthlyBandRetest` | Active ou désactive le setup |
| `MonthlyBandRetestToleranceTicks` | Tolérance autour de SD±1 |
| `MonthlyBandMinAcceptanceBars` | Nombre minimal de clôtures au-delà de la bande |
| `MonthlyBandMaxRetests` | Nombre maximal de retests acceptés |
| `MonthlyBandMinVwapSlope` | Pente minimale pour un Long |
| `MonthlyBandMaxVwapSlope` | Pente maximale admissible pour un Short |
| `MonthlyBandMaxEntryDriftAtr` | Distance maximale entre entrée et bande |
| `MonthlyBandRequireDeltaConfirmation` | Exige une confirmation de delta |
| `MonthlyBandRequireHtfAlignment` | Exige l’alignement HTF |

## 5. Invalidation du setup

Un candidat doit être invalidé dans les cas suivants :

- clôture confirmée du mauvais côté de la bande ;
- retour durable sous le VWAP Monthly pour un Long ;
- retour durable au-dessus du VWAP Monthly pour un Short ;
- pente du VWAP qui s’inverse fortement ;
- nombre maximal de retests dépassé ;
- expiration après `ValidityBarsMax` ;
- news de haute sévérité ;
- gap supérieur au seuil instrument ;
- stop supérieur à `MaxStopTicks` ;
- cible adverse trop proche pour respecter `MinRiskReward` ;
- données Monthly invalides ou réinitialisées.

Chaque invalidation doit être journalisée avec un code stable, par exemple :

```text
MONTHLY_BAND_CROSSED
MONTHLY_VWAP_REGIME_LOST
MONTHLY_RETEST_LIMIT_REACHED
MONTHLY_DATA_RESET
MONTHLY_RISK_REJECTED
```

## 6. Stop, objectifs et sizing

Le stop doit combiner structure et volatilité :

- Long : sous le creux du retest et/ou sous SD+1 avec buffer ;
- Short : au-dessus du sommet du retest et/ou au-dessus de SD-1 avec buffer ;
- choix de la distance protectrice selon la politique existante du `SwingRiskManager` ;
- bornage obligatoire par `MinStopTicks` et `MaxStopTicks`.

Les cibles peuvent utiliser :

1. le VWAP Monthly courant ;
2. SD+2 ou SD-2 ;
3. un POC/HVN Weekly ou Monthly ;
4. une cible `R` configurée ;
5. une sortie partielle à TP1 puis une sortie finale à TP2.

Le système doit rejeter le signal si la première cible raisonnable est trop proche pour satisfaire `MinRiskReward`.

Le sizing doit conserver la formule existante basée sur la valeur réelle du tick :

```text
Risque par contrat =
    (StopDistanceTicks + ExecutionCostTicks) × TickValue

Contracts = floor(RiskPerTradeCurrency / RisqueParContrat)
```

Le risque calculé doit être plafonné par `MaxContracts` et journalisé avec le prix du tick et la valeur du point de l’instrument.

## 7. Intégration dans AuctionMarketCore

### 7.1. Modèles

Ajouter un type dédié :

```csharp
MonthlyVwapBandRetest = 5
```

Ajouter au `SwingContext` les champs Monthly courants, leur pente, leur validité et leur timestamp. Ajouter au `SwingSignal` les champs de snapshot et le type de bande utilisé.

### 7.2. Scorer

Ajouter une méthode isolée :

```csharp
bool ValidateCurrentMonthlyBandRetest(
    SwingContext context,
    SwingDirection direction,
    out string rejectionReason);
```

Le score doit séparer :

- qualité de la tendance HTF ;
- distance et qualité du retest ;
- pente du VWAP Monthly ;
- confirmation de clôture ;
- Volume Profile ;
- Order Flow ;
- risque/rendement ;
- pénalités news/gap/volatilité.

Ne pas donner au setup un accès direct non contrôlé aux positions ni à l’exécution. Il doit produire un signal conforme au contrat Swing existant.

### 7.3. Cycle de vie

Le setup doit être exécuté uniquement lorsque :

- `IsSwing` est vrai ;
- `EnableSwingEngine` est actif ;
- la barre évaluée est clôturée ;
- les données Monthly sont disponibles ;
- le setup n’a pas déjà été évalué sur le même index.

L’intégration ne doit pas modifier le comportement des setups ScalpingPro.

### 7.4. Journalisation Telegram et CSV

Chaque alerte doit identifier sans ambiguïté le setup :

```text
🚨 SWING LONG
Setup: CurrentMonthlyVwapBandRetest
Band: Monthly_SD1_UPPER
Trend: HTF_BULLISH
Mode: SHADOW / SIMULATION
MonthlyVWAP: ...
MonthlySD1: ...
RetestDistanceTicks: ...
Score: .../100
Stop: ...
TP1: ...
TP2: ...
Risk: ...
```

Pour un Short :

```text
Setup: CurrentMonthlyVwapBandRetest
Band: Monthly_SD1_LOWER
Trend: HTF_BEARISH
Mode: SHADOW / SIMULATION
```

## 8. Tests obligatoires

### 8.1. Tests de calcul

- Calcul correct du VWAP Monthly cumulatif.
- Calcul correct de SD+1 et SD-1.
- Réinitialisation exacte au début d’un nouveau mois.
- Absence de données futures.
- Comportement lorsque le mois contient peu de barres.
- Gestion des données manquantes et des sessions incomplètes.

### 8.2. Tests de signaux

- Long valide après acceptation au-dessus de SD+1 puis retest confirmé.
- Short valide après acceptation sous SD-1 puis retest confirmé.
- Contact sans clôture de confirmation rejeté.
- Cassure directe sans retest rejetée.
- Tendance HTF opposée rejetée ou pénalisée selon la configuration.
- VWAP plat ou en inversion rejeté.
- Plusieurs retests au-delà de la limite rejetés.
- Niveau Monthly réinitialisé correctement au changement de mois.

### 8.3. Tests de risque

- Stop sous le retest Long.
- Stop au-dessus du retest Short.
- Respect de `MinStopTicks` et `MaxStopTicks`.
- Sizing ES/MES/NQ/MNQ/GC/MGC/CL/MCL.
- Rejet d’un R/R insuffisant.
- Impact des coûts d’exécution.
- Gap et news sévère.
- TP1 partiel, break-even et TP2.

### 8.4. Tests d’intégration

- Exécution au bon moment dans `OnBarUpdate`.
- Compatibilité `BarsInProgress`.
- Isolation ScalpingPro.
- Journal CSV et Telegram clairement identifiés `SWING`.
- Persistance du snapshot.
- Reprise après redémarrage.
- Aucun doublon après recalcul ou reconnexion.

## 9. Validation empirique

Le setup doit d’abord être activé en Shadow uniquement. Il faut comparer au minimum :

| Groupe | Description |
|---|---|
| A | Moteur Swing actuel sans Monthly Band Retest |
| B | Moteur actuel + filtre Monthly Band Retest |
| C | Monthly Band Retest seul |

Les métriques à journaliser sont :

- nombre de candidats ;
- nombre de signaux validés ;
- taux d’atteinte de TP1 ;
- taux d’atteinte de TP2 ;
- excursion adverse maximale ;
- excursion favorable maximale ;
- durée de détention ;
- résultat en R et en devise ;
- slippage théorique ;
- performance par instrument et par sens ;
- performance selon la pente du VWAP ;
- performance selon la distance du retest ;
- performance avec et sans confirmation delta.

Aucun taux de réussite ne doit être annoncé avant une séparation stricte entre période d’apprentissage, validation et données hors échantillon.

## 10. Ordre d’exécution recommandé

| Phase | Livrable | Critère de sortie |
|---:|---|---|
| 1 | Audit des données Monthly existantes | Sources, unités et reset mensuel documentés |
| 2 | Calcul VWAP/SD Monthly courant | Tests mathématiques passants |
| 3 | Ajout du contexte et du snapshot | Aucun mélange avec Daily clôturé |
| 4 | Détecteur Long/Short | Tests de retest et invalidation passants |
| 5 | Intégration au scorer Swing | Aucun impact ScalpingPro |
| 6 | Gestion stop/TP/sizing | Scénarios multi-instruments passants |
| 7 | Journalisation | Messages Telegram et CSV identifiables |
| 8 | Tests d’intégration | Cycle NT8 testé ou explicitement limité |
| 9 | Shadow Replay | Résultats archivés hors échantillon |
| 10 | Revue de passage | Autorisation ou rejet de l’étape suivante |

## 11. Critères d’acceptation

L’implémentation est acceptable pour Shadow si :

- les bandes du Monthly courant sont calculées sans look-ahead ;
- les signaux Long/Short nécessitent un vrai retest confirmé ;
- le niveau et la pente sont journalisés ;
- le setup est clairement identifié dans Telegram et CSV ;
- les stops et targets sont bornés ;
- le sizing utilise le TickValue réel ;
- l’état est persistant ;
- les tests négatifs sont passants ;
- aucune régression ScalpingPro n’est détectée.

L’implémentation n’est pas acceptable pour le live si l’un des éléments suivants manque :

- compilation et chargement NinjaTrader 8 ;
- réconciliation avec les positions réelles ;
- gestion des ordres, rejets et remplissages partiels ;
- gestion déconnexion/reconnexion ;
- validation Market Replay ;
- résultats hors échantillon ;
- limites de risque par instrument et par compte.

## Conclusion

Le setup `CurrentMonthlyVwapBandRetest` est une extension cohérente du moteur Swing. Son avantage principal est d’utiliser un niveau Monthly dynamique qui peut accompagner une tendance multi-jour. Son principal risque est l’instabilité du VWAP et des bandes pendant le mois.

La priorité est donc de séparer clairement les données Monthly courantes des profils Daily ou Monthly clôturés, de confirmer le retest sur barre clôturée, de conserver un snapshot du niveau et de commencer en mode Shadow. Le setup ne doit pas être considéré comme rentable ou prêt pour le réel avant une validation hors échantillon et une intégration NinjaTrader complète.
