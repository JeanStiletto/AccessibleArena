<h1>Accessible Arena</h1>

<h2>O que é este mod</h2>

Este mod permite jogar o Arena, a representação digital mais popular e mais amigável para iniciantes do jogo de cartas colecionáveis Magic: The Gathering. Ele adiciona suporte completo a leitores de tela e navegação por teclado a quase todos os aspectos do jogo.

O mod dá suporte a todas as línguas em que o jogo foi traduzido. Além disso, alguns idiomas que o próprio jogo não suporta são parcialmente cobertos: neles, avisos específicos do mod, como textos de ajuda e dicas de interface, são traduzidos, enquanto os dados das cartas e do jogo permanecem no idioma padrão do jogo.

<h2>O que é Magic: The Gathering</h2>

Magic é um jogo de cartas colecionáveis, marca registrada da Wizards of the Coast, que permite jogar como um mago contra outros magos, lançando mágicas representadas pelas cartas. Existem 5 cores em Magic, que representam diferentes identidades de jogabilidade e ambientação. Se você conhece Hearthstone ou Yu-Gi-Oh, vai reconhecer muitos conceitos, porque Magic é o ancestral de todos esses jogos.
Se você quiser aprender mais sobre Magic em geral, o site oficial do jogo e muitos criadores de conteúdo vão te ajudar.

<h2>Requisitos</h2>

- Windows 10 ou mais recente
- Magic: The Gathering Arena (instalado pelo instalador oficial da Wizards ou pela Steam)
- Um leitor de tela (apenas NVDA e JAWS são testados)
- MelonLoader (o instalador cuida disso automaticamente)

<h2>Instalação</h2>

<h3>Usando o instalador (recomendado)</h3>

1. [Baixe o AccessibleArenaInstaller.exe](https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe) do lançamento mais recente no GitHub
2. Feche o MTG Arena se estiver aberto
3. Execute o instalador. Ele vai detectar sua instalação do MTGA, instalar o MelonLoader se necessário e implantar o mod
4. Abra o MTG Arena. Você deve ouvir "Accessible Arena v... launched" pelo seu leitor de tela

<h3>Instalação manual</h3>

