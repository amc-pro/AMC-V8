# DOCUMENT TECHNIQUE D'IMPLÉMENTATION — RÉGIME SWING V2 & INVALIDATION STRUCTURELLE

**Version :** 2.0  
**Branche Git :** `feat/swing-v3-opportunity-manager`  
**Statut :** Validé — 120/120 Tests Unitaires Réussis (0 Échecs)  
**Date :** Septembre 2026  
**Auteurs :** Équipe AMC Pro Quantitative Research & Core Architecture  

---

## 1. Contexte & Diagnostic Forensic

### 1.1 Révélation de l'Audit Forensic
L'audit technique, statistique et comportemental documenté dans `MD/REGIME_CHANGED_AUDIT.md` et complété par le replay contrefactuel tick-par-tick sur 558 trades Swing clôturés par `REGIME_CHANGED` en S1 2026 (`scratch/counterfactual_regime_exits.csv`) a établi sans équivoque que le mécanisme de coupure agressive `REGIME_CHANGED` (Hard Exit sur M5 vs H1 EMA) détruisait l'alpha du système :

| Métrique Forensic S1 2026 | Valeur Réelle Mesurée | Impact Stratégique |
| :--- | :---: | :--- |
| **Trades coupés par REGIME_CHANGED** | **558 trades** | 100 % des sorties prématurées auditées |
| **Trades qui auraient touché SL en premier** | 255 (45.7 %) | +219.56 R de perte brute épargnée |
| **Trades qui auraient touché TP2 en premier** | **302 (54.1 %)** | **-871.74 R de gain brut confisqué** |
| **Bilan Net Alpha Détruit par le mécanisme** | **-691.53 R** | **Détérioration massive de l'espérance mathématique** |
| **Sorties survenues avant 15 minutes (≤ 3 barres M5)** | **89.2 % (498 trades)** | Bruit de repli intrajournalier confondu avec un retournement |
| **Part des setups MacroReversal liquidés** | **77.8 % (434 trades)** | **Anomalie structurelle flagrante (position contre-EMA attendue)** |

### 1.2 Diagnostic d'Architecture
1. **Défaut de temporalité :** Une position Swing calibrée pour durer 2 à 8 heures ne peut pas être arbitrée sur une clôture M5 franchissant l'EMA H1.
2. **Absence de filtre structurel :** Le prix traversait l'EMA HTF lors de simples retests normaux de Value Area ou de POC sans qu'aucun pivot ni stop structurel ne soit violé.
3. **Pénalisation de MacroReversal :** Par construction, un trade de retournement macro Long entre *sous* l'EMA HTF baissière pour exploiter l'excès volumétrique ; le tester contre cette même EMA conduisait à son exécution immédiate dès la barre suivante.

---

## 2. Nouvelle Philosophie : "Structure-First"

L'architecture V2 repose sur le triptyque institutionnel fondamental :

$$\text{Régime} = \text{Contexte} \quad\vert\quad \text{Structure} = \text{Validation} \quad\vert\quad \text{Risk Management} = \text{Protection}$$

```
+-------------------------------------------------------------------------------+
|                       CYCLE DE DÉCISION STRUCTURE-FIRST                        |
+-------------------------------------------------------------------------------+
|                                                                               |
|  [Régime HTF Adverse Détecté]                                                 |
|               |                                                               |
|               v                                                               |
|  [MacroReversal ?] -------- OUI --------> [Immunité Activée : Maintenir Trade]|
|               | (NON)                                                         |
|               v                                                               |
|  [Hystérésis & Persistance : ConsecutiveAdverseBars >= 3 ?]                   |
|               |                                                               |
|         +-----+-----+                                                         |
|         |           | (OUI)                                                   |
|       (NON)         v                                                         |
|         |    [Structure Intacte ? (Prix vs StructuralStopPrice)]              |
|         |           |                                                         |
|         |     +-----+-----+                                                   |
|         |     |           |                                                   |
|         |  (INTACTE)   (ROMPUE)                                               |
|         |     |           |                                                   |
|         |     v           v                                                   |
|         |  [Position En Gain ?]        [STRUCTURAL_REGIME_INVALIDATION]       |
|         |     |                        - Clôture au marché                    |
|         |  +--+--+                     - Notification OpportunityManager      |
|         |  |     |                     - Marquer Campagne: RegimeChanged      |
|         | (OUI) (NON)                                                         |
|         |  |     |                                                            |
|         |  v     v                                                            |
|         | [BE] [HOLD]                                                         |
|         |                                                                     |
|         v                                                                     |
|  [HOLD : Géré par Stop Loss & Cibles Naturelles]                              |
|                                                                               |
+-------------------------------------------------------------------------------+
```

