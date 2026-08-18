# AMC PRO — Volume Profile Session Boundary Fix

## Scope

Correction du partitionnement temporel des Volume Profiles Daily / Weekly / Monthly.

## Corrections

- Le `Trading Day` n'est plus défini par minuit UTC.
- Conversion des timestamps vers `America/New_York` / `Eastern Standard Time` avec gestion DST via `TimeZoneInfo`.
- RTH : trading date calendaire New York, bornes 09:30 → 16:00 ET.
- ETH/GLOBEX/24 : frontière CME 18:00 ET → 17:00 ET le jour suivant.
- Une ouverture Globex le dimanche à 18:00 ET appartient au trading day du lundi.
- Weekly RTH : lundi 09:30 → vendredi 16:00 ET.
- Weekly ETH : dimanche 18:00 → vendredi 17:00 ET.
- Monthly : bornes alignées sur la convention de session au lieu de minuit UTC.
- Le manager finalise désormais les profils avec les bornes réelles de session, et non avec l'heure d'arrivée de la première barre de la période suivante.
- Suppression du fallback uniforme `barVolume / nombre de ticks` lorsque la distribution volumétrique par prix est absente. Une barre sans `tickVolumes` est ignorée pour éviter de fabriquer un POC/VAH/VAL artificiel.

## Anti-look-ahead

Les profils restent exposés au moteur décisionnel uniquement après clôture de la période. Le profil courant n'est pas utilisé comme `PrevDay` ou `PrevWeek`.

## Tests ajoutés

- RTH Daily boundary
- RTH Weekly close
- ETH Sunday 18:00 → Monday trading date
- ETH Daily close 17:00 ET
- ETH Weekly close Friday 17:00 ET
- Contract test pour l'absence de fallback uniforme

## Limitation de validation

L'environnement de travail ne contient ni `dotnet`, ni les assemblies NinjaTrader, ni MetaEditor. La compilation NinjaTrader/MetaTrader doit donc être exécutée dans les environnements natifs correspondants avant déploiement live.
