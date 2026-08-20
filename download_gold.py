import kagglehub

def download_gold():
    try:
        print("Tentative de téléchargement du dataset Gold 2000-2026...")
        path = kagglehub.dataset_download("hamzasamiullah/gold-price-historical-data-2000-2026")
        print(f"Téléchargement réussi ! Chemin : {path}")
    except Exception as e:
        print(f"Échec du téléchargement : {e}")

if __name__ == "__main__":
    download_gold()