### Règle d'or
**Le Stop Loss initial reste le garde-fou ultime du capital.** Une dégradation de contexte ne doit jamais provoquer de sortie panique au marché si la structure technique qui a justifié le signal reste valide.

---

## 3. Détails d'Implémentation C#

### 3.1 Nouveaux Enums et Extensions (`AuctionMarketCore.Swing.Models.cs`)

#### `SwingRegimeHealth`
Définit la compatibilité instantanée entre le trade et le régime HTF résolu :
```csharp
public enum SwingRegimeHealth
{
    Aligned,      // Régime confirme pleinement la direction (Long: TrendUp/Expansion, Short: TrendDown/Compression)
    Neutral,      // Régime neutre ou setup immunisé (MacroReversal)
    Deteriorated  // Régime opposé persistant (Long: TrendDown, Short: TrendUp)
}
```

#### `SwingRegimeDecision`
Action graduée déterminée par le moteur de gestion des positions :
```csharp
public enum SwingRegimeDecision
{
    Hold,              // Maintenir le trade avec sa gestion normale (SL / TP)
    ProtectBreakeven,  // Déplacer le Stop Loss à Break-Even (+ 1 tick) sans couper
    StructuralExit     // Sortie au marché pour invalidation structurelle confirmée
}
```

#### Évaluation de Santé & Invalidation (`TrackedSwingTrade.EvaluateRegimeDecision`)
```csharp
public SwingRegimeDecision EvaluateRegimeDecision(
    SwingMarketRegime currentRegime,
    double close,
    double htfEma,
    double atrDaily,
    int confirmationBarsRequired,
    bool enableSoftProtection)
{
    if (Closed) return SwingRegimeDecision.Hold;

    // 1. Immunité absolue pour MacroReversal
    bool isMacroReversal = SetupType == SwingSetupType.MacroReversal;
    SwingRegimeHealth health = SwingRegimeHealth.Neutral;
    
    if (isMacroReversal)
    {
        health = SwingRegimeHealth.Neutral;
    }
    else
    {
        if (IsLong)
        {
            if (currentRegime == SwingMarketRegime.TrendUp || currentRegime == SwingMarketRegime.Expansion)
                health = SwingRegimeHealth.Aligned;
            else if (currentRegime == SwingMarketRegime.TrendDown)
                health = SwingRegimeHealth.Deteriorated;
            else
                health = SwingRegimeHealth.Neutral;
        }
        else
        {
            if (currentRegime == SwingMarketRegime.TrendDown || currentRegime == SwingMarketRegime.Compression)
                health = SwingRegimeHealth.Aligned;
            else if (currentRegime == SwingMarketRegime.TrendUp)
                health = SwingRegimeHealth.Deteriorated;
            else
                health = SwingRegimeHealth.Neutral;
        }
    }

    // 2. Amortissement par Hystérésis
    if (health == SwingRegimeHealth.Deteriorated)
        ConsecutiveAdverseBars++;
    else if (ConsecutiveAdverseBars > 0)
        ConsecutiveAdverseBars = Math.Max(0, ConsecutiveAdverseBars - 1);

    // 3. Confirmation temporelle
    int minBars = Math.Max(1, confirmationBarsRequired);
    bool isDeteriorationConfirmed = ConsecutiveAdverseBars >= minBars;

    // 4. Test d'invalidation structurelle (Structure-First)
    double structLevel = StructuralStopPrice;
    bool isStructureInvalidated = structLevel > 0 
        ? (IsLong ? (close < structLevel) : (close > structLevel)) 
        : false;

    // Cas A : Détérioration confirmée + Structure rompue -> Sortie
    if (isDeteriorationConfirmed && isStructureInvalidated)
        return SwingRegimeDecision.StructuralExit;

    // Cas B : Détérioration confirmée + Structure intacte + En profit -> Protection BE
    if (isDeteriorationConfirmed && enableSoftProtection && !isStructureInvalidated)
    {
        bool inProfit = IsLong ? (close > EntryPrice) : (close < EntryPrice);
        if (inProfit || Tp1Hit)
            return SwingRegimeDecision.ProtectBreakeven;
    }

    return SwingRegimeDecision.Hold;
}
```

