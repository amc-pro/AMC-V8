import datetime
import os
import sys

def download_and_analyze():
    try:
        import yfinance as yf
        import pandas as pd
    except ImportError:
        print("Waiting for dependencies...")
        return

    tickers = {
        "MNQ": "MNQ=F",
        "NQ": "NQ=F",
        "ES": "ES=F"
    }

    start_date = "2026-08-24"
    end_date = "2026-08-25"

    output_dir = r"c:\AMC-Pro\AMC-V8\historical_data_recent"
    os.makedirs(output_dir, exist_ok=True)

    for name, ticker in tickers.items():
        print(f"Downloading {name} ({ticker}) for {start_date}...")
        try:
            df = yf.download(ticker, start=start_date, end=end_date, interval="1m")
            if not df.empty:
                out_path = os.path.join(output_dir, f"{name}_20260824_1m.csv")
                df.to_csv(out_path)
                print(f"Saved {len(df)} rows to {out_path}")
            else:
                print(f"No data returned for {name}")
        except Exception as e:
            print(f"Error downloading {name}: {e}")

if __name__ == "__main__":
    download_and_analyze()
