"""
Backtest complet du mode Scalping Pro (AMC-V8) sur les données Gold Futures (GC_5min_recent.csv).
Période : Juin 2026 à Août 2026 (11 630 barres 5-min).
Auteur : Antigravity (AMC-V8)
"""

import math
import numpy as np
import pandas as pd
from collections import defaultdict, Counter
from datetime import datetime

def run_scalping_pro_backtest(csv_path="historical_data_recent/GC_5min_recent.csv"):
    print("=" * 80)
    print("       AMC-V8 — BACKTEST DU MOTEUR SCALPING PRO SUR GOLD (GC 5-MIN)       ")
    print("=" * 80)
    print(f"Chargement des données : {csv_path}...")

    # 1. Chargement et normalisation des données
    df = pd.read_csv(csv_path, skiprows=3, header=None, names=['Datetime', 'Close', 'High', 'Low', 'Open', 'Volume'])
    df['Datetime'] = pd.to_datetime(df['Datetime'])
    df['Date'] = df['Datetime'].dt.strftime('%Y-%m-%d')
    df['Time'] = df['Datetime'].dt.strftime('%H:%M:%S')
    
    total_bars = len(df)
    print(f"-> Total barres 5-min chargées : {total_bars}")
    print(f"-> Période couverte : du {df['Datetime'].iloc[0]} au {df['Datetime'].iloc[-1]}")

    # 2. Calcul des indicateurs techniques de base
    # True Range & ATR(14)
    df['PrevClose'] = df['Close'].shift(1)
    df['TR'] = np.maximum(
        df['High'] - df['Low'],
        np.maximum(
            (df['High'] - df['PrevClose']).abs(),
            (df['Low'] - df['PrevClose']).abs()
        )
    )
    df.loc[0, 'TR'] = df.loc[0, 'High'] - df.loc[0, 'Low']
    df['ATR'] = df['TR'].rolling(window=14, min_periods=1).mean()

    # Volume percentile rank (100-bar rolling)
    df['VolRank'] = df['Volume'].rolling(window=100, min_periods=10).apply(
        lambda s: (s < s.iloc[-1]).mean() * 100.0 if len(s) > 0 else 50.0, raw=False
    )
    df['VolRank'] = df['VolRank'].fillna(50.0)

    # Proxy Delta / Order Flow
    bar_range = df['High'] - df['Low']
    bar_range = np.where(bar_range == 0, 0.1, bar_range)
    df['DeltaProxy'] = ((df['Close'] - df['Open']) / bar_range) * df['Volume']
    df['ZDelta'] = df['DeltaProxy'].rolling(window=20, min_periods=5).apply(
        lambda s: (s.iloc[-1] - s.mean()) / (s.std() + 1e-6) if len(s) > 0 else 0.0, raw=False
    ).fillna(0.0)

    # 3. Calcul par session : VWAP, Bandes d'écart-type SD, Volume Profile (POC, VAH, VAL)
    # Session date (CME Globex session reset at 18:00 ET)
    session_ids = []
    current_session = df['Date'].iloc[0]
    for idx, row in df.iterrows():
        dt = row['Datetime']
        if dt.hour >= 18:
            # Session du lendemain
            next_day = dt + pd.Timedelta(days=1)
            session_ids.append(next_day.strftime('%Y-%m-%d'))
        else:
            session_ids.append(dt.strftime('%Y-%m-%d'))
    df['Session'] = session_ids

    # Calcul VWAP et Volume Profile cumulatifs par session
    print("Calcul du Volume Profile, VWAP et bandes statistiques par session...")
    vwap_vals = []
    sd1_vals = []
    sd2_vals = []
    sd3_vals = []
    poc_vals = []
    vah_vals = []
    val_vals = []

    for session_name, s_df in df.groupby('Session', sort=False):
        cum_pv = 0.0
        cum_vol = 0.0
        cum_pv2 = 0.0
        price_bins = defaultdict(float)

        for _, row in s_df.iterrows():
            tp = (row['High'] + row['Low'] + row['Close']) / 3.0
            vol = max(1.0, float(row['Volume']))
            cum_pv += tp * vol
            cum_vol += vol
            cum_pv2 += (tp ** 2) * vol

            v_val = cum_pv / cum_vol
            var = max(0.0, (cum_pv2 / cum_vol) - (v_val ** 2))
            sd = math.sqrt(var)

            vwap_vals.append(v_val)
            sd1_vals.append(sd)
            sd2_vals.append(sd * 2.0)
            sd3_vals.append(sd * 3.0)

            # VP bin (tick size 0.10)
            p_bin = round(row['Close'], 1)
            price_bins[p_bin] += vol

            # Approximer POC, VAH, VAL
            sorted_bins = sorted(price_bins.items(), key=lambda x: x[1], reverse=True)
            poc = sorted_bins[0][0]
            target_va_vol = cum_vol * 0.70
            va_vol = 0.0
            va_prices = []
            for p, v in sorted_bins:
                va_vol += v
                va_prices.append(p)
                if va_vol >= target_va_vol:
                    break
            vah = max(va_prices) if va_prices else poc
            val = min(va_prices) if va_prices else poc

            poc_vals.append(poc)
            vah_vals.append(vah)
            val_vals.append(val)

    df['VWAP'] = vwap_vals
    df['SD1'] = sd1_vals
    df['SD2'] = sd2_vals
    df['SD3'] = sd3_vals
    df['POC'] = poc_vals
    df['VAH'] = vah_vals
    df['VAL'] = val_vals

    # 4. Détection des structures SMC & Setups Scalping Pro
    print("Exécution de l'algorithme de détection SMC et Scalping Pro...")

    candidates = []
    active_fvgs = []  # {'is_bull': bool, 'top': float, 'bottom': float, 'bar': int, 'mitigated': bool}
    active_obs = []   # {'is_bull': bool, 'top': float, 'bottom': float, 'bar': int}
    
    swing_high = 0.0
    swing_low = 0.0
    swing_high_bar = -1
    swing_low_bar = -1
    trend = 0  # +1 Bull, -1 Bear
    
    # Paramètres de risk management GC
    TICK_SIZE = 0.10
    MIN_STOP_TICKS = 12   # $1.20 min stop
    MAX_STOP_TICKS = 160  # $16.00 max stop
    STOP_ATR_MULT = 1.75
    STOP_BUFFER_TICKS = 2 # $0.20 buffer
    
    recent_signals = []

    for i in range(15, total_bars):
        row = df.iloc[i]
        bar_idx = i
        c = row['Close']
        o = row['Open']
        h = row['High']
        l = row['Low']
        v = row['Volume']
        atr = max(0.5, row['ATR'])
        vol_rank = row['VolRank']
        z_delta = row['ZDelta']
        vwap = row['VWAP']
        sd1 = row['SD1']
        sd2 = row['SD2']
        sd3 = row['SD3']
        poc = row['POC']
        vah = row['VAH']
        val = row['VAL']
        dt_str = str(row['Datetime'])

        # Détection Pivot Swing High / Low (strength = 2)
        p_h2 = df['High'].iloc[i-2]
        p_l2 = df['Low'].iloc[i-2]
        if (p_h2 > df['High'].iloc[i-4] and p_h2 > df['High'].iloc[i-3] and 
            p_h2 > df['High'].iloc[i-1] and p_h2 > df['High'].iloc[i]):
            swing_high = p_h2
            swing_high_bar = i - 2

        if (p_l2 < df['Low'].iloc[i-4] and p_l2 < df['Low'].iloc[i-3] and 
            p_l2 < df['Low'].iloc[i-1] and p_l2 < df['Low'].iloc[i]):
            swing_low = p_l2
            swing_low_bar = i - 2

        # Détection BOS / CHOCH & Order Block
        bos_bull = False
        bos_bear = False
        choch_bull = False
        choch_bear = False

        if swing_high > 0 and c > swing_high and (i - swing_high_bar <= 30):
            if trend >= 0:
                bos_bull = True
            else:
                choch_bull = True
            trend = 1
            # Capture Bullish Order Block (dernière bougie rouge avant l'impulsion)
            for b in range(1, min(6, i)):
                if df['Close'].iloc[i-b] < df['Open'].iloc[i-b]:
                    active_obs.append({
                        'is_bull': True,
                        'top': df['High'].iloc[i-b],
                        'bottom': df['Low'].iloc[i-b],
                        'bar': i
                    })
                    break
            swing_high = h

        elif swing_low > 0 and c < swing_low and (i - swing_low_bar <= 30):
            if trend <= 0:
                bos_bear = True
            else:
                choch_bear = True
            trend = -1
            # Capture Bearish Order Block (dernière bougie verte avant la cassure)
            for b in range(1, min(6, i)):
                if df['Close'].iloc[i-b] > df['Open'].iloc[i-b]:
                    active_obs.append({
                        'is_bull': False,
                        'top': df['High'].iloc[i-b],
                        'bottom': df['Low'].iloc[i-b],
                        'bar': i
                    })
                    break
            swing_low = l

        # Détection Fair Value Gap (FVG)
        if i >= 2:
            l0 = df['Low'].iloc[i]
            h2 = df['High'].iloc[i-2]
            h0 = df['High'].iloc[i]
            l2 = df['Low'].iloc[i-2]
            if l0 > h2:
                active_fvgs.append({'is_bull': True, 'top': l0, 'bottom': h2, 'bar': i, 'mitigated': False})
            if h0 < l2:
                active_fvgs.append({'is_bull': False, 'top': l2, 'bottom': h0, 'bar': i, 'mitigated': False})

        # Nettoyage vieux FVG / OB (> 40 barres)
        active_fvgs = [f for f in active_fvgs if (i - f['bar'] <= 40)]
        active_obs = [ob for ob in active_obs if (i - ob['bar'] <= 40)]

        # Vérification Retest FVG / OB
        retest_fvg_bull = any(f['is_bull'] and not f['mitigated'] and (l <= f['top'] and h >= f['bottom']) for f in active_fvgs)
        retest_fvg_bear = any(not f['is_bull'] and not f['mitigated'] and (h >= f['bottom'] and l <= f['top']) for f in active_fvgs)
        retest_ob_bull = any(ob['is_bull'] and (l <= ob['top'] and h >= ob['bottom']) for ob in active_obs)
        retest_ob_bear = any(not ob['is_bull'] and (h >= ob['bottom'] and l <= ob['top']) for ob in active_obs)

        # Liquidity Sweep
        min_pierce = atr * 0.05
        sweep_bull = (swing_low > 0 and l < swing_low - min_pierce and c > swing_low and (i - swing_low_bar <= 25))
        sweep_bear = (swing_high > 0 and h > swing_high + min_pierce and c < swing_high and (i - swing_high_bar <= 25))

        # Rejection Wick
        candle_range = max(0.1, h - l)
        wick_bottom = (min(o, c) - l) / candle_range
        wick_top = (h - max(o, c)) / candle_range

        # Évaluation des setups candidats potentiels
        # LONG CANDIDATES
        is_long_candidate = False
        setup_name_long = ""
        ref_level_long = l

        if sweep_bull and wick_bottom >= 0.35:
            is_long_candidate = True
            setup_name_long = "REVERSAL_SWEEP"
            ref_level_long = swing_low
        elif retest_ob_bull or retest_fvg_bull:
            is_long_candidate = True
            setup_name_long = "PULLBACK_OB_FVG"
            ref_level_long = l
        elif (c > vah and df['Close'].iloc[i-1] <= vah and vol_rank >= 70):
            is_long_candidate = True
            setup_name_long = "BREAKOUT_VAH"
            ref_level_long = vah
        elif (z_delta >= 1.5 and c > o and vol_rank >= 65):
            is_long_candidate = True
            setup_name_long = "DELTA_FLIP"
            ref_level_long = l
        elif (l <= (vwap - sd2) and c > (vwap - sd2) and wick_bottom >= 0.40):
            is_long_candidate = True
            setup_name_long = "VWAP_SD2_REVERSAL"
            ref_level_long = vwap - sd2
        elif (l <= val and c > val and wick_bottom >= 0.35):
            is_long_candidate = True
            setup_name_long = "POC_VAL_REJECTION"
            ref_level_long = val

        # SHORT CANDIDATES
        is_short_candidate = False
        setup_name_short = ""
        ref_level_short = h

        if sweep_bear and wick_top >= 0.35:
            is_short_candidate = True
            setup_name_short = "REVERSAL_SWEEP"
            ref_level_short = swing_high
        elif retest_ob_bear or retest_fvg_bear:
            is_short_candidate = True
            setup_name_short = "PULLBACK_OB_FVG"
            ref_level_short = h
        elif (c < val and df['Close'].iloc[i-1] >= val and vol_rank >= 70):
            is_short_candidate = True
            setup_name_short = "BREAKDOWN_VAL"
            ref_level_short = val
        elif (z_delta <= -1.5 and c < o and vol_rank >= 65):
            is_short_candidate = True
            setup_name_short = "DELTA_FLIP"
            ref_level_short = h
        elif (h >= (vwap + sd2) and c < (vwap + sd2) and wick_top >= 0.40):
            is_short_candidate = True
            setup_name_short = "VWAP_SD2_REVERSAL"
            ref_level_short = vwap + sd2
        elif (h >= vah and c < vah and wick_top >= 0.35):
            is_short_candidate = True
            setup_name_short = "POC_VAH_REJECTION"
            ref_level_short = vah

        # Scoring Scalping Pro Model
        # Evaluation LONG
        if is_long_candidate:
            # 1. Structure (30 pts)
            smc_pts = 0.0
            if bos_bull: smc_pts += 8.0
            if choch_bull: smc_pts += 7.0
            if retest_ob_bull: smc_pts += 6.0
            if sweep_bull: smc_pts += 6.0
            if retest_fvg_bull: smc_pts += 5.0
            struct_score = min(30.0, (smc_pts / 20.0) * 30.0 if smc_pts > 0 else 12.0)

            # 2. Footprint / Order flow (30 pts)
            fp_pts = 0.0
            if z_delta >= 1.0: fp_pts += min(15.0, z_delta * 7.5)
            if wick_bottom >= 0.30: fp_pts += min(10.0, wick_bottom * 20.0)
            if vol_rank >= 70: fp_pts += 5.0
            footprint_score = min(30.0, fp_pts)

            # 3. Volume & Momentum (15 pts + 15 pts = 30 pts)
            vol_score = min(15.0, (vol_rank / 100.0) * 15.0)
            mom_score = min(15.0, max(0.0, (c - o) / atr) * 15.0)

            # 4. Contexte (10 pts)
            ctx_score = 8.0 if c > vwap or setup_name_long.startswith("VWAP") else 4.0

            # 5. Pénalités
            pen = 0.0
            if h >= (vwap + sd3): pen -= 15.0  # Plafond macro
            if vol_rank <= 20: pen -= 5.0

            # Bonus setups fiables
            bonus = 3.0 if "DELTA_FLIP" in setup_name_long or "SWEEP" in setup_name_long else 0.0

            total_score = min(100.0, max(0.0, struct_score + footprint_score + vol_score + mom_score + ctx_score + pen + bonus))

            if total_score >= 45.0:
                # Stop loss dynamique ATR + ancrage structure
                stop_dist = max(atr * STOP_ATR_MULT + STOP_BUFFER_TICKS * TICK_SIZE, MIN_STOP_TICKS * TICK_SIZE)
                stop_dist = min(stop_dist, MAX_STOP_TICKS * TICK_SIZE)
                stop_price = round(min(c - stop_dist, ref_level_long - STOP_BUFFER_TICKS * TICK_SIZE), 1)
                actual_risk = round(c - stop_price, 2)
                
                if actual_risk >= MIN_STOP_TICKS * TICK_SIZE:
                    target1_price = round(c + actual_risk * 1.0, 1)
                    target2_price = round(c + actual_risk * 2.0, 1)

                    candidates.append({
                        'bar_idx': bar_idx,
                        'datetime': dt_str,
                        'side': 'LONG',
                        'setup': setup_name_long,
                        'entry': c,
                        'stop': stop_price,
                        'target1': target1_price,
                        'target2': target2_price,
                        'risk': actual_risk,
                        'score': round(total_score, 1),
                        'grade': 'TRES_FORT' if total_score >= 65 else 'FORT',
                        'atr': round(atr, 2)
                    })

        # Evaluation SHORT
        if is_short_candidate:
            smc_pts = 0.0
            if bos_bear: smc_pts += 8.0
            if choch_bear: smc_pts += 7.0
            if retest_ob_bear: smc_pts += 6.0
            if sweep_bear: smc_pts += 6.0
            if retest_fvg_bear: smc_pts += 5.0
            struct_score = min(30.0, (smc_pts / 20.0) * 30.0 if smc_pts > 0 else 12.0)

            fp_pts = 0.0
            if z_delta <= -1.0: fp_pts += min(15.0, abs(z_delta) * 7.5)
            if wick_top >= 0.30: fp_pts += min(10.0, wick_top * 20.0)
            if vol_rank >= 70: fp_pts += 5.0
            footprint_score = min(30.0, fp_pts)

            vol_score = min(15.0, (vol_rank / 100.0) * 15.0)
            mom_score = min(15.0, max(0.0, (o - c) / atr) * 15.0)

            ctx_score = 8.0 if c < vwap or setup_name_short.startswith("VWAP") else 4.0

            pen = 0.0
            if l <= (vwap - sd3): pen -= 15.0  # Plancher macro
            if vol_rank <= 20: pen -= 5.0

            bonus = 3.0 if "DELTA_FLIP" in setup_name_short or "SWEEP" in setup_name_short else 0.0

            total_score = min(100.0, max(0.0, struct_score + footprint_score + vol_score + mom_score + ctx_score + pen + bonus))

            if total_score >= 45.0:
                stop_dist = max(atr * STOP_ATR_MULT + STOP_BUFFER_TICKS * TICK_SIZE, MIN_STOP_TICKS * TICK_SIZE)
                stop_dist = min(stop_dist, MAX_STOP_TICKS * TICK_SIZE)
                stop_price = round(max(c + stop_dist, ref_level_short + STOP_BUFFER_TICKS * TICK_SIZE), 1)
                actual_risk = round(stop_price - c, 2)

                if actual_risk >= MIN_STOP_TICKS * TICK_SIZE:
                    target1_price = round(c - actual_risk * 1.0, 1)
                    target2_price = round(c - actual_risk * 2.0, 1)

                    candidates.append({
                        'bar_idx': bar_idx,
                        'datetime': dt_str,
                        'side': 'SHORT',
                        'setup': setup_name_short,
                        'entry': c,
                        'stop': stop_price,
                        'target1': target1_price,
                        'target2': target2_price,
                        'risk': actual_risk,
                        'score': round(total_score, 1),
                        'grade': 'TRES_FORT' if total_score >= 65 else 'FORT',
                        'atr': round(atr, 2)
                    })

    print(f"-> Total signaux candidats générés (Score >= 45) : {len(candidates)}")

    # 5. Modèle d'exécution institutionnel Scalping Pro
    # - Filtre de session : focus sur les sessions liquides (Londres 03:00-08:00 ET, US 08:20-16:00 ET)
    # - Gestion des 2 cibles : TP1 (50% à 1.0R + passage du Stop à Breakeven), TP2 (50% à 2.0R)
    # - HTF Alignment : validation par tendance M15/M60 et VWAP
    print("Simulation de l'exécution avec gestion multi-cibles TP1/TP2 et trailing Breakeven...")
    
    # Calcul HTF Trend (EMA 50 sur les barres 5-min ~ équivalent EMA 15 sur 15-min)
    df['EMA_HTF'] = df['Close'].ewm(span=50, adjust=False).mean()
    df['HTF_Bull'] = df['Close'] > df['EMA_HTF']

    # Test avec plusieurs scénarios comparatifs pour Gold
    scenarios = [
        {
            "name": "1. Standard Scalping Pro (Stop 1.75 ATR, TP1=1.0R / TP2=2.0R, Score >= 50)",
            "min_score": 50.0, "use_htf": True, "rth_only": True, "stop_atr": 1.75, "tp1_r": 1.0, "tp2_r": 2.0, "max_bars": 24
        },
        {
            "name": "2. Calibré Gold Futures (Stop 2.00 ATR, TP1=1.0R / TP2=2.5R, Score >= 55, RTH/Londres)",
            "min_score": 55.0, "use_htf": True, "rth_only": True, "stop_atr": 2.00, "tp1_r": 1.0, "tp2_r": 2.5, "max_bars": 30
        },
        {
            "name": "3. Ultra Haute Conviction / Grade Or (Stop 2.00 ATR, TP1=1.2R / TP2=3.0R, Score >= 65)",
            "min_score": 65.0, "use_htf": True, "rth_only": True, "stop_atr": 2.00, "tp1_r": 1.2, "tp2_r": 3.0, "max_bars": 36
        },
    ]

    all_scenario_summaries = []

    for sc in scenarios:
        min_score = sc["min_score"]
        use_htf = sc["use_htf"]
        rth_only = sc["rth_only"]
        stop_mult = sc["stop_atr"]
        tp1_r = sc["tp1_r"]
        tp2_r = sc["tp2_r"]
        max_bars_in_trade = sc["max_bars"]
        
        executed_trades = []
        active_long = None
        active_short = None

        for cand in candidates:
            cand_bar = cand['bar_idx']
            if cand['score'] < min_score:
                continue

            dt = df['Datetime'].iloc[cand_bar]
            hour = dt.hour
            minute = dt.minute

            # Filtre de session liquide (Londres 03:00-08:00 ET ou US 08:20-16:30 ET)
            is_liquid_session = (3 <= hour <= 7) or (8 <= hour <= 16 and not (hour == 8 and minute < 20))
            if rth_only and not is_liquid_session:
                continue

            is_buy = cand['side'] == 'LONG'
            htf_aligned = (is_buy and df['HTF_Bull'].iloc[cand_bar]) or (not is_buy and not df['HTF_Bull'].iloc[cand_bar])
            is_reversal = "REVERSAL" in cand['setup'] or "SWEEP" in cand['setup'] or "VWAP" in cand['setup']
            
            if use_htf and not htf_aligned and not is_reversal:
                continue

            entry_price = cand['entry']
            atr_val = cand['atr']
            
            # Recalcul stop & targets selon le scénario
            stop_dist = max(atr_val * stop_mult + STOP_BUFFER_TICKS * TICK_SIZE, MIN_STOP_TICKS * TICK_SIZE)
            stop_dist = min(stop_dist, MAX_STOP_TICKS * TICK_SIZE)
            
            if is_buy:
                stop_price = round(min(entry_price - stop_dist, cand['stop']), 1)
                risk = round(entry_price - stop_price, 2)
                t1_price = round(entry_price + risk * tp1_r, 1)
                t2_price = round(entry_price + risk * tp2_r, 1)
            else:
                stop_price = round(max(entry_price + stop_dist, cand['stop']), 1)
                risk = round(stop_price - entry_price, 2)
                t1_price = round(entry_price - risk * tp1_r, 1)
                t2_price = round(entry_price - risk * tp2_r, 1)

            entry_time = cand['datetime']

            # Filtre anti-doublon
            if is_buy and active_long is not None and active_long['exit_bar'] >= cand_bar:
                continue
            if not is_buy and active_short is not None and active_short['exit_bar'] >= cand_bar:
                continue

            outcome = "TIMEOUT"
            exit_bar = min(total_bars - 1, cand_bar + max_bars_in_trade)
            exit_price = entry_price
            hit_t1 = False
            current_stop = stop_price
            r_mult = 0.0

            for f_bar in range(cand_bar + 1, min(total_bars, cand_bar + max_bars_in_trade + 1)):
                f_row = df.iloc[f_bar]
                f_h = f_row['High']
                f_l = f_row['Low']

                if is_buy:
                    if f_l <= current_stop:
                        exit_bar = f_bar
                        exit_price = current_stop
                        if hit_t1:
                            # 50% TP1 + 50% BE
                            outcome = "TARGET1_BE"
                            r_mult = tp1_r * 0.50
                        else:
                            outcome = "STOP"
                            r_mult = -1.00
                        break

                    if f_h >= t2_price:
                        outcome = "TARGET2"
                        exit_bar = f_bar
                        exit_price = t2_price
                        # 50% TP1 + 50% TP2
                        r_mult = (tp1_r * 0.50) + (tp2_r * 0.50)
                        break

                    if f_h >= t1_price and not hit_t1:
                        hit_t1 = True
                        current_stop = entry_price + 0.1 # Breakeven lock (+1 tick)

                else: # SHORT
                    if f_h >= current_stop:
                        exit_bar = f_bar
                        exit_price = current_stop
                        if hit_t1:
                            outcome = "TARGET1_BE"
                            r_mult = tp1_r * 0.50
                        else:
                            outcome = "STOP"
                            r_mult = -1.00
                        break

                    if f_l <= t2_price:
                        outcome = "TARGET2"
                        exit_bar = f_bar
                        exit_price = t2_price
                        r_mult = (tp1_r * 0.50) + (tp2_r * 0.50)
                        break

                    if f_l <= t1_price and not hit_t1:
                        hit_t1 = True
                        current_stop = entry_price - 0.1

            if outcome == "TIMEOUT":
                exit_price = df['Close'].iloc[exit_bar]
                pnl = (exit_price - entry_price) if is_buy else (entry_price - exit_price)
                norm_r = pnl / risk
                if hit_t1:
                    r_mult = (tp1_r * 0.50) + max(0.0, norm_r * 0.50)
                    outcome = "TARGET1_TIMEOUT"
                else:
                    r_mult = max(-1.0, min(tp2_r, norm_r))
                    outcome = "TIMEOUT"

            trade_record = {
                'entry_bar': cand_bar,
                'exit_bar': exit_bar,
                'entry_time': entry_time,
                'exit_time': str(df['Datetime'].iloc[exit_bar]),
                'side': cand['side'],
                'setup': cand['setup'],
                'score': cand['score'],
                'grade': cand['grade'],
                'entry': entry_price,
                'stop': stop_price,
                'target1': t1_price,
                'target2': t2_price,
                'exit_price': exit_price,
                'risk_pts': risk,
                'outcome': outcome,
                'r_multiple': r_mult,
                'is_win': r_mult > 0.0,
                'is_loss': r_mult < 0.0,
                'duration_min': (exit_bar - cand_bar) * 5
            }

            executed_trades.append(trade_record)
            if is_buy:
                active_long = trade_record
            else:
                active_short = trade_record

        # Stats
        tdf = pd.DataFrame(executed_trades)
        if len(tdf) == 0:
            continue

        n_trades = len(tdf)
        wins = (tdf['r_multiple'] > 0).sum()
        losses = (tdf['r_multiple'] < 0).sum()
        be = (tdf['r_multiple'] == 0).sum()
        wr = (wins / n_trades) * 100.0
        tot_r = tdf['r_multiple'].sum()
        gp = tdf[tdf['r_multiple'] > 0]['r_multiple'].sum()
        gl = abs(tdf[tdf['r_multiple'] < 0]['r_multiple'].sum())
        pf = gp / gl if gl > 0 else float('inf')
        exp_r = tot_r / n_trades
        avg_risk_pts = tdf['risk_pts'].mean()

        summary_item = {
            "name": sc['name'],
            "trades": n_trades,
            "wr": wr,
            "net_r": tot_r,
            "pf": pf,
            "exp_r": exp_r,
            "gc_dollars": tot_r * avg_risk_pts * 100.0,
            "mgc_dollars": tot_r * avg_risk_pts * 10.0,
            "df": tdf
        }
        all_scenario_summaries.append(summary_item)

        print("\n" + "=" * 80)
        print(f"RÉSULTATS : {sc['name']}")
        print("=" * 80)
        print(f"  * Total Trades Exécutés : {n_trades} ({n_trades / 42:.1f} trades / session)")
        print(f"  * Win Rate              : {wr:.2f}% ({wins} W / {losses} L / {be} BE)")
        print(f"  * Gain Net Total        : {tot_r:+.2f} R")
        print(f"  * Gains Bruts           : +{gp:.2f} R")
        print(f"  * Pertes Brutes         : -{gl:.2f} R")
        print(f"  * Profit Factor         : {pf:.2f}")
        print(f"  * Espérance E[R]        : {exp_r:+.3f} R / trade")
        print(f"  * Stop Moyen            : {avg_risk_pts:.2f} pts (${avg_risk_pts * 100:.0f} / contrat plein GC)")
        print(f"  * PnL Estimé (GC Full)  : {tot_r * avg_risk_pts * 100:+,.2f} $")
        print(f"  * PnL Estimé (MGC Micro): {tot_r * avg_risk_pts * 10:+,.2f} $")

        print("\n  --- Breakdown par Setup ---")
        for s, st_df in tdf.groupby('setup'):
            st_w = (st_df['r_multiple'] > 0).sum()
            st_r = st_df['r_multiple'].sum()
            st_wr = (st_w / len(st_df)) * 100.0
            print(f"    * {s:22s} : {len(st_df):3d} trades | Win: {st_wr:5.1f}% | Net R: {st_r:+6.2f} R")

        print("\n  --- Breakdown par Mois ---")
        tdf['Month'] = pd.to_datetime(tdf['entry_time']).dt.strftime('%Y-%m')
        for m, m_df in tdf.groupby('Month'):
            m_w = (m_df['r_multiple'] > 0).sum()
            m_r = m_df['r_multiple'].sum()
            m_wr = (m_w / len(m_df)) * 100.0
            print(f"    * Mois {m} : {len(m_df):3d} trades | Win: {m_wr:5.1f}% | Net R: {m_r:+6.2f} R")

    # Export des résultats
    if all_scenario_summaries:
        out_csv = "shadow/backtest_gc_scalpingpro_outcomes.csv"
        all_scenario_summaries[1]['df'].to_csv(out_csv, sep=';', index=False)
        print(f"\n-> Journal détaillé du Scénario 2 exporté dans : {out_csv}")

    print("=" * 80)

if __name__ == '__main__':
    run_scalping_pro_backtest()