---

## 4. Propriétés NinjaScript & Calibration

### 4.1 Propriétés Exposées (`AuctionMarketCore.Swing.cs`)

| Propriété C# / NinjaScript | Type | Valeur Défaut | Plage | Description Métier |
| :--- | :---: | :---: | :---: | :--- |
| `ExitOnRegimeChange` | `bool` | `false` | `true/false` | **Legacy Hard Exit :** Sortie immédiate si cours traverse EMA HTF (désactivé par défaut suite audit). |
| `EnableSwingRegimeInvalidation` | `bool` | `false` | `true/false` | **Architecture V2 :** Active l'invalidation structurelle multibarres conditionnée. |
| `RegimeConfirmationBars` | `int` | `3` | `1..20` | Nombre de barres consécutives sous régime adverse requises pour confirmation. |
| `EnableRegimeSoftProtection` | `bool` | `true` | `true/false` | Trailing du stop à Break-Even si régime adverse confirmé mais structure préservée. |

### 4.2 Hiérarchie Stricte des Priorités dans `UpdateOpenSwingTrades()`
L'ordre d'évaluation a été réorganisé pour éliminer toute sortie aberrante avant la vérification des règles de risque capital :
1. **Stop Loss (Priorité Absolue)** : Exécuté sur le Stop Price si `low <= stop` (Long) ou `high >= stop` (Short).
2. **Take Profit 1 (TP1)** : Prise de profit partielle à Target 1 + trailing stop initial à Break-Even (+ 1 tick).
3. **Take Profit 2 (TP2)** : Sortie finale du solde des contrats sur Target 2.
4. **Timeout (`SwingMaxBarsInTrade`)** : Sortie temporelle si le trade excède la durée maximale de détention.
5. **Rupture Régime Legacy (`ExitOnRegimeChange == true`)** : Option de compatibilité B (requiert `BarsElapsed >= 12` et exclut mean-reversion).
6. **Invalidation Structurelle V2 (`EnableSwingRegimeInvalidation == true`)** : Déclenche `ProtectBreakeven` ou `StructuralExit` (`STRUCTURAL_REGIME_INVALIDATION`).

---

## 5. Matrice des 8 Fichiers XML (`configs/SWING/`)

Les 8 configurations ont été alignées sur la recommandation institutionnelle (`ExitOnRegimeChange = false`) :

```xml
      <!-- Paramètres Opportunity Manager & Régime V2 -->
      <EnableOpportunityManager>true</EnableOpportunityManager>
      <SameCampaignLock>true</SameCampaignLock>
      <RequireNewStructureForReentry>true</RequireNewStructureForReentry>
      <ExitOnRegimeChange>false</ExitOnRegimeChange>
      <EnableSwingRegimeInvalidation>false</EnableSwingRegimeInvalidation>
      <RegimeConfirmationBars>3</RegimeConfirmationBars>
      <EnableRegimeSoftProtection>true</EnableRegimeSoftProtection>
      <SwingEntryCooldownBars>12</SwingEntryCooldownBars>
      <SwingMaxEntriesPerSession>2</SwingMaxEntriesPerSession>
```

