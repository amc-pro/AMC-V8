# AMC-V8 — Spécification & Guide d'Implémentation : Swing V3 Opportunity Manager

## 1. Contexte & Objectif Quantitatif

### Constat Initial (Swing V2)
- **Taux de Réussite (Win Rate)** : ~40% à 41%
- **Profit Factor** : ~1.04
- **Points de Défaillance Identifiés** :
  1. **Déclenchement barre-par-barre sans mémoire de campagne** : Émission de signaux répétés sur 5 à 10 barres M5 consécutives lors d'une même phase de marché.
  2. **Entrées HTF "en l'air"** : Entrées en continuation de tendance sans retour à la valeur (Value Area / VWAP / FVG / HVN), achetant le sommet d'un swing.
  3. **Breakout Retest toxique sur les indices technologiques (NQ/MNQ)** : Fausses cassures fréquentes menant à des séries de pertes.
  4. **Sorties arbitraires** : Déclenchement de sorties sur minuteur de barres (`SwingMaxBarsInTrade`) coupant prématurément des swings gagnants.
  5. **Prise de profit TP1 rigide** : Objectif TP1 fixé aveuglément à 1.5R sans tenir compte d'un mur institutionnel (VAH/VAL/SD Bands) situé juste avant.

### Cibles Quantitatives (Swing V3)
- **Win Rate visé** : **50% à 56%+**
- **Profit Factor visé** : **> 1.40**
- **Réduction du Drawdown** : Suppression du sur-trading et des pertes en cascade.

---

## 2. Architecture & Nouveaux Composants

### 2.1. `SwingOpportunityManager` & `SwingCampaign`
Situé dans `AuctionMarketCore.Swing.Models.cs`, ce composant centralise la gestion du cycle de vie des campagnes Swing :

```csharp
public sealed class SwingOpportunityManager
{
    public bool Enabled { get; set; }
    public bool SameCampaignLock { get; set; }
    public bool RequireNewStructureForReentry { get; set; }
    public int EntryCooldownBars { get; set; }
    public int MaxEntriesPerSession { get; set; }
    public int MaxLongEntriesPerSession { get; set; }
    public int MaxShortEntriesPerSession { get; set; }

    public SwingCampaign ActiveLongCampaign { get; set; }
    public SwingCampaign ActiveShortCampaign { get; set; }
    public Dictionary<string, int> RecentSignatures { get; }
    ...
}
```

- **`SameCampaignLock` (défaut : `true`)** : Verrouille la campagne active dans une direction donnée. Tout candidat appartenant à la même campagne ou tenté pendant qu'une position est active est rejeté (`DuplicateCampaign`).
- **`RequireNewStructureForReentry` (défaut : `true`)** : Après clôture d'un trade, une nouvelle entrée n'est autorisée que si une nouvelle rupture structurelle (BOS / CHOCH) est intervenue.
- **`EntryCooldownBars` (défaut : `12` barres)** : Cooldown obligatoire de 1 heure sur timeframe M5 entre deux prises de position dans la même direction.
- **`SwingSetupSignature`** : Signature unique et déterministe basée sur `Symbol:SetupType:Direction:StructureId:RegimeId:AnchorPrice`.

