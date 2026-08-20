import kagglehub

def download_nq():
    try:
        print("Tentative de téléchargement du dataset NQ 2022-2025...")
        path = kagglehub.dataset_download("tgtanalytics/nq-futures-1min-bar-2022-2025")
        print(f"Téléchargement réussi ! Chemin : {path}")
    except Exception as e:
        print(f"Échec du téléchargement : {e}")

if __name__ == "__main__":
    download_nq()
