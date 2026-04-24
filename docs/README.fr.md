<h1>Accessible Arena</h1>

<h2>Qu'est-ce que ce mod</h2>

Ce mod te permet de jouer à Arena, la représentation numérique la plus populaire et la plus accessible aux débutants du jeu de cartes à collectionner Magic: The Gathering. Il ajoute un support complet des lecteurs d'écran et la navigation au clavier à presque tous les aspects du jeu.

Le mod prend en charge toutes les langues dans lesquelles le jeu est traduit. De plus, quelques langues que le jeu lui-même ne prend pas en charge sont partiellement couvertes : dans celles-ci, les annonces spécifiques au mod comme les textes d'aide et les indications de l'interface sont traduites, tandis que les données des cartes et du jeu restent dans la langue par défaut du jeu.

<h2>Qu'est-ce que Magic: The Gathering</h2>

Magic est un jeu de cartes à collectionner déposé par Wizards of the Coast qui permet de jouer en tant que mage contre d'autres mages, en lançant des sorts représentés par les cartes. Il existe 5 couleurs dans Magic qui représentent différentes identités de gameplay et d'ambiance. Si tu connais Hearthstone ou Yu-Gi-Oh, tu reconnaîtras beaucoup de concepts, car Magic est l'ancêtre de tous ces jeux.
Si tu veux en apprendre davantage sur Magic en général, le site officiel du jeu ainsi que de nombreux créateurs de contenu pourront t'aider.

<h2>Prérequis</h2>