### 2.2. Collecte Multi-Candidats & Ranking V3
Dans `AuctionMarketCore.Swing.cs` (`EvaluateSwingDirection`) :
- Suppression du `break;` prématuré : tous les setups activés sont évalués.
- Chaque signal valide est transformé en `SwingCandidate`.
- Les candidats sont scorés via `ISwingScorer.ComputeQualityMetrics` :
  - `TimingQuality` (distance optimale par rapport à la formation du setup)
  - `RegimeCompatibility` (alignement avec la tendance HTF)
  - `DirectionalQuality` (confluence avec la Value Area et le VWAP)
  - `LocationQuality` (proximité d'un niveau institutionnel clé)
  - `LateEntryPenalty` (pénalité si le prix s'est trop éloigné de l'ancre structurelle)
  - `ConflictPenalty` (pénalité en cas de signaux contradictoires)
- Les candidats sont triés par `FinalQualityScore` décroissant. Seul le meilleur candidat éligible est exécuté.

### 2.3. Snapping Dynamique de TP1
Dans `SwingRiskManager.CalculateTargets` :
- Si un niveau institutionnel opposé (VAH, VAL, SD+2, SD-2) se situe entre 1.0R et 1.5R de l'entrée :
  - **TP1 est calé précisément sur ce niveau adverse** (avec un buffer de ticks de sécurité).
  - Ce snapping garantit que TP1 est atteint avant le heurt d'un mur d'ordres institutionnels, sécurisant le win rate et activant la bascule du Stop à Break-Even.

### 2.4. Hard Exit sur Changement de Régime & Maintien de Position
Dans `AuctionMarketCore.Swing.cs` (`UpdateOpenSwingTrades`) :
- **`ExitOnRegimeChange = false` (Recommandé en Production)** : Le test d'audit OOS H1 2026 a prouvé que couper sur simple franchissement M5 de l'EMA HTF avortait prématurément 53.3% des trades (notamment les MacroReversals et pullbacks), détruisant -$66,6K$. La désactivation permet de laisser les positions atteindre leur cycle complet (SL / TP1 / TP2 / BE), débloquant **+$57 605 $ (+59,85 R, PF 1.23)** avec 100% des 5 actifs dans le vert.
- **Garde-fous si activé** : Exige au minimum 12 barres de maturité (~1h) et exclut formellement les setups de mean-reversion (`MacroReversal`, `ValueReentry`).
- **`SwingMaxBarsInTrade = 0`** : Désactivation du minuteur de barres arbitraire. La position est conservée tant que le Stop ou le TP ne l'invalident pas.

### 2.5. Spécialisation par Actif
- **NQ & MNQ** : `EnableSwingBreakoutRetest` est fixé à `false` dans `CONFIG_NQ_SWING.xml` et `CONFIG_MNQ_SWING.xml`.
- **CL & MCL** : `EnableSwingBreakoutRetest` reste à `true` dans `CONFIG_CL_SWING.xml` et `CONFIG_MCL_SWING.xml`.

---

## 3. Matrice des Nouveaux Paramètres XML

Les 8 fichiers de configuration dans `configs/SWING/` intègrent désormais la section standardisée :

```xml
  <!-- Swing 08. Opportunity Management & Win Rate (V3) -->
  <EnableOpportunityManager>true</EnableOpportunityManager>
  <SameCampaignLock>true</SameCampaignLock>
  <RequireNewStructureForReentry>true</RequireNewStructureForReentry>
  <ExitOnRegimeChange>false</ExitOnRegimeChange>
  <SwingEntryCooldownBars>12</SwingEntryCooldownBars>
  <SwingMaxEntriesPerSession>2</SwingMaxEntriesPerSession>       <!-- 0 = Illimité, 1..10 = Plafond actif -->
  <SwingMaxLongEntriesPerSession>1</SwingMaxLongEntriesPerSession>   <!-- 0 = Illimité, 1..10 = Plafond actif -->
  <SwingMaxShortEntriesPerSession>1</SwingMaxShortEntriesPerSession> <!-- 0 = Illimité, 1..10 = Plafond actif -->
  <SwingMaxBarsInTrade>0</SwingMaxBarsInTrade>                     <!-- 0 = Infini (sortie sur SL/TP/Régime) -->
  <EnableLateEntryPenalty>true</EnableLateEntryPenalty>
  <EnableCandidateRanking>true</EnableCandidateRanking>
```

> **Règle de Dimensionnement des Sessions (Option A) :**
> Les attributs `[Range(0, 10)]` sur les propriétés C# permettent désormais de configurer explicitement `0` pour désactiver le plafond (mode illimité), tout en conservant les valeurs par défaut de production (`2` entrées max, `1` long, `1` short) pour prévenir le sur-trading.

---

## 4. Résultats des Tests de Non-Régression

Commande exécutée :
```bash
dotnet run --project Tests/VolumeProfileTests.csproj
```

**Résultat : 111 Tests Réussis, 0 Échec.**
- 99 tests historiques (Volume Profile, VWAP, SMC, FVG, ScalpingPro Isolation, PocMigration, MonthlyVWAP) : **100% PASS**.
- 12 nouveaux tests dédiés Swing V3 : **100% PASS**.
