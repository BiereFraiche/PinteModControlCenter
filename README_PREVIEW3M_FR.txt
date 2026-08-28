PINTE MOD CONTROL CENTER - PREVIEW 3M
========================================

CHANGEMENTS PRINCIPAUX
- Une derniere activation locale de l Agent avec 3M.
- A partir de 3M, les prochaines versions de l Agent peuvent etre poussees depuis le Control Center principal via SMB.
- Verifications update: HMAC-SHA256 + SHA-256 + chemin ferme + taille bornee.
- Aucun nouveau port et aucun secret transporte dans l update.
- Le pairing existant est conserve.
- Bridge v0.3.1 : le caractere | est accepte dans le hostname public BOIII.

UTILISATION
1. BUILD_PREVIEW.bat.
2. Une derniere fois, lancer 3M sur le PC serveur puis ACTIVER / MAJ AGENT SUR CE PC.
3. Sur le PC principal, garder 3M. Le statut doit afficher Agent ONLINE 3M.
4. Pour les versions futures, remplacer uniquement l EXE principal puis utiliser METTRE A JOUR L AGENT DISTANT.
5. Pour | dans le hostname, mettre a jour le Bridge v0.3.1 sur chaque serveur cible pendant qu il est arrete.
