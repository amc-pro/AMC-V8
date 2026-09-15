# AMC PRO V8 — HTF Trend Logic

## Objectif

Le calcul HTF ne doit pas dépendre d’un simple croisement prix/EMA ni donner le même poids à H4, H1, M15 et M5.

## Nouvelle classification d’une tendance

Chaque timeframe est évalué uniquement sur des bougies clôturées :

- position du dernier close confirmé par rapport à l’EMA ;
- pente de l’EMA sur deux intervalles ;
- momentum du close sur deux intervalles ;
- seuil minimal de distance prix/EMA ;
- seuil minimal de pente EMA.

Une tendance est Bullish/Bearish seulement si les trois conditions directionnelles sont cohérentes. Sinon : Neutral.

## Hiérarchie MTF

| Timeframe | Rôle | Poids |
|---|---|---:|
| H4 | Régime principal | 40% |
| H1 | Confirmation | 30% |
| M15 | Contexte d’exécution | 20% |
| M5 | Trigger fin | 10% |

M15/M5 ne peuvent donc pas annuler à eux seuls un régime H4 sain.

## Biais global

- H4 directionnel + H1 opposé → `NO TRADE`.
- H4 directionnel + H1 neutre → biais H4 conservé, confiance réduite par l’alignement.
- H4 neutre + H1 directionnel → H1 devient la référence.
- H4 et H1 neutres → `NO TRADE`.
- Un conflit M15/M5 ne change pas le biais global ; il réduit l’alignement et doit être traité comme contexte d’exécution.

## Sécurité

Un timeframe absent ou pas encore suffisamment alimenté n’est jamais considéré comme aligné. Le filtre H1/M15 retourne `false` si les données HTF sont indisponibles ou insuffisantes.

## Paramètres par défaut

- EMA : 21
- distance minimale prix/EMA : 0,50 tick
- pente minimale EMA : 0,10 tick/barre

Ces valeurs sont des valeurs de départ et doivent être calibrées par instrument/régime via replay/backtest, sans optimisation excessive.
