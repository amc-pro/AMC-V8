# Prompt d’implémentation — Système Swing pour AuctionMarketCore

Tu es un architecte logiciel senior spécialisé en C#, NinjaTrader 8, Auction Market Theory, Volume Profile, Order Flow, gestion du risque et validation quantitative. Tu dois auditer puis implémenter un système de trading **Swing** dans le dépôt `amc-pro/AMC-V8`, actuellement renommé `AuctionMarketCore`.

## 1. Contexte obligatoire

Le projet actuel contient un moteur NinjaTrader 8 organisé autour d’une classe partielle `AuctionMarketCore` répartie dans plusieurs fichiers :

- `AuctionMarketCore.cs` : classe racine et intégration NinjaTrader ;
- `AuctionMarketCore.Engine.cs` : flux, delta, CVD et microstructure ;
- `AuctionMarketCore.Features.cs` : footprint, absorption et patterns ;
- `AuctionMarketCore.VolumeProfile.cs` : profils de volume, persistance SQLite et alertes ;
- `AuctionMarketCore.MarketIntelligence.cs` : contexte de marché ;
- `AuctionMarketCore.ScalpingPro.cs` : logique spécifique ScalpingPro ;
- `AuctionMarketCore.Sniper.cs` : pipeline N1-N4, gates, scoring, stops et journal Shadow ;
- `AuctionMarketCore.Render.cs`, `Network.cs` et `Exports.cs` : rendu, réseau et exports.

Le seul jeu de configurations actif est actuellement `configs/SCALPING_PRO/`, avec huit instruments : `MNQ`, `NQ`, `ES`, `MES`, `GC`, `MGC`, `CL` et `MCL`. Le projet utilise notamment des profils Volume Profile clôturés, VWAP et bandes SD, SQLite, un stop ATR, un filtre anti-empilement, un contexte news/HTF et un journal Shadow.

Le système Swing doit être une **extension isolée et explicitement identifiable**. Il ne doit pas dégrader, modifier silencieusement ou mélanger le comportement existant de ScalpingPro.

## 2. Règle absolue : commencer par un audit du dépôt

Avant d’écrire du code, inspecte le dépôt et produis un diagnostic factuel comprenant :

1. l’architecture des classes partielles, les points d’entrée NinjaTrader et le cycle `OnStateChange` / `OnBarUpdate` / `OnMarketData` ;
2. les séries temporelles et `BarsInProgress` actuellement utilisées ;
3. les mécanismes existants de Volume Profile, VWAP, SD, structure, delta, scoring, news, HTF, stops et gestion des positions ;
4. les propriétés publiques sérialisées par NinjaTrader et leur compatibilité XML ;
5. les usages de SQLite, journaux, Telegram, exports et état persistant ;
6. les dépendances entre `AuctionMarketCore.cs`, `AuctionMarketCore.Sniper.cs` et `AuctionMarketCore.ScalpingPro.cs` ;
7. les risques de mélange entre la logique Swing et la logique ScalpingPro ;
8. les tests existants, leurs limites et les parties impossibles à valider sans NinjaTrader 8.

Ne déduis rien sans preuve. Pour chaque conclusion, indique le fichier, le symbole et les lignes concernées. Si une hypothèse est nécessaire, marque-la explicitement comme **À CONFIRMER**.

## 3. Architecture cible à respecter

Conçois une architecture Swing séparée de ScalpingPro. Ne recrée pas les anciens presets supprimés `SCALPING`, `SNIPER`, `SCANNER` ou `STANDARD`.

Privilégie une séparation claire avec des fichiers dédiés, par exemple :

- `AuctionMarketCore.Swing.cs` pour la logique Swing ;
- `AuctionMarketCore.Swing.Models.cs` ou des types internes dédiés pour les signaux ;
- `configs/SWING/` pour les configurations Swing par instrument ;
- des paramètres clairement préfixés ou regroupés sous une section `Swing` ;
- une séparation explicite entre les signaux `SCALPING_PRO` et `SWING`.

