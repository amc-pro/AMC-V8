# Matrice de Configuration Institutionnelle — Système Swing (AMC-V8)

Ce document résume la calibration quantitative des 8 instruments supportés par le moteur Swing d'`AuctionMarketCore`. Chaque configuration est dimensionnée en fonction de la valeur intrinsèque du tick, de la structure CME et de la volatilité macro de l'actif.

---

## 1. Matrice des Instruments & Risque Swing

| Symbole | Nom de l'Instrument | Exchange | Tick Size | Multiplicateur Point | Valeur du Tick ($) | Risque par Trade ($) | Min Stop (Ticks / Pts) | Max Stop (Ticks / Pts) | Max Contrats |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **ES** | E-mini S&P 500 | CME | 0.25 | $50.00 | **$12.50** | $250 | 16 ticks (4.0 pts) | 80 ticks (20.0 pts) | 4 |
| **MES** | Micro E-mini S&P 500 | CME | 0.25 | $5.00 | **$1.25** | $50 | 16 ticks (4.0 pts) | 80 ticks (20.0 pts) | 10 |
| **NQ** | E-mini Nasdaq 100 | CME | 0.25 | $20.00 | **$5.00** | $300 | 40 ticks (10.0 pts) | 240 ticks (60.0 pts) | 4 |
| **MNQ** | Micro E-mini Nasdaq 100 | CME | 0.25 | $2.00 | **$0.50** | $60 | 40 ticks (10.0 pts) | 240 ticks (60.0 pts) | 10 |
| **GC** | Gold Futures | COMEX | 0.10 | $100.00 | **$10.00** | $250 | 20 ticks ($2.0) | 150 ticks ($15.0) | 4 |
| **MGC** | Micro Gold Futures | COMEX | 0.10 | $10.00 | **$1.00** | $50 | 20 ticks ($2.0) | 150 ticks ($15.0) | 10 |
| **CL** | Crude Oil Futures | NYMEX | 0.01 | $1,000.00 | **$10.00** | $250 | 25 ticks ($0.25) | 150 ticks ($1.50) | 4 |
| **MCL** | Micro Crude Oil | NYMEX | 0.01 | $100.00 | **$1.00** | $50 | 25 ticks ($0.25) | 150 ticks ($1.50) | 10 |

---

## 2. Paramètres d'Exécution & Profils Institutionnels

| Catégorie | Paramètre | ES / MES | NQ / MNQ | GC / MGC | CL / MCL | Justification Swing |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Timeframe** | `BaseBarsPeriodValue` | 15 min | 15 min | 15 min | 15 min | Vue intermédiaire swing réactive sur clôtures. |
| | `HtfMinutes` | 240 (4H) | 240 (4H) | 240 (4H) | 240 (4H) | Tendance de fond macro pour filtrage de contexte. |
| **Risk / Reward** | `TargetR1` | 1.5 R | 1.5 R | 1.5 R | 1.5 R | Sortie partielle TP1 sur zone intermédiaire/POC. |
| | `TargetR2` | 3.0 R | 3.0 R | 3.0 R | 3.0 R | Sortie finale TP2 sur borne opposée de Value Area. |
| | `StopAtrMultiple` | 2.0 | 2.25 | 2.0 | 2.0 | Multiplicateur ATR adapté au bruit de l'actif. |
| **Volume Profile** | `UseSessionProfile` | `true` | `true` | `true` | `true` | Références institutionnelles partagées. |
| | `CompositeSessions` | 30 | 30 | 30 | 30 | Contexte mensuel clôturé. |
| | `ValueAreaPercent` | 70% | 70% | 70% | 70% | Standard statistique d'acceptation 1-Sigma. |
| **Scoring Gates** | `MinScoreToAlert` | 50 | 50 | 50 | 50 | Filtre sélectif qualité institutionnelle. |
| | `TierSilverScore` | 50 | 50 | 50 | 50 | Grade Moyen. |
| | `TierGoldScore` | 70 | 70 | 70 | 70 | Grade Fort / Très Fort. |
| **Filtres Macro** | `NewsBlackoutMinutes` | 15 | 15 | 15 | 15 | Fenêtre de protection autour des chiffres majeurs. |
| | `NewsWindowPenalty` | 20 | 20 | 20 | 20 | Pénalité de score lors des annonces économiques. |
| | `JournalShadowMode` | `true` | `true` | `true` | `true` | Journalisation intégrale sans omission. |

---

## 3. Fichiers XML Associés

Les 8 fichiers de configuration sont situés dans le dossier `configs/SWING/` :
* `CONFIG_ES_SWING.xml`
* `CONFIG_MES_SWING.xml`
* `CONFIG_NQ_SWING.xml`
* `CONFIG_MNQ_SWING.xml`
* `CONFIG_GC_SWING.xml`
* `CONFIG_MGC_SWING.xml`
* `CONFIG_CL_SWING.xml`
* `CONFIG_MCL_SWING.xml`
