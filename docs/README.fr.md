# Accessible Arena

Mod d'accessibilité pour Magic: The Gathering Arena permettant aux joueurs aveugles et malvoyants de jouer à l'aide d'un lecteur d'écran. Navigation complète au clavier, annonces du lecteur d'écran pour tous les états de jeu et localisation en 12 langues.

**Statut :** Bêta publique. Le gameplay principal est fonctionnel. Quelques cas particuliers et bugs mineurs subsistent. Voir Problèmes connus ci-dessous.

**Remarque :** Clavier uniquement pour l'instant. Pas de support souris ou tactile. Testé uniquement sous Windows 11 avec NVDA. D'autres versions de Windows et lecteurs d'écran (JAWS, Narrator, etc.) pourraient fonctionner mais ne sont pas testés.

## Fonctionnalités

- Navigation complète au clavier pour tous les écrans (accueil, boutique, maîtrise, constructeur de deck, duels)
- Intégration du lecteur d'écran via la bibliothèque Tolk
- Lecture des informations de carte avec les touches fléchées (nom, coût de mana, type, force/endurance, texte de règles, texte d'ambiance, rareté, artiste)
- Support complet des duels : navigation par zones, combat, ciblage, pile, navigateurs (regard, surveillance, mulligan)
- Annonces des relations d'attachement et de combat (enchanté par, bloque, ciblé par)
- Boutique accessible avec options d'achat et support des dialogues de paiement
- Support des matchs contre des bots pour s'entraîner
- Menu des paramètres (F2) et menu d'aide (F1) disponibles partout
- 12 langues : anglais, allemand, français, espagnol, italien, portugais (BR), japonais, coréen, russe, polonais, chinois simplifié, chinois traditionnel

## Prérequis

- Windows 10 ou ultérieur
- Magic: The Gathering Arena (installé via l'installateur officiel ou l'Epic Games Store)
- Un lecteur d'écran (NVDA recommandé : https://www.nvaccess.org/download/)
- MelonLoader (l'installateur gère cela automatiquement)

## Installation

### Avec l'installateur (recommandé)

1. Téléchargez `AccessibleArenaInstaller.exe` depuis la dernière version sur GitHub : https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. Fermez MTG Arena s'il est en cours d'exécution
3. Exécutez l'installateur. Il détectera votre installation MTGA, installera MelonLoader si nécessaire et déploiera le mod
4. Lancez MTG Arena. Vous devriez entendre « Accessible Arena v... lancé » via votre lecteur d'écran

### Installation manuelle

1. Installez MelonLoader dans votre dossier MTGA (https://github.com/LavaGang/MelonLoader)
2. Téléchargez `AccessibleArena.dll` depuis la dernière version
3. Copiez la DLL dans : `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. Assurez-vous que `Tolk.dll` et `nvdaControllerClient64.dll` sont dans le dossier racine de MTGA
5. Lancez MTG Arena

## Démarrage rapide

Si vous n'avez pas encore de compte Wizards, vous pouvez en créer un sur https://myaccounts.wizards.com/ au lieu d'utiliser l'écran d'inscription dans le jeu.

Après l'installation, lancez MTG Arena. Le mod annonce l'écran actuel via votre lecteur d'écran.

- Appuyez sur **F1** à tout moment pour un menu d'aide navigable listant tous les raccourcis clavier
- Appuyez sur **F2** pour le menu des paramètres (langue, verbosité, messages du tutoriel)
- Appuyez sur **F3** pour entendre le nom de l'écran actuel
- Utilisez **Flèche haut/bas** ou **Tab/Maj+Tab** pour naviguer dans les menus
- Appuyez sur **Entrée** ou **Espace** pour activer les éléments
- Appuyez sur **Retour arrière** pour revenir en arrière

## Raccourcis clavier

### Menus

- Flèche haut/bas (ou W/S) : Naviguer dans les éléments
- Tab/Maj+Tab : Naviguer dans les éléments (identique à Flèche haut/bas)
- Flèche gauche/droite (ou A/D) : Contrôles de carrousel et de pas
- Début/Fin : Aller au premier/dernier élément
- Page précédente/Page suivante : Page précédente/suivante dans la collection
- Entrée/Espace : Activer
- Retour arrière : Revenir

### Duels - Zones

- C : Votre main
- G / Maj+G : Votre cimetière / Cimetière adverse
- X / Maj+X : Votre exil / Exil adverse
- S : Pile
- B / Maj+B : Vos créatures / Créatures adverses
- A / Maj+A : Vos terrains / Terrains adverses
- R / Maj+R : Vos non-créatures / Non-créatures adverses

### Duels - Dans les zones

- Gauche/Droite : Naviguer entre les cartes
- Début/Fin : Aller à la première/dernière carte
- Flèche haut/bas : Lire les détails de la carte quand elle est en focus
- I : Info étendue de la carte (descriptions des mots-clés, autres faces)
- Maj+Haut/Bas : Changer de rangée sur le champ de bataille

### Duels - Informations

- T : Tour et phase actuels
- L : Totaux de points de vie
- V : Zone d'info joueur (Gauche/Droite pour changer de joueur, Haut/Bas pour les propriétés)
- D / Maj+D : Nombre de cartes dans votre bibliothèque / Bibliothèque adverse
- Maj+C : Nombre de cartes en main adverse

### Duels - Actions

- Espace : Confirmer (passer la priorité, confirmer attaquants/bloqueurs, phase suivante)
- Retour arrière : Annuler / refuser
- Tab : Parcourir les cibles ou éléments en surbrillance
- Ctrl+Tab : Parcourir uniquement les cibles adverses
- Entrée : Sélectionner la cible

### Duels - Navigateurs (Regard, Surveillance, Mulligan)

- Tab : Naviguer entre toutes les cartes
- C/D : Aller à la zone du dessus/dessous
- Gauche/Droite : Naviguer dans la zone
- Entrée : Basculer le placement de la carte
- Espace : Confirmer la sélection
- Retour arrière : Annuler

### Global

- F1 : Menu d'aide
- F2 : Menu des paramètres
- F3 : Annoncer l'écran actuel
- Ctrl+R : Répéter la dernière annonce
- Retour arrière : Retour/fermer/annuler universel

## Signaler des bugs

Si vous trouvez un bug, veuillez ouvrir un ticket sur GitHub : https://github.com/JeanStiletto/AccessibleArena/issues

Incluez les informations suivantes :

- Ce que vous faisiez quand le bug s'est produit
- Ce que vous attendiez
- Ce qui s'est réellement passé
- Votre lecteur d'écran et sa version
- Joignez le fichier journal MelonLoader : `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## Problèmes connus

- La touche Espace pour passer la priorité n'est pas toujours fiable (le mod clique directement sur le bouton en secours)
- Les cartes de la liste de deck dans le constructeur n'affichent que le nom et la quantité, pas les détails complets
- La sélection du type de file d'attente PlayBlade (Classé, Jeu libre, Brawl) ne définit pas toujours le bon mode de jeu

Pour la liste complète, voir docs/KNOWN_ISSUES.md.

## Dépannage

**Pas de sortie vocale après le lancement du jeu**
- Assurez-vous que votre lecteur d'écran est en cours d'exécution avant de lancer MTG Arena
- Vérifiez que `Tolk.dll` et `nvdaControllerClient64.dll` sont dans le dossier racine de MTGA (l'installateur les place automatiquement)
- Vérifiez le journal MelonLoader à `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log` pour les erreurs

**Le jeu plante au démarrage ou le mod ne se charge pas**
- Assurez-vous que MelonLoader est installé.
- Si le jeu a été mis à jour récemment, MelonLoader ou le mod doivent peut-être être réinstallés. Exécutez à nouveau l'installateur.
- Vérifiez que `AccessibleArena.dll` est dans `C:\Program Files\Wizards of the Coast\MTGA\Mods\`

**Le mod fonctionnait mais a cessé après une mise à jour du jeu**
- Les mises à jour de MTG Arena peuvent écraser les fichiers MelonLoader. Exécutez à nouveau l'installateur pour réinstaller MelonLoader et le mod.
- Si le jeu a considérablement changé sa structure interne, le mod peut nécessiter une mise à jour. Vérifiez les nouvelles versions sur GitHub.

**Les raccourcis clavier ne fonctionnent pas**
- Assurez-vous que la fenêtre du jeu est au premier plan (cliquez dessus ou utilisez Alt+Tab)
- Appuyez sur F1 pour vérifier si le mod est actif. Si vous entendez le menu d'aide, le mod fonctionne.
- Certains raccourcis ne fonctionnent que dans des contextes spécifiques (les raccourcis de duel uniquement pendant un duel)

**Mauvaise langue**
- Appuyez sur F2 pour ouvrir le menu des paramètres, puis utilisez Entrée pour parcourir les langues

## Compilation depuis les sources

Prérequis : SDK .NET (toute version supportant le ciblage net472)

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

La DLL compilée sera à `src/bin/Debug/net472/AccessibleArena.dll`.

Les références d'assembly du jeu sont attendues dans le dossier `libs/`. Copiez ces DLLs depuis votre installation MTGA (`MTGA_Data/Managed/`) :
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

Les DLLs MelonLoader (`MelonLoader.dll`, `0Harmony.dll`) proviennent de votre installation MelonLoader.

## Licence

Ce projet est sous licence GNU General Public License v3.0. Voir le fichier LICENSE pour les détails.

## Liens

- GitHub : https://github.com/JeanStiletto/AccessibleArena
- Lecteur d'écran NVDA (recommandé) : https://www.nvaccess.org/download/
- MelonLoader : https://github.com/LavaGang/MelonLoader
- MTG Arena : https://magic.wizards.com/mtgarena