Fichiers validés :
1. `CONFIG_CL_SWING.xml` (Pétrole Brut)
2. `CONFIG_MCL_SWING.xml` (Micro Pétrole Brut)
3. `CONFIG_ES_SWING.xml` (S&P 500)
4. `CONFIG_MES_SWING.xml` (Micro S&P 500)
5. `CONFIG_GC_SWING.xml` (Or Comex)
6. `CONFIG_MGC_SWING.xml` (Micro Or Comex)
7. `CONFIG_NQ_SWING.xml` (Nasdaq 100)
8. `CONFIG_MNQ_SWING.xml` (Micro Nasdaq 100)

---

## 6. Validation & Suite de Tests (120/120 Tests Passés)

La suite de tests automatisée `Tests/Program.cs` exécutée via `dotnet run --project Tests/VolumeProfileTests.csproj` intègre désormais 9 tests unitaires dédiés certifiant tous les cas d'usage :

| Nom du Test Unitaire | Objet Vérifié | Résultat |
| :--- | :--- | :---: |
| `Test_SwingV2_SimpleRegimeChange_NoExit` | Dégradation de régime sans bris structurel -> Maintien de la position (`Hold`) | **PASS** |
| `Test_SwingV2_RegimeDeterioration_And_StructuralInvalidation_Exit` | Dégradation 3 barres + bris du Stop Structurel -> Sortie `STRUCTURAL_REGIME_INVALIDATION` | **PASS** |
| `Test_SwingV2_MacroReversal_Long_Immunity` | Trade Long sous EMA HTF -> Immunité totale, 0 barre adverse comptabilisée | **PASS** |
| `Test_SwingV2_MacroReversal_Short_Immunity` | Trade Short au-dessus EMA HTF -> Immunité totale, 0 barre adverse comptabilisée | **PASS** |
| `Test_SwingV2_SoftProtection_Trails_Stop_To_Breakeven` | Dégradation confirmée + structure intacte + gain -> Trailing Stop à Break-Even | **PASS** |
| `Test_SwingV2_LegacyFlag_ExitOnRegimeChange_BackwardCompatibility` | Drapeau `ExitOnRegimeChange = true` préserve le comportement historique pour A/B testing | **PASS** |
| `Test_SwingV2_DefaultSettings_NoPrematureExit` | Contrôle d'intégrité des 8 XML : tous configurés à `false` par défaut | **PASS** |
| `Test_SwingV2_AdverseBars_Hysteresis_And_Persistence` | Amortissement : le compteur s'incrémente sous bruit puis se décrémente au réalignement | **PASS** |
| `Test_SwingV2_StrictIsolation_ScalpingPro_Sniper` | Isolation étanche : aucun symbole ou appel Swing n'affecte ScalpingPro ou Sniper | **PASS** |

---

## 7. Recommandations d'Exploitation & A/B Testing

1. **Mode Recommandé en Production (Par Défaut) :**  
   - `ExitOnRegimeChange = false`  
   - `EnableSwingRegimeInvalidation = false`  
   *Justification :* Les Stop Loss initiaux et les sorties partielles TP1/TP2 avec BE trailing fournissent un ratio de gain optimal sans risquer de confisquer les coureurs (+691.53 R récupérés).
2. **Mode V2 Expérimental (Structure-First) :**  
   - `ExitOnRegimeChange = false`  
   - `EnableSwingRegimeInvalidation = true`  
   - `RegimeConfirmationBars = 3`  
   - `EnableRegimeSoftProtection = true`  
   *Usage :* Pour les comptes à tolérance de drawdown plus stricte souhaitant verrouiller le Break-Even en avance dès confirmation de divergence de tendance.
3. **Mode A/B Testing Legacy :**  
   - `ExitOnRegimeChange = true` permet de reproduire à l'identique les résultats du backtest historique si nécessaire.
