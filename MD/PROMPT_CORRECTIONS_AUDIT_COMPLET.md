# Prompt — Corrections complètes post-audit AMC-V8

> Copier-coller le bloc ci-dessous dans une nouvelle session Cursor/Agent pour demander l'implémentation intégrale des correctifs identifiés lors de l'audit du projet `AMC-V8`.

---

## PROMPT À COPIER

```
Tu travailles sur le dépôt AMC-V8 (NinjaTrader 8, indicateur AuctionMarketCore).
Un audit approfondi a identifié des incohérences entre modules, des stubs incomplets en Swing, des risques de concurrence Telegram/SQLite, et des lacunes de tests.

OBJECTIF : implémenter TOUTES les corrections listées ci-dessous, dans l'ordre de priorité, avec un diff minimal, en respectant les conventions existantes du projet (partial classes, naming, commentaires en français pour la logique métier, pas de sur-ingénierie).

---

## CONTEXTE PROJET

- Classe principale : `public partial class AuctionMarketCore : Indicator` répartie en 11 fichiers partiels.
- Presets actifs : `ScalpingPro` (intraday) et `Swing` (macro).
- Modules autonomes : `VolumeProfile/` (namespace VolumeProfilePro), `MarketIntelligence/` (namespace SniperMarketIntelligence).
- Tests : `Tests/Program.cs` via `dotnet run --project Tests/VolumeProfileTests.csproj` — **tous les tests existants doivent rester PASS** ; ajouter des tests pour chaque correctif critique.
- Discipline bar-close / anti-lookahead : ne jamais régresser `evalOffset`, `EvaluateOnBarClose`, `PrevDay/Week/Month` clôturés.

---

## PHASE 1 — CORRECTIFS CRITIQUES (OBLIGATOIRE)

### 1.1 Corriger `IsScalpingPro` (AuctionMarketCore.ScalpingPro.cs)

**Problème :** `IsScalpingPro { get { return true; } }` force le pipeline ScalpingPro même en preset Swing.

**Correction attendue :**
- `IsScalpingPro` doit retourner `TradingPreset == SniperMarketPreset.ScalpingPro`.
- Vérifier que `ScalpingProOnEvaluatedBar()`, `ApplyScalpingProPipeline()`, et toutes les branches `if (IsScalpingPro)` ne s'exécutent PAS en preset Swing.
- En preset Swing : le moteur Swing (`SwingOnEvaluatedBar`) reste actif ; le pipeline ScalpingPro pondéré ne doit pas contaminer les candidats Sniper ni le scoring Swing.

**Tests à ajouter :**
- `Test_ScalpingPro_IsScalpingPro_False_When_Swing_Preset`
- `Test_ScalpingPro_Pipeline_Skipped_When_Swing_Preset`

---

### 1.2 Initialiser `EnableSniperEngine` dans les defaults (AuctionMarketCore.Sniper.cs)

**Problème :** `EnableSniperEngine` n'est jamais mis à `true` dans `ApplySniperDefaults()` — défaut C# = false, moteur inactif sans XML.

**Correction attendue :**
- Mettre `EnableSniperEngine = true` dans `ApplySniperDefaults()` et/ou `ApplyScalpingProPreset()`.
- En preset Swing : documenter et implémenter une désactivation explicite du Sniper si `IsSwing && EnableSwingEngine` (éviter double émission Sniper + Swing). Préférer : `if (IsSwing) return;` en tête de `SniperOnEvaluatedBar()` ou guard équivalent, sauf si paramètre utilisateur explicite pour activer les deux.

**Tests à ajouter :**
- `Test_Sniper_Enabled_By_Default_ScalpingPro`
- `Test_Swing_Sniper_Skipped_When_Swing_Preset`

---

### 1.3 Corriger les helpers Swing incomplets (AuctionMarketCore.Swing.cs)

**Problèmes actuels :**
- `IsInActiveFvg()` teste un cross POC, pas un FVG réel.
- `HasRecentAbsorption()` utilise delta hardcodé ±100 sans Z-score.
- `RegimeHtf` assigné par direction (`isBuy ? TrendUp : TrendDown`) au lieu du régime réel.
- `HasRecentBos()` / `HasRecentChoch()` : vérifier cohérence avec MI après correction API (déjà partiellement corrigé — valider la logique finale).

**Correction attendue :**
- **FVG :** utiliser `fvgEngineZones` (Engine) ou `SmcStructureTracker` (ScalpingPro) — choisir UNE source et documenter le choix dans un commentaire bref. Vérifier qu'une zone FVG active existe dans le sens du setup et que le prix est dedans ou en retest.
- **Absorption :** utiliser les flags existants du moteur (`isBullishAbsorptionActive`, `isBearishAbsorptionActive`) ou `AbsorptionCluster()` / Z-delta (`ZDeltaCurrent()`) selon le sens du setup.
- **RegimeHtf :** dériver du régime HTF réel (EMA H4, `htfEma`, ou `GetMarketIntelligenceBias()` / trend MI) — pas de la direction du trade candidat.
- **BOS/CHOCH :** conserver `miAnalyzer.LastBos` / `miAnalyzerH4.LastChoch` avec `MiStructureEvent` et `SmcEventMaxAgeBars`.

**Tests à ajouter :**
- `Test_Swing_IsInActiveFvg_Uses_Real_Fvg_Zones`
- `Test_Swing_RegimeHtf_Derived_From_Htf_Not_Direction`
- `Test_Swing_HasRecentAbsorption_Uses_Engine_Flags`

---

### 1.4 Corriger `RiskRewardScore` hardcodé (AuctionMarketCore.Swing.Models.cs)

**Problème :** `s.RiskRewardScore = 9.0;` avant calcul RR réel dans `BuildAndSizeSignal()`.

**Correction attendue :**
- Calculer `RiskRewardScore` (0..10) APRÈS sizing, à partir du RR réel vers TP1/TP2 (ou zone adverse structurelle).
- Formule suggérée : mapper RR 1.0→5 pts, RR 2.0→8 pts, RR ≥3.0→10 pts (ajuster selon `MinRiskReward` existant).
- Le filtre `MinRiskReward` doit rester en aval ; le score ne doit plus valider un setup avec RR fictif.

**Tests à ajouter :**
- `Test_Swing_RiskRewardScore_Computed_From_Real_RR`
- `Test_Swing_RiskRewardScore_Low_When_RR_Below_Min`

---

### 1.5 Peupler `PrevCurrentMonthlySd1Upper/Lower` (AuctionMarketCore.Swing.cs)

**Problème :** champs jamais assignés — contrôle d'acceptation multi-barres Monthly VWAP retest affaibli.

**Correction attendue :**
- Stocker les valeurs SD±1 du mois courant de la barre précédente (état persistant entre barres).
- Utiliser ces valeurs dans la branche « acceptation barre précédente » du setup MonthlyVwapBandRetest.

**Tests à ajouter :**
- `Test_MonthlyBand_Acceptance_Uses_Prev_Sd1_Values`

---

## PHASE 2 — ROBUSTESSE & COHÉRENCE (OBLIGATOIRE)

### 2.1 Unifier ou synchroniser SMC (source de vérité)

**Problème :** 3 implémentations parallèles — `SmcStructureTracker`, `MarketStructureAnalyzer` (MI), `fvgEngineZones` (Engine).

**Correction attendue (choix minimal) :**
- Option A (préférée) : `MarketStructureAnalyzer` (MI) devient la source BOS/CHOCH/OB pour Swing et modulateur MI ; `SmcStructureTracker` reste pour scoring ScalpingPro ; documenter la séparation.
- Option B : factoriser une interface `ISmcStructureSource` lue par Sniper, Swing et MI.
- **Minimum requis :** FVG pour Swing doit lire la même source que Sniper retest FVG (Engine `fvgEngineZones` ou tracker ScalpingPro — pas de stub POC).
- Aligner `CaptureOrderBlock` entre ScalpingPro et MI si divergence confirmée (même fenêtre, même critère bougie opposée).

---

### 2.2 Thread safety TelegramDispatcher (MarketIntelligence/TelegramDispatcher.cs)

**Problème :** `lastSentHash` / `lastSentUtc` lus/écrits sans synchronisation ; hash mis à jour avant confirmation d'envoi.

**Correction attendue :**
- Protéger l'état de déduplication par `lock` dédié ou `Interlocked` + structure immutable.
- Mettre à jour le hash **après** confirmation d'envoi réussi (callback `onComplete(true)`).
- Ajouter tests unitaires pour déduplication concurrente simulée.

---

### 2.3 SQLite Dispose & FlushQueue (VolumeProfile/VolumeProfileRepository.cs)

**Problème :** `Dispose()` n'attend pas `backgroundWorkerTask` ; `FlushQueue()` avale les exceptions.

**Correction attendue :**
- Dans `Dispose()` : annuler le token, **attendre** la fin du worker (timeout raisonnable 5s), puis fermer connexion.
- Dans `FlushQueue()` : remplacer `catch { }` par log via mécanisme existant (`RegisterRuntimeError` ou delegate logger si disponible).

---

### 2.4 Protéger `EvaluateOnBarClose` par preset

**Problème :** l'utilisateur peut forcer `EvaluateOnBarClose=false` → repaint backtest.

**Correction attendue :**
- Dans `ApplyScalpingProPreset()` et `ApplySwingPreset()` : forcer `EvaluateOnBarClose = true`.
- Dans `OnStateChange` Configure ou validation : si preset ScalpingPro/Swing et `EvaluateOnBarClose=false`, log warning + réinitialiser à true (ou rendre le paramètre read-only en preset actif).

---

### 2.5 Remplacer `lock(this)` et borner les listes

**Fichiers :**
- `AuctionMarketCore.Swing.cs` : remplacer `lock(this)` par objet dédié `swingJournalLock`.
- `closedSwingTrades` : borner à N entrées (ex. 500) avec trim FIFO.

---

### 2.6 Brancher ou supprimer `IsH1M15Aligned()` (AuctionMarketCore.MarketIntelligence.cs)

**Problème :** code mort — gate HTF « H1+M15 » documenté mais jamais appelé.

**Correction attendue :**
- Soit brancher sur `IsHtfAligned()` / gate Sniper quand `HtfSoftMode=false`.
- Soit supprimer la méthode et mettre à jour la doc si obsolète.
- Documenter le comportement HTF réel dans un commentaire près du gate.

---

### 2.7 Corriger fallbacks silencieux

| Fichier | Correction |
|---------|------------|
| `Swing.cs` `ResolvePointValue()` | log + retour 0 ou `Instrument.MasterInstrument.PointValue` sans catch vide |
| `Swing.cs` `CalculateSessionGapPercent()` | log warning au lieu de `catch { }` |
| `Exports.cs` `DispatchMt5Ack()` | log échec ACK |
| `Exports.cs` boucles TCP | log exceptions Accept/Handle |

---

## PHASE 3 — QUALITÉ & TESTS (OBLIGATOIRE)

### 3.1 Tests Sniper (priorité haute)

Ajouter dans `Tests/Program.cs` :
- Gates N1–N4 : rejet/acceptation par seuils
- Gate recovery ScalpingPro : vérifier qu'un setup faible N3 ne passe pas uniquement grâce au score global
- `ProcessSelectionBuffer()` : best-of-window, quota session
- Non-régression : preset Swing n'exécute pas ScalpingPro pipeline

### 3.2 Tests TelegramDispatcher

- Déduplication : même message 2x en <30s → 1 envoi
- Hash libéré après échec définitif → retry possible

### 3.3 Tests configs secrets

- Étendre `Test_Swing_19` aux 8 fichiers `configs/SCALPING_PRO/*.xml` (pas de token réel, placeholder attendu)

### 3.4 Gate footprint ScalpingPro

- Documenter ou corriger la contradiction « footprint obligatoire » vs bypass WEAK/Breakout
- Si comportement voulu : commentaire explicite + test `Test_ScalpingPro_Footprint_Bypass_Only_For_Momentum_Setups`

---

## CONTRAINTES D'IMPLÉMENTATION

1. **Diff minimal** — ne pas refactoriser au-delà du nécessaire pour chaque item.
2. **Pas de nouvelles dépendances** NuGet sauf si indispensable.
3. **Conventions** — matcher le style des fichiers existants (regions, naming, CultureInfo.InvariantCulture).
4. **Commentaires** — uniquement pour logique non évidente (choix source SMC, formule RR score).
5. **Ne pas commit/push** sauf demande explicite de l'utilisateur.
6. **Validation finale obligatoire :**
   ```
   dotnet run --project Tests/VolumeProfileTests.csproj
   ```
   Résultat attendu : **0 échec**, nombre de tests ≥ 90.

---

## LIVRABLES ATTENDUS

1. Liste des fichiers modifiés avec résumé 1 ligne par fichier.
2. Tableau : Problème audit → Correction → Test ajouté.
3. Résultat complet de la suite de tests.
4. Liste des items NON faits (si blocage) avec justification.

---

## ORDRE D'EXÉCUTION RECOMMANDÉ

1. Phase 1.1 → 1.5 (critiques Swing/ScalpingPro)
2. Lancer tests → corriger régressions
3. Phase 2.1 → 2.7 (robustesse)
4. Phase 3.1 → 3.4 (tests + doc)
5. Run final tests + résumé

Commence par lire les fichiers concernés, confirme ta compréhension en 5 lignes, puis implémente sans demander validation intermédiaire sauf ambiguïté bloquante.
```

---

## Notes d'utilisation

| Élément | Détail |
|---------|--------|
| **Fichier audit source** | Session audit du 31/08/2026 + `MD/SWING_AUDIT_AND_ADR_REPORT.md`, `MD/AUCTION_MARKET_CORE_AUDIT_REPORT.md` |
| **Branche de travail suggérée** | `fix/audit-corrections-completes` depuis `feat/auction-market-current-monthly-vwap-retest` ou `main` |
| **Durée estimée** | 2–4 sessions agent selon profondeur Phase 2 (unification SMC) |
| **Variante allégée** | Copier uniquement la section **PHASE 1** si correction urgente avant prod Swing |

## Variante courte (Phase 1 uniquement)

```
Corrige uniquement la PHASE 1 du fichier MD/PROMPT_CORRECTIONS_AUDIT_COMPLET.md dans AMC-V8 :
IsScalpingPro, EnableSniperEngine, helpers Swing (FVG/absorption/RegimeHtf), RiskRewardScore, PrevCurrentMonthlySd1*.
Ajoute les tests listés. Lance dotnet run --project Tests/VolumeProfileTests.csproj. Pas de commit.
```