- Windows 10 ou ultérieur
- Magic: The Gathering Arena (installé via l'installeur officiel de Wizards ou via Steam)
- Un lecteur d'écran (seuls NVDA et JAWS sont testés)
- MelonLoader (l'installeur s'en occupe automatiquement)

<h2>Installation</h2>

<h3>Avec l'installeur (recommandé)</h3>

1. [Télécharge AccessibleArenaInstaller.exe](https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe) depuis la dernière version sur GitHub
2. Ferme MTG Arena s'il est en cours d'exécution
3. Lance l'installeur. Il détectera ton installation de MTGA, installera MelonLoader si nécessaire et déploiera le mod
4. Lance MTG Arena. Tu devrais entendre « Accessible Arena v... launched » via ton lecteur d'écran

<h3>Installation manuelle</h3>

1. Installe [MelonLoader](https://github.com/LavaGang/MelonLoader) dans ton dossier MTGA
2. Télécharge `AccessibleArena.dll` depuis la dernière version
3. Copie la DLL dans ton dossier Mods de MTGA :
   - Installation WotC : `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
   - Installation Steam : `C:\Program Files (x86)\Steam\steamapps\common\MTGA\Mods\`
4. Assure-toi que `Tolk.dll` et `nvdaControllerClient64.dll` sont dans le dossier racine de MTGA
5. Lance MTG Arena

<h2>Désinstallation</h2>

Relance l'installeur. Si le mod est déjà installé, il te proposera une option de désinstallation. Tu peux optionnellement supprimer aussi MelonLoader. Pour désinstaller manuellement, supprime `AccessibleArena.dll` du dossier `Mods\` et retire `Tolk.dll` ainsi que `nvdaControllerClient64.dll` du dossier racine de MTGA.

<h2>Si tu viens de Hearthstone</h2>

Si tu as joué à Hearthstone Access, tu reconnaîtras beaucoup de choses pour de bonnes raisons, car non seulement les principes de jeu sont proches, mais j'ai aussi suivi beaucoup de principes de conception. Cependant, certaines choses sont différentes.

D'abord, tu as plus de zones à parcourir, car Magic possède le cimetière, l'exil et quelques zones supplémentaires. Ton champ de bataille n'est pas limité en taille et dispose de lignes de tri supplémentaires pour rendre plus gérable la masse d'éléments qui peuvent y apparaître.

Ton mana n'augmente pas automatiquement mais provient de cartes de terrain de différentes couleurs que tu dois jouer activement. De ce fait, les coûts de mana ont des parties incolores et colorées qui, additionnées, donnent l'exigence totale de coût d'une carte que tu dois remplir.

Tu ne peux pas attaquer les créatures directement, seuls les adversaires et certaines cartes très spécifiques (arpenteurs et batailles) peuvent être ciblés par les attaquants. En tant que défenseur, tu dois décider si tu veux bloquer une attaque pour faire combattre les créatures. Si tu ne bloques pas, les dégâts toucheront ton avatar de joueur mais tes créatures pourront rester intactes. De plus, les dégâts ne s'accumulent pas sur les créatures mais sont soignés à la fin de chaque tour, donc aussi à la fin de ton tour et du tour de l'adversaire. Pour interagir avec les créatures de l'adversaire qui refusent de se battre, tu dois jouer des cartes spécifiques ou mettre tellement de pression sur les points de vie de ton adversaire qu'il n'aura d'autre choix que de sacrifier de précieuses créatures pour survivre.

Le jeu possède des phases de combat très distinctes qui permettent des actions spécifiques comme piocher, lancer des sorts ou combattre. De ce fait, Magic permet et encourage à faire des choses pendant le tour de l'adversaire. Finies les attentes passives pendant que des choses se produisent. Joue un deck interactif et détruis les plans adverses à la volée.

<h2>Premiers pas</h2>

Le jeu te demande d'abord de fournir quelques données sur toi et d'enregistrer un personnage. Cela devrait fonctionner via les mécanismes internes du jeu, mais si ce n'est pas le cas, tu peux alternativement utiliser le site web du jeu pour le faire, il est entièrement accessible.

Le jeu commence par un tutoriel où tu apprends les bases de Magic: The Gathering. Le mod ajoute des indications de tutoriel personnalisées pour les utilisateurs de lecteur d'écran en parallèle du tutoriel standard. Après avoir terminé le tutoriel, tu es récompensé avec 5 decks de départ, un pour chaque couleur.

À partir de là, tu as plusieurs options pour débloquer plus de cartes et apprendre le jeu :

- **Défis de couleur :** Joue le défi de couleur pour chacune des cinq couleurs de Magic. Chaque défi t'oppose à 4 adversaires PNJ, suivi d'un match contre un vrai joueur à la fin.
- **Événements deck de départ :** Joue l'un des 10 decks bicolores contre de vrais joueurs qui ont les mêmes choix de decks disponibles.
- **Jump In :** Choisis deux lots de 20 cartes de couleurs et thèmes différents, combine-les en un deck et joue contre de vrais joueurs avec des choix similaires. Tu reçois des jetons gratuits pour cet événement et tu gardes les cartes choisies.
- **Échelle d'étincelles :** À un certain moment, l'échelle d'étincelles se débloque, où tu joues tes premiers matchs classés contre de vrais adversaires.

Vérifie ton courrier dans le menu social car il contient beaucoup de récompenses et de boosters de cartes.

Le jeu débloque les modes progressivement en fonction de ce que tu joues et combien tu joues. Il te donne des indications et des quêtes dans le menu progression et objectifs, et met en évidence les modes pertinents pour toi dans le menu jouer. Une fois que tu as suffisamment terminé le contenu nouveau joueur, tous les différents modes et événements deviennent entièrement disponibles.

Dans le Codex du Multivers, tu peux en apprendre plus sur les modes de jeu et les mécaniques. Il s'enrichit avec la progression dans l'expérience NPE.

Sous paramètres du compte, tu peux sauter toutes les expériences tutoriel et tout débloquer de force pour avoir une liberté totale dès le départ. Cependant, jouer les événements nouveau joueur te donne beaucoup de cartes et est recommandé pour les nouveaux joueurs. Ne débloque tout tôt que si tu sais déjà ce que tu fais. Sinon, le contenu débutant apporte plein de plaisir et d'apprentissage tout en te guidant bien.

<h2>Raccourcis clavier</h2>

La navigation suit des conventions standard partout : flèches pour se déplacer, Début/Fin pour aller au premier/dernier, Entrée pour sélectionner, Espace pour confirmer, Retour arrière pour revenir ou annuler. Tab/Maj+Tab fonctionne aussi pour la navigation. Page précédente/suivante change de page.

<h3>Global</h3>

- F1 : Menu d'aide (liste tous les raccourcis de l'écran actuel)
- Ctrl+F1 : Annoncer les raccourcis de l'écran actuel
- F2 : Paramètres du mod
- F3 : Annoncer l'écran actuel
- F4 : Panneau des amis (depuis les menus) / Chat de duel (pendant les duels)
- F5 : Vérifier / démarrer la mise à jour
- Ctrl+R : Répéter la dernière annonce

<h3>Duels - Zones</h3>

Tes zones : C (Main), G (Cimetière), X (Exil), S (Pile), W (Zone de commandement)
Zones de l'adversaire : Shift+G, Shift+X, Shift+W
Champ de bataille : B / Shift+B (Créatures), A / Shift+A (Terrains), R / Shift+R (Non-créatures)
Dans les zones : Gauche/Droite pour naviguer, Haut/Bas pour lire les détails de la carte, I pour les infos étendues
Shift+Haut/Bas : Changer de ligne sur le champ de bataille

<h3>Duels - Informations</h3>

- T : Tour/Phase
- L : Points de vie
- V : Zone d'info du joueur
- D / Shift+D : Compteurs de bibliothèque
- Shift+C : Nombre de cartes en main de l'adversaire
- M / Shift+M : Résumé des terrains de toi / de l'adversaire
- K : Infos des marqueurs sur la carte ciblée
- O : Journal de partie (annonces récentes du duel)
- E / Shift+E : Chronomètre de toi / de l'adversaire

<h3>Duels - Ciblage et actions</h3>

- Tab / Ctrl+Tab : Parcourir les cibles (toutes / adversaire uniquement)
- Entrée : Sélectionner la cible
- Espace : Passer la priorité, confirmer attaquants/bloqueurs, avancer la phase

<h3>Duels - Full control et arrêts de phase</h3>

- P : Basculer full control (temporaire, réinitialisé au changement de phase)
- Shift+P : Basculer full control verrouillé (permanent)
- Shift+Retour arrière : Basculer passer jusqu'à action de l'adversaire (skip léger)
- Ctrl+Retour arrière : Basculer sauter le tour (forcer à sauter tout le tour)
- 1-0 : Basculer les arrêts de phase (1=Entretien, 2=Pioche, 3=Première phase principale, 4=Début du combat, 5=Déclaration des attaquants, 6=Déclaration des bloqueurs, 7=Dégâts de combat, 8=Fin du combat, 9=Deuxième phase principale, 0=Étape de fin)

<h3>Duels - Navigateurs (Scry, Surveiller, Mulligan)</h3>

- Tab : Naviguer sur toutes les cartes
- C/D : Sauter entre zones haut/bas
- Entrée : Basculer le placement de la carte

<h2>Dépannage</h2>

<h3>Pas de sortie vocale après le lancement du jeu</h3>

- Assure-toi que ton lecteur d'écran est lancé avant de lancer MTG Arena
- Vérifie que `Tolk.dll` et `nvdaControllerClient64.dll` sont dans le dossier racine de MTGA (l'installeur les place automatiquement)
- Vérifie le journal MelonLoader dans ton dossier MTGA (`MelonLoader\Latest.log`) pour des erreurs

<h3>Le jeu plante au démarrage ou le mod ne se charge pas</h3>

- Assure-toi que MelonLoader est installé.
- Si le jeu a été récemment mis à jour, MelonLoader ou le mod doit peut-être être réinstallé. Relance l'installeur.
- Vérifie que `AccessibleArena.dll` est dans le dossier `Mods\` à l'intérieur de ton installation MTGA

<h3>Le mod fonctionnait mais a arrêté après une mise à jour du jeu</h3>

- Les mises à jour de MTG Arena peuvent écraser les fichiers de MelonLoader. Relance l'installeur pour réinstaller MelonLoader et le mod.
- Si le jeu a modifié sa structure interne de manière significative, le mod peut nécessiter une mise à jour. Vérifie les nouvelles versions sur GitHub.

<h3>Les raccourcis clavier ne fonctionnent pas</h3>

- Assure-toi que la fenêtre du jeu est au premier plan (clique dessus ou Alt+Tab)
- Appuie sur F1 pour vérifier si le mod est actif. Si tu entends le menu d'aide, le mod fonctionne.
- Certains raccourcis ne fonctionnent que dans des contextes spécifiques (les raccourcis de duel ne fonctionnent qu'en duel)

<h3>Mauvaise langue</h3>

- Appuie sur F2 pour ouvrir le menu des paramètres, puis utilise Entrée pour parcourir les langues

<h3>Windows avertit que l'installeur ou la DLL n'est pas sûr</h3>

L'installeur et la DLL du mod ne sont pas signés numériquement. Les certificats de signature de code coûtent quelques centaines d'euros par an, ce qui n'est pas réaliste pour un projet d'accessibilité gratuit. En conséquence, Windows SmartScreen et certains antivirus t'avertiront lors du premier lancement de l'installeur, ou signaleront la DLL comme « éditeur inconnu ».

Pour vérifier que le fichier téléchargé correspond à celui publié sur GitHub, chaque version indique une somme de contrôle SHA256 pour `AccessibleArenaInstaller.exe` et `AccessibleArena.dll`. Tu peux calculer le hachage de ton fichier téléchargé et comparer :

- PowerShell : `Get-FileHash <nomfichier> -Algorithm SHA256`
- Invite de commandes : `certutil -hashfile <nomfichier> SHA256`

Si le hachage correspond à celui indiqué dans les notes de version, le fichier est authentique. Pour lancer l'installeur malgré l'avertissement SmartScreen, choisis « Informations complémentaires » puis « Exécuter quand même ».

<h2>Signaler des bugs</h2>

Si tu trouves un bug, tu peux poster là où tu as trouvé le mod publié, ou [ouvrir une issue sur GitHub](https://github.com/JeanStiletto/AccessibleArena/issues).

Inclus les informations suivantes :

- Ce que tu étais en train de faire quand le bug s'est produit
- Ce à quoi tu t'attendais
- Ce qui s'est réellement passé
- Si tu veux joindre un journal de jeu, ferme le jeu et partage le fichier journal MelonLoader depuis ton dossier MTGA :
  - WotC : `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
  - Steam : `C:\Program Files (x86)\Steam\steamapps\common\MTGA\MelonLoader\Latest.log`

<h2>Problèmes connus</h2>
Le jeu devrait couvrir presque chaque écran du jeu, mais il peut y avoir quelques cas limites qui ne fonctionnent pas entièrement. PayPal bloque les utilisateurs aveugles avec un captcha non audio illégal, donc tu dois utiliser l'aide d'une personne voyante ou d'autres moyens de paiement si tu veux dépenser de l'argent réel dans le jeu.
Certains événements spécifiques peuvent ne pas être entièrement fonctionnels. Le draft avec de vrais joueurs a un écran de lobby pas encore pris en charge, mais en quickdraft tu choisis des cartes contre des bots avant d'affronter des adversaires humains, ce mode est fonctionnel et recommandé pour quiconque aime ce type d'expérience. Le mode Cube n'est pas traité. Je ne sais même pas vraiment de quoi il s'agit et cela coûte beaucoup de ressources du jeu. Je m'en occuperai si j'ai le temps ou sur demande.
Le système cosmétique du jeu avec Émotes, Familiers, styles de cartes et titres n'est pris en charge que partiellement pour le moment.
Le mod est testé uniquement sous Windows avec NVDA et JAWS et dépend toujours de la bibliothèque Tolk non modifiée. Je ne peux pas tester la compatibilité Mac ou Linux ici, et les bibliothèques multiplateformes comme Prism ne supportaient pas entièrement les anciennes versions de .NET dont dépend le jeu à ce stade. Je ne passerai à une bibliothèque plus large que si des personnes peuvent aider à tester soit d'autres plateformes, soit des lecteurs d'écran asiatiques qui ne sont pas entièrement supportés par Tolk non modifié. N'hésite donc pas à me contacter si tu veux que je travaille là-dessus.

Pour la liste actuelle des problèmes connus, voir [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

<h2>Avertissements</h2>
<h3>Autres formes d'accessibilité</h3>

Ce mod s'appelle Accessible Arena surtout parce que ça sonne bien. Mais pour l'instant, c'est uniquement un mod d'accessibilité pour lecteurs d'écran. Je suis absolument intéressé par la prise en charge de plus de handicaps avec ce mod, déficiences visuelles, handicaps moteurs, etc. Mais je n'ai d'expérience qu'en accessibilité pour lecteurs d'écran. En tant que personne entièrement aveugle, par exemple, les questions de couleur et de polices sont totalement abstraites pour moi. Donc si tu veux que quelque chose de ce genre soit implémenté, n'hésite pas à me contacter si tu peux décrire clairement tes besoins et es prêt à m'aider à tester les résultats.
Alors je serai heureux de donner plus de vérité au nom de ce mod.

<h3>Contact avec l'éditeur</h3>

Malheureusement, je n'ai pas réussi à obtenir un aperçu fiable de l'équipe Arena ou de contacts informels avec les développeurs. J'ai donc décidé de sauter leurs canaux officiels de communication pour le moment. En 3 mois de création et de jeu, je n'ai jamais rencontré de système de protection anti-bot, donc je ne pense pas qu'ils puissent nous détecter comme utilisateurs de mod. Mais je ne voulais pas prendre le risque de communiquer sur des canaux officiels en tant qu'individu seul. Alors répandez le mot sur le mod et construisons une grande communauté de valeur. Nous aurons alors une bien meilleure position si nous décidons de les contacter directement. N'essayez juste pas de leur écrire sans en avoir parlé avec moi d'abord. En particulier, ne leur envoyez pas de demandes d'accessibilité native ou d'intégration de mon mod dans leur base de code. Ni l'un ni l'autre n'arrivera dans tous les cas.

<h3>Achats intégrés</h3>

Arena a quelques mécaniques d'argent réel et on peut acheter une monnaie dans le jeu. Ces moyens de paiement sont pour la plupart accessibles, à l'exception de PayPal, qui a inclus une protection captcha dans sa connexion. Tu peux essayer de désinstaller le mod pour l'enregistrement du moyen de paiement et demander l'aide d'une personne voyante, mais même cela n'est pas fiable en raison de leur cauchemar d'accessibilité de captcha, encore plus cassé et mal implémenté par Wizards of the Coast.
Mais les autres moyens de paiement fonctionnent de manière stable. Moi et d'autres avons testé l'achat intégré de choses et l'utilisation du système devrait être sûre. Mais il est absolument possible qu'il y ait des bugs ou même que le mod t'induise en erreur. Pourrait cliquer sur les mauvaises choses, afficher des informations fausses ou incomplètes, faire de mauvaises choses à cause de changements internes d'Arena. Je pourrais tester, mais je ne peux pas garantir à 100 % que tu ne pourrais pas acheter les mauvaises choses avec ton argent réel. Je ne prendrai pas la responsabilité de cela, et étant donné que ce n'est pas un produit officiel d'Arena, l'éditeur du jeu ne le fera pas non plus. Dans ce cas, n'essaie même pas d'obtenir un remboursement, ils ne t'en donneront pas.

<h3>Utilisation de l'IA</h3>

Le code de ce mod a été créé à 100 % avec l'aide de l'agent Claude d'Anthropic, utilisant les modèles Opus : cela a commencé avec le 4.5, la plupart du développement s'est fait sur 4.6, et les dernières étapes avant la sortie ont été faites sur 4.7. Et grâce à mon plus grand contributeur, un peu de Codex aussi. Je suis conscient des problèmes liés à l'utilisation de l'IA. Mais à une époque où tout le monde utilise ces logiciels pour faire beaucoup de choses bien plus douteuses alors que l'industrie du jeu ne nous a pas donné l'accessibilité que nous voulons en termes de qualité ou de quantité, j'ai quand même décidé d'utiliser ces outils.

<h2>Comment contribuer</h2>

Je suis heureux d'accepter des contributions, et avec [blindndangerous](https://github.com/blindndangerous), beaucoup de travail précieux d'une autre personne fait déjà partie de ce mod. Je suis particulièrement intéressé par les améliorations et corrections pour des choses que je ne peux pas tester, comme différentes configurations système, corriger des langues que je ne parle pas, etc. Mais les demandes de fonctionnalités sont aussi les bienvenues. Avant de travailler sur quelque chose, vérifie les problèmes connus.

- Pour les directives générales de contribution, voir [CONTRIBUTING.md](../CONTRIBUTING.md)
- Pour aider aux traductions, voir [CONTRIBUTING_TRANSLATIONS.md](CONTRIBUTING_TRANSLATIONS.md)

<h2>Crédits</h2>

Et maintenant je veux remercier beaucoup de personnes, car heureusement, ce n'était pas juste moi et l'IA dans une boîte noire, mais tout un réseau autour de moi, aidant, donnant du pouvoir, étant simplement social et sympathique.
N'hésite pas à me contacter en message privé si je t'ai oublié ou si tu veux être mentionné sous un autre nom ou ne pas être mentionné.

D'abord, ce travail s'appuie énormément sur celui d'autres personnes qui ont fait le travail pionnier que j'ai juste eu à refaire pour Accessible Arena.
Sur le plan du design, c'est Hearthstone Access dont j'ai pu beaucoup hériter, non seulement parce qu'il est bien connu de tous ceux qui ont joué au jeu, mais parce que c'est une conception d'interface vraiment bonne.
Sur le plan du modding, je veux remercier les membres du Discord de modding de Zax. Vous avez non seulement compris toutes ces choses, tous les outils et procédures que j'ai juste eu à installer et à utiliser. Vous m'avez appris tout ce que j'avais à savoir sur le modding IA, soit directement, soit en discutant des choses en public ou en aidant d'autres débutants. Vous m'avez aussi donné une plateforme et une communauté dans lesquelles moi et mon projet pouvons exister.

Pour d'énormes contributions de code, je veux remercier [blindndangerous](https://github.com/blindndangerous) qui a aussi fait beaucoup de travail sur ce projet. Sur la durée du projet, je pense que j'ai reçu environ 50 PR et plus de lui concernant tous types de problèmes, des petites choses agaçantes à régler aux suggestions UI plus importantes et à l'accessibilité d'écrans entiers du jeu.
Plus grand merci à Ahix qui a créé des [prompts de refactorisation pour de grands projets codés par IA](https://github.com/ahicks92/llm-mod-refactoring-prompts) que j'ai exécutés par-dessus mes propres refactorisations pour garantir la qualité et la maintenabilité du code.

Pour le test des bêtas, les retours et les idées, je veux remercier :
- Alfi
- Plüschyoda
- Firefly92
- Berenion
- [blindndangerous](https://github.com/blindndangerous)
- Toni Barth
- Chaosbringer216
- ABlindFellow
- SightlessKombat
- hamada
- Zack
- glaroc
- zersiax
- kairos4901
- [patricus3](https://github.com/patricus3)

Pour le test avec des personnes voyantes afin de comprendre les flux visuels et de confirmer certaines choses, je veux remercier :
- [mauriceKA](https://github.com/mauriceKA)
- VeganWolf
- Lea Holstein

<h3>Outils utilisés</h3>

- Claude avec tous les modèles inclus
- MelonLoader
- Harmony pour le patch IL
- Tolk pour la communication avec les lecteurs d'écran
- ILSpy pour décompiler le code du jeu

<h2>Soutiens ton moddeur</h2>

Créer ce mod a été non seulement beaucoup de plaisir et d'émancipation pour moi, mais m'a aussi coûté beaucoup de temps et d'argent réel en abonnements Claude. Je les garderai pour travailler sur d'autres améliorations et maintenir le projet au cours des prochaines années.
Alors si tu es prêt et capable de faire un don ponctuel ou même mensuel, tu peux regarder ici.
J'apprécierais énormément cette reconnaissance de mon travail, et cela me donne une base stable pour continuer à travailler sur Arena et, je l'espère, d'autres grands projets à l'avenir.

[Ko-fi : ko-fi.com/jeanstiletto](https://ko-fi.com/jeanstiletto)

<h2>Licence</h2>

Ce projet est distribué sous la licence GNU General Public License v3.0. Voir le fichier LICENSE pour plus de détails.

<h2>Liens</h2>

- [GitHub](https://github.com/JeanStiletto/AccessibleArena)
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [MTG Arena](https://magic.wizards.com/mtgarena)

<h2>Autres langues</h2>

[English](../README.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Italiano](README.it.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Polski](README.pl.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md)