1. Instale o [MelonLoader](https://github.com/LavaGang/MelonLoader) na sua pasta do MTGA
2. Baixe `AccessibleArena.dll` do lançamento mais recente
3. Copie a DLL para a pasta Mods do MTGA:
   - Instalação WotC: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
   - Instalação Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\Mods\`
4. Garanta que `Tolk.dll` e `nvdaControllerClient64.dll` estejam na pasta raiz do MTGA
5. Abra o MTG Arena

<h2>Desinstalação</h2>

Execute o instalador de novo. Se o mod já estiver instalado, ele oferecerá uma opção de desinstalação. Opcionalmente você também pode remover o MelonLoader. Para desinstalar manualmente, apague `AccessibleArena.dll` da pasta `Mods\` e remova `Tolk.dll` e `nvdaControllerClient64.dll` da pasta raiz do MTGA.

<h2>Se você vem do Hearthstone</h2>

Se você jogou Hearthstone Access, vai reconhecer muitas coisas por bons motivos, pois não só os princípios de jogo são próximos entre si, como eu segui muitos princípios de design. Mesmo assim, algumas coisas são diferentes.

Primeiro, você tem mais zonas para navegar, pois Magic conhece cemitério, exílio e algumas zonas extras. Seu campo de batalha não é limitado em tamanho e tem linhas adicionais de ordenação para tornar mais gerenciável a massa de coisas que pode aparecer.

Sua mana não sobe automaticamente, mas vem de cartas de terreno de cores diferentes que você tem que jogar ativamente. Assim, os custos de mana têm partes incolores e coloridas que, somadas, dão os requisitos completos de custo de uma carta que você precisa cumprir.

Você não pode atacar criaturas diretamente; apenas os oponentes e algumas cartas muito específicas (planinautas e batalhas) podem ser alvo dos atacantes. Como defensor, você tem que decidir se quer bloquear um ataque para fazer as criaturas lutarem. Se você não bloquear, o dano atingirá seu avatar de jogador, mas suas criaturas podem permanecer intactas. Além disso, o dano não se acumula nas criaturas, mas é curado ao fim de cada turno, tanto no fim do seu turno quanto no do oponente. Para interagir com as criaturas do oponente que se recusam a lutar com você, você precisa jogar cartas específicas ou pressionar tanto os pontos de vida do seu oponente que ele não tenha escolha a não ser sacrificar criaturas valiosas para sobreviver.

O jogo tem fases de batalha muito bem distintas que permitem ações específicas como comprar, conjurar mágicas ou lutar. Assim, Magic permite e estimula que você faça coisas no turno do oponente. Não fique mais sentado esperando enquanto as coisas acontecem. Jogue um deck interativo e destrua os planos inimigos na hora.

<h2>Primeiros passos</h2>

O jogo primeiro pede que você forneça alguns dados sobre si e registre um personagem. Isso deveria funcionar pelos mecanismos internos do jogo, mas se não funcionar você pode alternativamente usar o site do jogo para fazer isso, ele é totalmente acessível.

O jogo começa com um tutorial no qual você aprende o básico de Magic: The Gathering. O mod adiciona dicas de tutorial personalizadas para usuários de leitores de tela ao lado do tutorial padrão. Depois de terminar o tutorial, você é recompensado com 5 decks iniciais, um para cada cor.

A partir daí, você tem várias opções para desbloquear mais cartas e aprender o jogo:

- **Desafios de cor:** Jogue o desafio de cor para cada uma das cinco cores de Magic. Cada desafio faz você enfrentar 4 oponentes NPC, seguidos de uma partida contra um jogador real no final.
- **Eventos de decks iniciais:** Jogue um dos 10 decks bicolores contra humanos reais que têm as mesmas opções de deck disponíveis.
- **Jump In:** Escolha dois pacotes de 20 cartas de cores e temas diferentes, combine-os em um deck e jogue contra humanos reais com escolhas similares. Você ganha tokens grátis para este evento e fica com as cartas escolhidas.
- **Spark Ladder:** Em algum momento, a Spark Ladder se desbloqueia, onde você joga suas primeiras partidas ranqueadas contra oponentes reais.

Confira seu correio no menu social, pois ele contém muitas recompensas e pacotes de cartas.

O jogo desbloqueia modos gradualmente com base no que e no quanto você joga. Ele dá dicas e missões no menu de progresso e objetivos, e destaca para você os modos relevantes no menu jogar. Depois que você terminar conteúdo suficiente para novos jogadores, todos os diferentes modos e eventos ficam totalmente disponíveis.

No Códex do Multiverso, você pode aprender sobre modos de jogo e mecânicas. Ele se expande com o progresso na experiência NPE.

Em configurações de conta, você pode pular todas as experiências de tutorial e forçar o desbloqueio de tudo para ter total liberdade desde o início. Contudo, jogar os eventos de novo jogador dá muitas cartas e é recomendado para novos jogadores. Só desbloqueie tudo cedo se já souber o que está fazendo. Caso contrário, o conteúdo para iniciantes oferece bastante diversão e aprendizado enquanto te guia bem.

<h2>Atalhos de teclado</h2>

A navegação segue convenções padrão em todo lugar: Setas para mover, Home/End para ir ao primeiro/último, Enter para selecionar, Espaço para confirmar, Backspace para voltar ou cancelar. Tab/Shift+Tab também funciona para navegação. Page Up/Page Down muda de página.

<h3>Global</h3>

- F1: Menu de ajuda (lista todos os atalhos para a tela atual)
- Ctrl+F1: Anunciar atalhos para a tela atual
- F2: Configurações do mod
- F3: Anunciar tela atual
- F4: Painel de amigos (nos menus) / Chat do duelo (durante duelos)
- F5: Buscar / iniciar atualização
- Ctrl+R: Repetir o último anúncio

<h3>Duelos - Zonas</h3>

Suas zonas: C (Mão), G (Cemitério), X (Exílio), S (Pilha), W (Zona de Comando)
Zonas do oponente: Shift+G, Shift+X, Shift+W
Campo de batalha: B / Shift+B (Criaturas), A / Shift+A (Terrenos), R / Shift+R (Não criaturas)
Dentro das zonas: Esquerda/Direita para navegar, Cima/Baixo para ler detalhes da carta, I para informações estendidas
Shift+Cima/Baixo: Alternar linhas do campo de batalha

<h3>Duelos - Informações</h3>

- T: Turno/Fase
- L: Pontos de vida
- V: Zona de info do jogador
- D / Shift+D: Contagens de grimório
- Shift+C: Cartas na mão do oponente
- M / Shift+M: Resumo de terrenos seus / do oponente
- K: Info de marcadores na carta focada
- O: Log da partida (últimos anúncios do duelo)
- E / Shift+E: Cronômetro seu / do oponente

<h3>Duelos - Alvos e ações</h3>

- Tab / Ctrl+Tab: Ciclar alvos (todos / somente oponente)
- Enter: Selecionar alvo
- Espaço: Passar prioridade, confirmar atacantes/bloqueadores, avançar fase

<h3>Duelos - Full control e paradas de fase</h3>

- P: Alternar full control (temporário, reseta ao mudar de fase)
- Shift+P: Alternar full control travado (permanente)
- Shift+Backspace: Alternar passar até ação do oponente (skip suave)
- Ctrl+Backspace: Alternar pular turno (forçar pulo do turno inteiro)
- 1-0: Alternar paradas de fase (1=Manutenção, 2=Compra, 3=Primeira fase principal, 4=Início de combate, 5=Declarar atacantes, 6=Declarar bloqueadores, 7=Dano de combate, 8=Fim de combate, 9=Segunda fase principal, 0=Etapa final)

<h3>Duelos - Navegadores (Scry, Surveil, Mulligan)</h3>

- Tab: Navegar por todas as cartas
- C/D: Alternar entre zonas superior/inferior
- Enter: Alternar colocação da carta

<h2>Solução de problemas</h2>

<h3>Sem fala depois de abrir o jogo</h3>

- Garanta que seu leitor de tela esteja em execução antes de abrir o MTG Arena
- Verifique se `Tolk.dll` e `nvdaControllerClient64.dll` estão na pasta raiz do MTGA (o instalador os coloca automaticamente)
- Verifique o log do MelonLoader na sua pasta do MTGA (`MelonLoader\Latest.log`) por erros

<h3>O jogo trava ao iniciar ou o mod não carrega</h3>

- Garanta que o MelonLoader esteja instalado.
- Se o jogo foi atualizado recentemente, o MelonLoader ou o mod podem precisar ser reinstalados. Execute o instalador de novo.
- Verifique se `AccessibleArena.dll` está na pasta `Mods\` dentro da sua instalação do MTGA

<h3>O mod estava funcionando, mas parou depois de uma atualização do jogo</h3>

- Atualizações do MTG Arena podem sobrescrever arquivos do MelonLoader. Execute o instalador de novo para reinstalar o MelonLoader e o mod.
- Se o jogo mudou significativamente sua estrutura interna, o mod pode precisar de atualização. Verifique novos lançamentos no GitHub.

<h3>Atalhos de teclado não funcionam</h3>

- Garanta que a janela do jogo esteja em foco (clique nela ou Alt+Tab)
- Pressione F1 para verificar se o mod está ativo. Se você ouvir o menu de ajuda, o mod está rodando.
- Alguns atalhos só funcionam em contextos específicos (atalhos de duelo só funcionam durante um duelo)

<h3>Idioma errado</h3>

- Pressione F2 para abrir o menu de configurações, depois use Enter para ciclar pelos idiomas

<h3>Windows avisa que o instalador ou a DLL não são seguros</h3>

O instalador e a DLL do mod não são assinados digitalmente. Certificados de assinatura de código custam algumas centenas de euros por ano, o que não é realista para um projeto de acessibilidade gratuito. Por isso, o Windows SmartScreen e alguns antivírus vão te avisar ao rodar o instalador pela primeira vez, ou marcar a DLL como "editor desconhecido".

Para verificar se o arquivo que você baixou corresponde ao publicado no GitHub, cada lançamento lista uma soma de verificação SHA256 tanto para `AccessibleArenaInstaller.exe` quanto para `AccessibleArena.dll`. Você pode calcular o hash do arquivo baixado e comparar:

- PowerShell: `Get-FileHash <nomedoarquivo> -Algorithm SHA256`
- Prompt de Comando: `certutil -hashfile <nomedoarquivo> SHA256`

Se o hash bater com o das notas de lançamento, o arquivo é autêntico. Para rodar o instalador apesar do aviso do SmartScreen, escolha "Mais informações" e depois "Executar mesmo assim".

<h2>Relatar bugs</h2>

Se encontrar um bug, você pode postar no lugar onde encontrou o mod publicado, ou [abrir uma issue no GitHub](https://github.com/JeanStiletto/AccessibleArena/issues).

Inclua as seguintes informações:

- O que você estava fazendo quando o bug ocorreu
- O que você esperava que acontecesse
- O que realmente aconteceu
- Se quiser anexar um log do jogo, feche o jogo e compartilhe o arquivo de log do MelonLoader da sua pasta do MTGA:
  - WotC: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
  - Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\MelonLoader\Latest.log`

<h2>Problemas conhecidos</h2>
O jogo deveria cobrir quase todas as telas, mas pode haver alguns casos extremos que não funcionam totalmente. O PayPal bloqueia usuários cegos com um captcha ilegal não sonoro, então você terá de usar ajuda de alguém com visão ou outros métodos de pagamento se quiser gastar dinheiro real no jogo.
Alguns eventos específicos podem não estar totalmente funcionais. O draft com jogadores reais tem uma tela de lobby ainda não suportada, mas no quickdraft você escolhe cartas contra bots antes de enfrentar oponentes humanos, esse modo é funcional e recomendado para quem gosta desse tipo de experiência. O modo Cube não foi tocado. Eu nem sei direito do que se trata e ele custa muitos recursos do jogo. Então vou fazer isso se tiver tempo ou sob pedido.
O sistema cosmético do jogo com Emotes, Mascotes, estilos de carta e títulos só é parcialmente suportado por enquanto.
O mod é testado apenas em Windows com NVDA e JAWS e ainda depende da biblioteca Tolk sem modificações. Eu não consigo testar a compatibilidade com Mac ou Linux aqui, e bibliotecas multiplataforma como Prism não suportavam totalmente as versões antigas de .NET das quais o jogo depende neste momento. Então só vou mudar para uma biblioteca mais ampla se houver pessoas que possam ajudar a testar outras plataformas ou leitores de tela asiáticos que não são totalmente suportados pelo Tolk sem modificações. Então não hesite em me contatar se quiser que eu trabalhe nisso.

Para a lista atual de problemas conhecidos, veja [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

<h2>Isenções</h2>
<h3>Outras acessibilidades</h3>

Este mod se chama Accessible Arena principalmente porque soa bem. Mas no momento, isso é apenas um mod de acessibilidade para leitores de tela. Estou absolutamente interessado em cobrir mais deficiências com este mod, deficiências visuais, motoras etc. Mas só tenho experiência em acessibilidade para leitores de tela. Como pessoa totalmente cega, por exemplo, questões de coloração e fontes são totalmente abstratas para mim. Então, se você quiser algo desse tipo implementado, por favor não hesite em me contatar se conseguir descrever claramente suas necessidades e estiver disposto a me ajudar a testar os resultados.
Aí ficarei feliz em dar mais verdade ao nome deste mod.

<h3>Contato com a empresa</h3>

Infelizmente não consegui obter insights confiáveis sobre a equipe do Arena ou contatos informais com desenvolvedores. Então decidi pular seus canais oficiais de comunicação por enquanto. Em 3 meses construindo e jogando eu nunca esbarrei em nenhum sistema de proteção contra bots, então não acho que eles consigam nos detectar como usuários do mod. Mas eu não quis correr o risco de comunicar em canais oficiais como pessoa sozinha. Então espalhem a palavra sobre o mod e vamos construir uma comunidade grande e valiosa. Aí teremos uma posição muito melhor se decidirmos entrar em contato diretamente. Só não tente escrever para eles sem falar comigo primeiro. Especialmente não mande pedidos de acessibilidade nativa ou de integração do meu mod à base de código deles. Nenhum dos dois vai acontecer de qualquer jeito.

<h3>Compras no jogo</h3>

O Arena tem algumas mecânicas de dinheiro real e você pode comprar uma moeda do jogo. Esses métodos de pagamento são em sua maioria acessíveis, exceto o PayPal, porque eles incluíram proteção captcha no login. Você pode tentar desinstalar o mod para o cadastro do método de pagamento e pedir ajuda de alguém com visão, mas mesmo isso é pouco confiável por causa do pesadelo de acessibilidade que é o captcha deles, ainda mais quebrado e mal implementado pela Wizards of the Coast.
Mas outros métodos de pagamento funcionam de forma estável. Eu e outros testamos a compra no jogo de coisas e o uso do sistema deve ser seguro. Mas é absolutamente possível que ocorram bugs ou mesmo que o mod te induza ao erro. Pode clicar nas coisas erradas, mostrar informações erradas ou incompletas, fazer coisas erradas por causa de mudanças internas do Arena. Eu poderia testar, mas não posso garantir 100% de que você não compraria as coisas erradas com seu dinheiro real. Eu não vou assumir responsabilidade por isso e, dado que este não é um produto oficial do Arena, a empresa do jogo também não vai. Por favor, nem tente pedir reembolso nesse caso, eles não vão dar.

<h3>Uso de IA</h3>

O código deste mod foi criado 100% com a ajuda do agente Claude da Anthropic, usando os modelos Opus: começou no 4.5, a maior parte do desenvolvimento aconteceu no 4.6 e os últimos passos rumo ao lançamento foram feitos no 4.7. E, graças ao meu maior colaborador, um pouco de Codex também. Estou ciente dos problemas do uso de IA. Mas em um tempo em que todos usam essas ferramentas para fazer muita coisa bem mais duvidosa, enquanto a indústria de jogos não conseguiu nos dar a acessibilidade que queremos em termos de qualidade ou quantidade, eu ainda assim decidi usar as ferramentas.

<h2>Como contribuir</h2>

Aceito contribuições com prazer e, com [blindndangerous](https://github.com/blindndangerous), muito trabalho útil de outra pessoa já faz parte deste mod. Estou especialmente interessado em melhorias e correções para coisas que não consigo testar, como diferentes configurações de sistema, correção de idiomas que não falo etc. Mas aceito também pedidos de funcionalidades. Antes de trabalhar em algo, confira os problemas conhecidos.

- Para diretrizes gerais de contribuição, veja [CONTRIBUTING.md](../CONTRIBUTING.md)
- Para ajuda em traduções, veja [CONTRIBUTING_TRANSLATIONS.md](CONTRIBUTING_TRANSLATIONS.md)

<h2>Créditos</h2>

E agora quero agradecer a muitas pessoas, porque felizmente isto não foi apenas eu e a IA em uma caixa preta, mas toda uma rede ao meu redor, ajudando, fortalecendo, sendo simplesmente social e gentil.
Por favor, me mande DM se eu te esqueci ou se você quer ser conhecido por um nome diferente ou não ser mencionado.

Primeiro, este trabalho se apoia muito no trabalho de outras pessoas que fizeram as coisas pioneiras que eu só tive que refazer para o Accessible Arena.
Em termos de design, é o Hearthstone Access, do qual pude herdar muito, não só por ser bem conhecido de quem jogou o jogo, mas porque é realmente um bom design de UI.
Em termos de modding, quero agradecer aos membros do Discord de modding do Zax. Vocês não só desvendaram todas essas coisas, todas as ferramentas e procedimentos que eu só precisei instalar e usar. Vocês me ensinaram tudo o que eu precisava saber sobre modding com IA, seja diretamente ou discutindo coisas em público ou ajudando outros iniciantes. Além disso, deram a mim e ao meu projeto uma plataforma e comunidade em que podemos existir.

Para grandes contribuições de código, quero agradecer a [blindndangerous](https://github.com/blindndangerous), que também fez muito trabalho neste projeto. Durante a vida do projeto acho que recebi cerca de 50 PRs e mais dele sobre todo tipo de problemas, das pequenas coisas chatas de resolver até sugestões maiores de UI e acessibilidade de telas inteiras do jogo.
Agradecimentos também ao Ahix, que criou [prompts de refatoração para grandes projetos feitos com IA](https://github.com/ahicks92/llm-mod-refactoring-prompts) que eu rodei sobre minhas próprias refatorações para garantir qualidade e manutenibilidade do código.

Por contribuições de código, quero agradecer:
- [blindndangerous](https://github.com/blindndangerous)
- [LordLuceus](https://github.com/LordLuceus)

Por testar as betas, dar feedback e ideias, quero agradecer:
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
- [LordLuceus](https://github.com/LordLuceus)

Por testes com pessoas videntes para entender fluxos visuais e confirmar algumas coisas, quero agradecer:
- [mauriceKA](https://github.com/mauriceKA)
- VeganWolf
- Lea Holstein

<h3>Ferramentas usadas</h3>

- Claude com todos os modelos inclusos
- MelonLoader
- Harmony para patching de IL
- Tolk para comunicação com leitores de tela
- ILSpy para descompilar código do jogo

<h2>Apoie seu modder</h2>

Criar este mod não foi só muita diversão e fortalecimento para mim, mas me custou muito tempo e muito dinheiro real em assinaturas do Claude. Vou mantê-las para trabalhar em mais melhorias e manter o projeto nos próximos anos.
Então, se você estiver disposto e puder fazer uma doação única ou mesmo mensal, dê uma olhada aqui.
Apreciaria muito esse reconhecimento pelo meu trabalho, pois ele me dá uma base estável para continuar trabalhando no Arena e, tomara, em outros grandes projetos no futuro.

[Ko-fi: ko-fi.com/jeanstiletto](https://ko-fi.com/jeanstiletto)

<h2>Licença</h2>

Este projeto é licenciado sob a GNU General Public License v3.0. Veja o arquivo LICENSE para detalhes.

<h2>Links</h2>

- [GitHub](https://github.com/JeanStiletto/AccessibleArena)
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [MTG Arena](https://magic.wizards.com/mtgarena)

<h2>Outros idiomas</h2>

[English](../README.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [Italiano](README.it.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Polski](README.pl.md) | [Русский](README.ru.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md)