Avant de choisir entre un mode intégré et un module séparé, compare les deux options et justifie la décision selon : isolation des états, sérialisation NinjaTrader, risques de régression, maintenance, tests et compatibilité avec les XML existants.

Le comportement existant de ScalpingPro doit rester identique lorsqu’il est chargé avec ses configurations actuelles. Aucun changement de seuil, gate, stop, score, news ou ordre ne doit être introduit implicitement par l’ajout du Swing.

## 4. Définition fonctionnelle du Swing

Implémente un système Swing qui travaille exclusivement sur des barres clôturées et des références confirmées. Le système doit exploiter, selon la disponibilité réelle des données :

- les profils Volume Profile clôturés journaliers, hebdomadaires et mensuels ;
- POC, VAH, VAL, VWAP clôturé et bandes SD ±1, ±2, ±3 ;
- HVN, LVN et zones de faible ou forte acceptation ;
- structure de marché multi-unité : tendance, range, break of structure et changement de caractère ;
- zones FVG, imbalance, liquidité et niveaux de réintégration ;
- delta, CVD, absorption et confirmation d’initiative ;
- contexte HTF sur 4H, journalier et hebdomadaire si les séries sont disponibles ;
- régime de marché : tendance, équilibre, expansion, compression et transition ;
- calendrier et fenêtres de news importantes ;
- rollover, gaps, sessions CME, jours fériés et données incomplètes.

Le système ne doit pas prendre une position simplement parce qu’un prix touche une bande SD ou un POC. Il doit distinguer au minimum :

1. **rejet d’un extrême statistique** ;
2. **acceptation au-delà d’un niveau** ;
3. **réintégration de Value Area** ;
4. **breakout confirmé avec retest** ;
5. **retournement macro avec divergence delta/CVD** ;
6. **continuation après pullback vers FVG, HVN, LVN ou VWAP clôturé**.

## 5. Modèle de signal et scoring

Crée un modèle de signal Swing explicite, traçable et déterministe. Le score ne doit pas être une simple somme opaque. Il doit exposer au minimum :

- contexte de régime et tendance HTF ;
- qualité de la localisation Auction Market ;
- confirmation Volume Profile ;
- confirmation structurelle ;
- confirmation Order Flow ;
- qualité du déclencheur ;
- risque de gap ou de news ;
- distance jusqu’à la prochaine zone adverse ;
- qualité du ratio rendement/risque ;
- pénalités de volatilité excessive, liquidité insuffisante et données incomplètes.

Définis des catégories lisibles, par exemple `REJECT_EXTREME`, `VALUE_REENTRY`, `BREAKOUT_RETEST`, `MACRO_REVERSAL` et `HTF_CONTINUATION`, mais n’ajoute aucune catégorie sans expliquer ses conditions d’entrée, ses invalidations et ses sorties.

Pour chaque signal, journalise :

- instrument, date, heure et fuseau ;
- unité de temps et `BarsInProgress` ;
- contexte HTF ;
- niveaux VP/VWAP/SD utilisés ;
- score détaillé par composant ;
- conditions de validation et conditions rejetées ;
- entrée théorique, stop, objectifs et distance en ticks ;
- slippage et buffer supposés ;
- statut final : `CANDIDATE`, `VALIDATED`, `BLOCKED`, `EXPIRED`, `ENTERED`, `EXITED`.

## 6. Gestion du risque Swing

Le risque Swing doit être conçu spécifiquement pour une exposition de plusieurs heures à plusieurs jours. Ne réutilise pas aveuglément les paramètres ScalpingPro.

Implémente et documente :

- taille de position calculée à partir du risque monétaire, du tick value, de la distance du stop et d’un buffer de slippage ;
- limites `MinStopTicks` et `MaxStopTicks` obligatoires dans chaque XML Swing ;
- stop structurel combiné à un stop ATR, avec règle explicite de priorité ;
- protection contre les stops irréalistes lors des gaps ;
- limite de risque par trade, par instrument, par corrélation et par journée ;
- limite du nombre de positions Swing simultanées ;
- anti-stacking par instrument, direction et famille de signal ;
- exposition maximale overnight et pendant les annonces majeures ;
- règles de réduction de taille avant news, rollover et liquidité dégradée ;
- perte maximale quotidienne et verrouillage après dépassement ;
- gestion des erreurs de prix, ticks invalides, données absentes et instruments non tradables.

