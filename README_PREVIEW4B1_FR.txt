PinteMod Control Center — Integration Preview 4B1 Fix15

Cette Preview inaugure le moteur adaptatif :
- PinteMod détecté -> provider PinteMod complet ;
- BOIII sans PinteMod -> provider BOIII natif limité ;
- GSC tiers -> audit read-only borné + capacités observées ;
- Generic/Control Center Bridge détecté -> capacités limitées tant que le runtime n'est pas prouvé.

RÈGLE DE SÉCURITÉ
Une commande trouvée dans un GSC tiers n'est jamais exécutée automatiquement.
"Observé" signifie seulement que l'audit a trouvé un indice dans le code.
"Disponible" exige un provider/contrat explicitement pris en charge.

ICÔNE 4B
L'EXE et les fenêtres utilisent désormais l'icône PinteMod zombie + pinte + tally Zombies.

BUILD
Double-cliquer BUILD_PREVIEW.bat.
Sorties attendues :
- app\artifacts\integration-preview4b1-fix15-win-x64\single-exe\PinteMod.ControlCenter.exe
- app\artifacts\integration-preview4b1-fix15-win-x64\folder\
- app\artifacts\integration-preview4b1-fix15-win-x64\PinteMod.ControlCenter-single-exe-win-x64.zip
- app\artifacts\integration-preview4b1-fix15-win-x64\PinteMod.ControlCenter-folder-win-x64.zip
- app\artifacts\integration-preview4b1-fix15-win-x64\SHA256SUMS.txt

Après build, suivre PREVIEW_INTEGRATION4B1_FIX15_TEST_FR.txt.
