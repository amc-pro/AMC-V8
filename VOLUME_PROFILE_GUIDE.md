# Guide Complet : Volume Profile, VP LOC & VP CONF (AMC PRO V8.0)

Ce document détaille le fonctionnement mathématique, algorithmique et opérationnel du module **Volume Profile (Closed References)** intégré dans **SniperMarketCorePro V8.0**.

---

## Sommaire
1. [Architecture & Principes Fondamentaux](#1-architecture--principes-fondamentaux)
2. [Anatomie des Références Clôturées (P.DAY, P.WEEK, P.MONTH)](#2-anatomie-des-références-clôturées-pday-pweek-pmonth)
3. [Détection des Nœuds de Volume (HVN & LVN)](#3-détection-des-nœuds-de-volume-hvn--lvn)
4. [Fonctionnement de `VP LOC` (Volume Profile Location)](#4-fonctionnement-de-vp-loc-volume-profile-location)
5. [Fonctionnement de `VP CONF` (Volume Profile Confluence)](#5-fonctionnement-de-vp-conf-volume-profile-confluence)
6. [Machine à États & Cycle de Vie des Zones](#6-machine-à-états--cycle-de-vie-des-zones)
7. [Playbooks & Applications Pratiques en Trading](#7-playbooks--applications-pratiques-en-trading)
8. [Paramètres Configurables dans NinjaTrader 8](#8-paramètres-configurables-dans-ninjatrader-8)

---

## 1. Architecture & Principes Fondamentaux

Le moteur Volume Profile d'AMC PRO V8.0 repose sur une règle stricte : **Zéro Look-Ahead Bias**.

```
                           FLUX VOLUMÉTRIQUE (Ticks / Barres)
                                          │
                                          ▼
                      ┌────────────────────────────────────────┐
                      │   Accumulateurs en direct (Non exposés)│
                      │      - Jour en cours                   │
                      │      - Semaine en cours                │
                      │      - Mois en cours                   │
                      └──────────────────┬─────────────────────┘
                                         │
                         Clôture de Session Déterministe
                                         │
                                         ▼
                      ┌────────────────────────────────────────┐
                      │   Profils Clôturés Immuables           │
                      │   (PrevDay, PrevWeek, PrevMonth)       │
                      └───────┬────────────────────────┬───────┘
                              │                        │
                              ▼                        ▼
                   ┌──────────────────────┐  ┌───────────────────┐
                   │ Persistance SQLite   │  │ VolumeProfile-    │
                   │ amc_volume_profile.db│  │ Analyzer          │
                   └──────────────────────┘  └─────────┬─────────┘
                                                       │
                                            ┌──────────┴──────────┐
                                            ▼                     ▼
                                         VP LOC                VP CONF
```

* **Accumulateurs en cours** : Enregistrent chaque tick de volume pendant la session active sans jamais être utilisés pour biaiser les signaux.
* **Profils Clôturés (Closed References)** : Dès qu'une session (RTH/Jour, Semaine, Mois) se termine, le profil est finalisé, lissé, enrichi de ses nœuds (HVN/LVN) et stocké dans la base SQLite locale.
* **Stabilité absolue** : Les niveaux de référence affichés au dashboard restent fixes tout au long de la journée, offrant des points de repère institutionnels stables.

---

## 2. Anatomie des Références Clôturées (JOUR PRÉ, SEM PRÉ, MOIS PRÉ)

Sur le panneau du dashboard :
```text
VOLUME PROFILE — RÉFÉRENCES CLÔTURÉES
JOUR PRÉ: VAH 30225,50 | POC 30100,00 | VAL 30063,25
SEM PRÉ : VAH 30269,25 | POC 30200,00 | VAL 30093,25
```

### Définitions des Métriques :
1. **POC (*Point of Control*)** :
   * Le niveau de prix exact où le volume échangé a été le plus élevé sur l'ensemble de la période.
   * C'est le prix de "juste valeur" (*fair value*) ou d'accord maximal entre acheteurs et vendeurs.
2. **VAH (*Value Area High*) & VAL (*Value Area Low*)** :
   * Bornes qui encadrent **70% du volume total** échangé pendant la période (calcul dual directionnel à partir du POC).
   * **Value Area (VA)** = Zone d'acceptation du prix.
   * **Hors de la Value Area** = Zone de rejet ou de déséquilibre (*imbalance*).

---

## 3. Détection des Nœuds de Volume (HVN & LVN)

Pour les périodes hebdomadaires (`SEM PRÉ`) et mensuelles (`MOIS PRÉ`), le système identifie mathématiquement les zones clés par **lissage Gaussien** et **calcul de proéminence relative**.

```text
S.LVN   : 29912,50-30023,00 (Pic 29924,75)
```

```
Volume
  ▲
  │        /\             /\  <-- HVN (High Volume Node) : Zone d'acceptation / Aimant
  │       /  \   /\      /  \
  │──────/────\_/──\────/────\────── Volume Moyen
  │            \    \  /
  │             \____\/  <-- LVN (Low Volume Node) : Zone de vide / Rejet / Accélération
  └───────────────────────────────────► Prix
```

### Algorithme de Détection :
1. **Lissage Gaussien** : Applique un filtre Gaussien ($\sigma = 2.5\text{ ticks}$) pour éliminer le bruit micro-structurel des ticks individuels.
2. **HVN (*High Volume Node*)** :
   * Sommet local avec ratio de volume $\ge 1.35 \times \text{Volume Moyen}$.
   * **Propriété Marché** : Agit comme un **aimant** (*zone de consolidation/support/résistance*).
3. **LVN (*Low Volume Node*)** :
   * Creux local avec ratio de volume $\le 0.65 \times \text{Volume Moyen}$.
   * **Propriété Marché** : Représente un **vide de liquidité**. Le prix traverse généralement cette zone très rapidement (*slippage/impulsion*) ou la rejette violemment.
4. **Calcul de la Zone** : L'algorithme étend la zone autour du pic (`PeakPrice`) de `ZoneLow` à `ZoneHigh` jusqu'à ce que le volume dépasse le seuil moyen.

---

## 4. Fonctionnement de `VP LOC` (Volume Profile Location)

`VP LOC` décrit la **position relative instantanée du prix** par rapport aux structures de volume établies.

### 4.1. Position Structurelle Primaire (par rapport au Jour Précédent)

| Valeur affichée | Condition Mathématique | État du Marché | Biais Théorique |
| :--- | :--- | :--- | :--- |
| **`AU-DESSUS VA JOUR PRÉC`** | $\text{Prix} > \text{PrevDay.VAH}$ | **Déséquilibre Haussier (*Imbalance Long*)** | Les acheteurs acceptent des prix plus chers qu'hier. Biais haussier actif. |
| **`DANS VA JOUR PRÉC`** | $\text{PrevDay.VAL} \le \text{Prix} \le \text{PrevDay.VAH}$ | **Équilibre / Range (*Rotational Market*)** | Le marché accepte la même valeur qu'hier. Stratégie de rotation VAH $\leftrightarrow$ VAL. |
| **`SOUS VA JOUR PRÉC`** | $\text{Prix} < \text{PrevDay.VAL}$ | **Déséquilibre Baissier (*Imbalance Short*)** | Les vendeurs poussent le prix sous la valeur d'hier. Biais baissier actif. |

### 4.2. Étiquettes de Proximité des Niveaux Majeurs (Tolérance : $\pm 3\text{ ticks}$)

Lorsque le prix s'approche d'un niveau institutionnel :
* **`[PROCHE POC JOUR]`** : Prix à $\le 3\text{ ticks}$ du POC d'hier.
* **`[PROCHE VAH JOUR]`** : Prix à $\le 3\text{ ticks}$ du VAH d'hier.
* **`[PROCHE VAL JOUR]`** : Prix à $\le 3\text{ ticks}$ du VAL d'hier.
* **`[PROCHE POC SEMAINE]`** : Prix à $\le 3\text{ ticks}$ du POC de la semaine passée.

### 4.3. Étiquettes d'Immersion dans les Nœuds Hebdo / Mensuels

* **`[DANS HVN SEMAINE]`** / **`[DANS HVN MOIS]`** : Le prix évolue à l'intérieur d'un nœud à fort volume.
* **`[DANS LVN SEMAINE]`** / **`[DANS LVN MOIS]`** : Le prix est entré dans un nœud à faible liquidité (zone d'accélération).
* **`[PROCHE LVN SEMAINE]`** / **`[PROCHE HVN SEMAINE]`** : Le prix est à moins de $4\text{ ticks}$ de la frontière du nœud.

---

## 5. Fonctionnement de `VP CONF` (Volume Profile Confluence)

`VP CONF` signale une **zone de confluence institutionnelle multi-temporelle**.

```text
VP CONF : CONFLUENCE x7 [HVN Sem Préc #6 + VAH Jour Préc + POC Sem Préc + HVN Sem Préc #7 + HVN Sem Préc #8 + HVN Sem Préc #9 + HVN Sem Préc #10]
```

```
VAH Jour Préc    ─────────────► 30225.50 ┐
POC Sem Préc     ─────────────► 30224.75 ┼───► ZONE DE CONFLUENCE (±4 ticks)
HVN Sem Préc #6  ─────────────► 30226.00 ┘     ==> "MUR" DE LIQUIDITÉ x3
```

### Algorithme de Détection :
1. **Agrégation des Niveaux** : Le moteur collecte tous les niveaux clôturés actifs :
   * Jour : POC, VAH, VAL
   * Semaine : POC, VAH, VAL, Nœuds HVN, Nœuds LVN
   * Mois : POC, VAH, VAL, Nœuds HVN, Nœuds LVN
2. **Clustering Spatial** : Il regroupe les niveaux dont l'écart est $\le 4\text{ ticks}$ (`ConfluenceToleranceTicks`).
3. **Filtre Multi-Timeframe** : Une confluence n'est validée que si elle réunit **au moins 2 horizons temporels différents** (ex: Jour + Semaine, Semaine + Mois).
4. **Format de Restitution** :
   * `CONFLUENCE xN` : `N` représente le nombre total de niveaux convergents.
   * `[...]` : Liste exhaustive des niveaux qui composent le cluster en français.

### Pourquoi la Confluence est Cruciale :
Un niveau isolé (ex: simple VAH journalier) peut être facilement franchi. En revanche, lorsque le **VAH journalier coïncide avec le POC hebdomadaire et un HVN mensuel**, plusieurs catégories d'acteurs institutionnels (day traders, swing traders, algos de couverture) ont leurs ordres passifs positionnés sur la même zone. Elle devient un **mur de liquidité majeur**.

---

## 6. Machine à États & Cycle de Vie des Zones

Le moteur suit l'historique d'interaction du flux de prix avec chaque zone :

```
                  ┌─────────────┐
                  │  UNTOUCHED  │ (Zone vierge créée)
                  └──────┬──────┘
                         │ Premier contact (TouchCount = 1)
                         ▼
                  ┌─────────────┐
                  │   TESTED    │ (Zone en cours de test)
                  └──┬───┬───┬──┘
    Rejet franc avec │   │   │ Clôtures répétées à l'intérieur
    mèche + Delta    │   │   │ (AcceptanceCount >= 2)
                     │   │   ▼
                     │   │ ┌──────────────┐
                     │   │ │   ACCEPTED   │ (Zone absorbée / perd son impact)
                     │   │ └──────────────┘
                     ▼   │
     ┌──────────────┐    │ Traversée sans réaction
     │   REJECTED   │    │ (distTicks > 2x tolérance)
     └──────────────┘    ▼
                   ┌─────────────┐
                   │   BROKEN    │ (Zone cassée / invalidée)
                   └─────────────┘
```

* **Score de force (`StrengthScore` de 0 à 100)** :
  * Augmente de **+10** lors d'un rejet confirmé avec delta opposé.
  * Diminue de **-15** si le prix s'y installe (*acceptation*).
  * Diminue de **-30** en cas de cassure nette (*breakout*).

---

## 7. Playbooks & Applications Pratiques en Trading

### Scénario A : Déséquilibre Baissier (`BELOW PREV DAY VA`)
* **Contexte** : Le prix ouvre ou s'échappe sous le `VAL` d'hier.
* **Stratégie** :
  * Chercher des ventes sur pullback vers le `PrevDay VAL` (qui fait désormais office de résistance).
  * Ne pas chercher d'achats tant que le prix ne réintègre pas avec force l'intérieur de la Value Area d'hier.

### Scénario B : Rebond sur Confluence Majeure (`VP CONF x3+`)
* **Contexte** : Le prix recule vers une zone où `VP CONF` affiche `x3` ou plus (ex: PrevWeek POC + PrevDay VAL + PrevWeek HVN).
* **Stratégie** :
  * Surveiller la réaction du carnet d'ordres / delta au contact de la zone.
  * Prendre un trade de contre-tendance ou de rebond (*Fade trade*).
  * Placer le Stop Loss juste derrière la borne extrême de la zone de confluence (protection par le mur d'ordres).

### Scénario C : Traversée d'un LVN (`W.LVN`)
* **Contexte** : Le prix pénètre dans la zone d'un LVN (ex: `29912.50 - 30023.00`).
* **Stratégie** :
  * **Ne jamais placer de Take Profit ni de Stop Loss au beau milieu d'un LVN**.
  * Prévoir une accélération vive du prix à travers le LVN jusqu'au prochain HVN.
  * Trade de *Breakout / Momentum* à l'entrée du LVN avec cible sur la sortie du nœud.

---

## 8. Système d'Alertes Telegram des Niveaux & Structures VP

Le module intègre un moteur d'alertes temps réel connecté à **Telegram** utilisant **le même canal et la même configuration que Market Intelligence** (`MiTelegramChannel` / Canal 3 avec repli automatique sur Canal 1).

### Types d'Alertes Émises :
1. **🔔 Test de Confluence / Premier Test de Niveau Majeur** :
   * Déclenché lors du premier contact avec une confluence institutionnelle (`x2+`) ou un niveau majeur clôturé (`POC`, `VAH`, `VAL`).
   * *Anti-spam* : Cooldown paramétrable (15 min par défaut) par niveau pour éviter tout spam pendant les consolidations.
2. **🛡️ Rejet de Zone Confirmé** :
   * Déclenché lorsque le marché teste un niveau et forme une mèche de rejet immédiate avec un delta opposé dans le sens du rejet.
   * Fournit l'invalidation structurelle pour un trade de rebond/fade.
3. **⚡ Entrée en Zone d'Accélération (LVN)** :
   * Déclenché lorsque le prix pénètre dans un nœud à faible liquidité (*Low Volume Node*).
   * Alerte le trader sur le risque de glissement/accélération rapide.

---

## 9. Paramètres Configurables dans NinjaTrader 8

Les paramètres se configurent dans la section **15. Volume Profile V2 (Closed References)** de l'indicateur :

| Paramètre | Valeur par défaut | Description |
| :--- | :--- | :--- |
| **Activer Volume Profile V2** | `true` | Active le moteur de calcul et l'affichage dashboard. |
| **Activer Persistance SQLite** | `true` | Sauvegarde et recharge l'historique dans `amc_volume_profile.db`. |
| **Tolérance Niveaux (Ticks)** | `3` | Distance max pour déclencher les étiquettes `[PROCHE POC/VAH/VAL]`. |
| **Tolérance Nodes HVN/LVN (Ticks)** | `4` | Distance max pour la détection de proximité des nœuds. |
| **Chemin Base SQLite** | `""` (Automatique) | Emplacement de la base (par défaut : `Documents/NinjaTrader 8/db/amc_volume_profile.db`). |
| **Activer Alertes Telegram VP** | `true` | Active les notifications Telegram pour les niveaux et zones VP. |
| **Confluence Min pour Alerte** | `2` | Nombre minimum de niveaux convergents pour déclencher une alerte confluence. |
| **Cooldown Alerte Niveau (Minutes)** | `15` | Délai minimal anti-spam avant de pouvoir ré-alerter sur le même niveau. |
| **Alerter sur 1er Test de Niveau/Zone** | `true` | Émet un message lors du premier test d'un niveau/confluence. |
| **Alerter sur Rejet Confirmé** | `true` | Émet un message lors d'un rejet avec mèche et delta confirmé. |
| **Alerter sur Entrée en LVN (Vide)** | `true` | Émet un avertissement lors de l'entrée dans un Low Volume Node. |

---

*Document généré pour le projet AMC PRO V8.0 - Système d'intelligence de marché & Volume Profile.*