Le calcul du risque doit utiliser les propriétés instrument réelles de NinjaTrader et ne doit jamais supposer qu’un tick vaut la même chose pour `NQ`, `ES`, `GC` et `CL`.

## 7. Entrées, sorties et gestion de position

Définis précisément :

- la condition de pré-signal ;
- le déclencheur d’entrée sur barre clôturée ;
- l’invalidation avant entrée ;
- le stop initial ;
- les objectifs `TP1`, `TP2` et la sortie finale ;
- les sorties partielles ;
- le passage éventuel au break-even ;
- le trailing basé sur structure, ATR ou nouveaux profils clôturés ;
- le time stop ;
- l’invalidation par changement de régime ;
- le comportement après redémarrage ou rechargement de NinjaTrader ;
- la réconciliation entre l’état interne et les positions réellement ouvertes.

Le système doit être idempotent : un redémarrage, un recalcul ou un événement répété ne doit pas provoquer une nouvelle entrée dupliquée.

Ne déplace jamais un stop dans le sens qui augmente le risque initial, sauf règle explicitement justifiée et testée. Documente toutes les règles de modification du stop.

## 8. Anti-lookahead et multi-timeframe

Garantis formellement qu’aucune donnée future ou barre non clôturée n’est utilisée :

- pas de lecture illégitime de la barre `[0]` pour prendre une décision confirmée ;
- contrôle strict de `BarsInProgress` ;
- synchronisation claire des séries 5m, 15m, 1H, 4H, daily et weekly ;
- aucune utilisation d’un profil journalier ou hebdomadaire avant sa clôture réelle ;
- gestion correcte des trous, changements de session, fuseaux, DST et jours fériés ;
- tests spécifiques des frontières de session et des changements de date.

Ajoute des assertions ou logs de diagnostic en mode développement afin de détecter toute lecture anticipée.

## 9. News, gaps et exposition overnight

Le Swing doit traiter les événements macro différemment du ScalpingPro. Implémente une politique documentée comprenant :

- blocage, réduction de taille ou pénalité autour des news selon leur importance ;
- fuseau unique et explicite pour les horaires news ;
- test des changements d’heure été/hiver ;
- gestion des gaps d’ouverture et des gaps entre sessions ;
- décision explicite sur la conservation overnight et durant le week-end ;
- expiration ou recalcul du signal après un gap significatif ;
- protection contre les stops théoriques qui ne peuvent pas être exécutés au prix prévu.

Ne présente pas `NewsHardBlock = false` comme une garantie de sécurité. Explique le comportement réel pour chaque score et chaque catégorie de news.

## 10. Configurations et compatibilité

Crée des configurations Swing explicites et complètes. Chaque fichier doit contenir au minimum :

- instrument et unité de temps ;
- paramètres HTF ;
- paramètres VP/VWAP/SD ;
- seuils par famille de setup ;
- `RiskPerTradeCurrency` ;
- `RiskAtrPeriod` ;
- `StopAtrMultiple` ;
- `MinStopTicks` ;
- `MaxStopTicks` ;
- objectifs et règles de trailing ;
- limites overnight et news ;
- fuseau horaire ;
- identifiant de stratégie et version de schéma.

Valide par script que tous les champs obligatoires sont présents dans chaque XML Swing, que les valeurs sont cohérentes avec l’instrument et que les XML ScalpingPro existants restent inchangés.

## 11. Tests obligatoires

Ajoute ou étends les tests afin de couvrir au minimum :

1. anti-lookahead multi-timeframe ;
2. calcul déterministe des profils clôturés ;
3. POC, VAH, VAL, VWAP et bandes SD ;
4. détection des régimes ;
5. rejet d’extrême et réintégration de Value Area ;
6. breakout/retest ;
7. validation structure + Order Flow ;
8. calcul du stop structurel/ATR ;
9. taille de position par tick value ;
10. respect de `MinStopTicks` et `MaxStopTicks` ;
11. anti-stacking ;
12. idempotence après redémarrage ;
13. news et fuseaux horaires ;
14. gaps, rollover et sessions CME ;
15. sorties partielles et trailing ;
16. absence de régression ScalpingPro ;
17. parsing des configurations Swing ;
18. synchronisation du script de déploiement NinjaTrader ;
19. sécurité des chemins, logs et secrets ;
20. absence de code mort ou de références aux anciens presets.

Pour chaque test, indique la donnée d’entrée, le résultat attendu, le résultat observé et le hash du commit testé. Ne déclare pas `100 % PASS` sans fournir la sortie complète et reproductible.

## 12. Validation NinjaTrader 8

Distingue clairement :

- tests unitaires reproductibles hors NinjaTrader ;
- analyse statique ;
- compilation réelle NinjaTrader 8 ;
- Market Replay ;
- simulation multi-session ;
- validation visuelle du rendu ;
- validation du comportement des ordres et de la réconciliation des positions.

Le module Swing ne doit pas être déclaré prêt pour compte réel tant que la compilation NinjaTrader, les tests Market Replay et la vérification des ordres simulés n’ont pas été effectués.

## 13. Nettoyage et qualité du code

Pendant l’implémentation :

- supprime les branches mortes et méthodes devenues inutiles ;
- supprime les paramètres orphelins et imports inutilisés ;
- retire les anciens noms, anciens presets et commentaires obsolètes ;
- conserve les commentaires qui expliquent une invariance, une règle de risque, une contrainte NinjaTrader ou une décision mathématique ;
- n’ajoute pas de duplication de calcul déjà disponible dans `VolumeProfile` ;
- évite les états globaux mutables non nécessaires ;
- utilise des noms cohérents `AuctionMarketCore` et `Swing` ;
- documente toute compatibilité temporaire et sa date de retrait.

## 14. Livrables obligatoires

À la fin, fournis :

1. un audit initial de l’architecture existante ;
2. une proposition d’architecture Swing comparant les options possibles ;
3. la liste des fichiers créés, modifiés, renommés et supprimés ;
4. le code complet et compilable dans l’environnement cible ;
5. les configurations XML Swing ;
6. les tests ajoutés avec résultats reproductibles ;
7. le journal des changements de schéma et de configuration ;
8. un tableau des risques résiduels ;
9. un rapport séparant ce qui est prouvé de ce qui reste à valider ;
10. un guide de déploiement NinjaTrader 8 ;
11. un guide de Market Replay ;
12. une procédure de rollback ;
13. un résumé des différences entre ScalpingPro et Swing ;
14. un verdict parmi `NO-GO`, `GO SIMULATION` ou `GO CONDITIONNEL`.

## 15. Règles de travail et Git

Travaille dans une nouvelle branche dédiée, par exemple :

```bash
git checkout -b feat/auction-market-swing
```

Procède par commits logiques et séparés :

1. audit et architecture ;
2. modèles et configuration ;
3. logique Swing ;
4. gestion du risque ;
5. tests ;
6. documentation et nettoyage.

Avant chaque commit :

```bash
git diff --check
git status
git grep -n "AuctionMarketScalpingPro\|SniperMarketCorePro"
```

Ne pousse rien qui introduit des secrets, des tokens, des chemins absolus propres à une machine ou des artefacts de build. À la fin, pousse la branche et fournis son nom, le SHA de chaque commit et le lien GitHub.

## 16. Format de la réponse attendue

Réponds dans cet ordre :

1. **Audit initial** ;
2. **Risques et dépendances** ;
3. **Architecture Swing retenue et justification** ;
4. **Plan d’implémentation** ;
5. **Modifications réalisées** ;
6. **Tests et preuves** ;
7. **Limites de validation** ;
8. **Instructions de déploiement** ;
9. **Verdict de readiness**.

Ne prétends jamais qu’une stratégie est rentable, sûre ou prête pour le compte réel sur la seule base de tests unitaires. Les performances doivent être évaluées séparément sur des données hors échantillon, avec coûts, slippage, gaps, commissions, liquidité et règles d’exécution réalistes.
