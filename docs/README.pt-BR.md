# Accessible Arena

Mod de acessibilidade para Magic: The Gathering Arena que permite a jogadores cegos e com deficiência visual jogar usando um leitor de tela. Navegação completa por teclado, anúncios do leitor de tela para todos os estados do jogo e localização em 12 idiomas.

**Status:** Beta pública. A jogabilidade principal é funcional. Alguns casos especiais e bugs menores permanecem. Veja Problemas conhecidos abaixo.

**Nota:** Atualmente apenas teclado. Não há suporte para mouse ou toque. Testado apenas no Windows 11 com NVDA. Outras versões do Windows e leitores de tela (JAWS, Narrator, etc.) podem funcionar, mas não foram testados.

## Recursos

- Navegação completa por teclado para todas as telas (início, loja, maestria, construtor de decks, duelos)
- Integração com leitor de tela via biblioteca Tolk
- Leitura de informações das cartas com teclas de seta (nome, custo de mana, tipo, poder/resistência, texto de regras, texto de ambientação, raridade, artista)
- Suporte completo para duelos: navegação por zonas, combate, seleção de alvos, pilha, navegadores (vidência, vigiar, mulligan)
- Anúncios de relações de anexo e combate (encantado por, bloqueando, alvo de)
- Loja acessível com opções de compra e suporte a diálogos de pagamento
- Suporte a partidas contra bots para prática
- Menu de configurações (F2) e menu de ajuda (F1) disponíveis em qualquer lugar
- 12 idiomas: inglês, alemão, francês, espanhol, italiano, português (BR), japonês, coreano, russo, polonês, chinês simplificado, chinês tradicional

## Requisitos

- Windows 10 ou posterior
- Magic: The Gathering Arena (instalado pelo instalador oficial ou Epic Games Store)
- Um leitor de tela (NVDA recomendado: https://www.nvaccess.org/download/)
- MelonLoader (o instalador cuida disso automaticamente)

## Instalação

### Usando o instalador (recomendado)

1. Baixe `AccessibleArenaInstaller.exe` da versão mais recente no GitHub: https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. Feche o MTG Arena se estiver em execução
3. Execute o instalador. Ele detectará sua instalação do MTGA, instalará o MelonLoader se necessário e implantará o mod
4. Inicie o MTG Arena. Você deve ouvir "Accessible Arena v... iniciado" pelo seu leitor de tela

### Instalação manual

1. Instale o MelonLoader na sua pasta do MTGA (https://github.com/LavaGang/MelonLoader)
2. Baixe `AccessibleArena.dll` da versão mais recente
3. Copie a DLL para: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. Certifique-se de que `Tolk.dll` e `nvdaControllerClient64.dll` estejam na pasta raiz do MTGA
5. Inicie o MTG Arena

## Início rápido

Se você ainda não tem uma conta Wizards, pode criar uma em https://myaccounts.wizards.com/ em vez de usar a tela de registro no jogo.

Após a instalação, inicie o MTG Arena. O mod anuncia a tela atual pelo seu leitor de tela.

- Pressione **F1** a qualquer momento para um menu de ajuda navegável listando todos os atalhos de teclado
- Pressione **F2** para o menu de configurações (idioma, verbosidade, mensagens do tutorial)
- Pressione **F3** para ouvir o nome da tela atual
- Use **Seta cima/baixo** ou **Tab/Shift+Tab** para navegar nos menus
- Pressione **Enter** ou **Espaço** para ativar elementos
- Pressione **Backspace** para voltar

## Atalhos de teclado

### Menus

- Seta cima/baixo (ou W/S): Navegar pelos itens
- Tab/Shift+Tab: Navegar pelos itens (igual a Seta cima/baixo)
- Seta esquerda/direita (ou A/D): Controles de carrossel e stepper
- Home/End: Ir para o primeiro/último item
- Page Up/Page Down: Página anterior/próxima na coleção
- Enter/Espaço: Ativar
- Backspace: Voltar

### Duelos - Zonas

- C: Sua mão
- G / Shift+G: Seu cemitério / Cemitério do oponente
- X / Shift+X: Seu exílio / Exílio do oponente
- S: Pilha
- B / Shift+B: Suas criaturas / Criaturas do oponente
- A / Shift+A: Seus terrenos / Terrenos do oponente
- R / Shift+R: Suas não-criaturas / Não-criaturas do oponente

### Duelos - Dentro das zonas

- Esquerda/Direita: Navegar entre cartas
- Home/End: Ir para a primeira/última carta
- Seta cima/baixo: Ler detalhes da carta quando focada
- I: Info estendida da carta (descrições de palavras-chave, outras faces)
- Shift+Cima/Baixo: Trocar fileiras do campo de batalha

### Duelos - Informações

- T: Turno e fase atuais
- L: Totais de vida
- V: Zona de info do jogador (Esquerda/Direita para trocar jogador, Cima/Baixo para propriedades)
- D / Shift+D: Quantidade na sua biblioteca / Biblioteca do oponente
- Shift+C: Quantidade de cartas na mão do oponente

### Duelos - Ações

- Espaço: Confirmar (passar prioridade, confirmar atacantes/bloqueadores, próxima fase)
- Backspace: Cancelar / recusar
- Tab: Percorrer alvos ou elementos destacados
- Ctrl+Tab: Percorrer apenas alvos do oponente
- Enter: Selecionar alvo

### Duelos - Navegadores (Vidência, Vigiar, Mulligan)

- Tab: Navegar por todas as cartas
- C/D: Ir para a zona superior/inferior
- Esquerda/Direita: Navegar dentro da zona
- Enter: Alternar posicionamento da carta
- Espaço: Confirmar seleção
- Backspace: Cancelar

### Global

- F1: Menu de ajuda
- F2: Menu de configurações
- F3: Anunciar tela atual
- Ctrl+R: Repetir último anúncio
- Backspace: Voltar/fechar/cancelar universal

## Reportar bugs

Se encontrar um bug, por favor abra um issue no GitHub: https://github.com/JeanStiletto/AccessibleArena/issues

Inclua as seguintes informações:

- O que você estava fazendo quando o bug ocorreu
- O que você esperava que acontecesse
- O que realmente aconteceu
- Seu leitor de tela e versão
- Anexe o arquivo de log do MelonLoader: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## Problemas conhecidos

- A tecla Espaço para passar prioridade nem sempre é confiável (o mod clica diretamente no botão como fallback)
- As cartas na lista do deck no construtor mostram apenas nome e quantidade, não detalhes completos
- A seleção do tipo de fila do PlayBlade (Ranqueada, Jogo Aberto, Brawl) nem sempre define o modo de jogo correto

Para a lista completa, veja docs/KNOWN_ISSUES.md.

## Solução de problemas

**Sem saída de voz após iniciar o jogo**
- Certifique-se de que seu leitor de tela esteja em execução antes de iniciar o MTG Arena
- Verifique se `Tolk.dll` e `nvdaControllerClient64.dll` estão na pasta raiz do MTGA (o instalador os coloca automaticamente)
- Verifique o log do MelonLoader em `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log` para erros

**O jogo trava ao iniciar ou o mod não carrega**
- Certifique-se de que o MelonLoader está instalado.
- Se o jogo foi atualizado recentemente, o MelonLoader ou o mod podem precisar ser reinstalados. Execute o instalador novamente.
- Verifique se `AccessibleArena.dll` está em `C:\Program Files\Wizards of the Coast\MTGA\Mods\`

**O mod estava funcionando mas parou após uma atualização do jogo**
- Atualizações do MTG Arena podem sobrescrever arquivos do MelonLoader. Execute o instalador novamente para reinstalar o MelonLoader e o mod.
- Se o jogo mudou significativamente sua estrutura interna, o mod pode precisar de uma atualização. Verifique novas versões no GitHub.

**Atalhos de teclado não funcionam**
- Certifique-se de que a janela do jogo está em foco (clique nela ou use Alt+Tab)
- Pressione F1 para verificar se o mod está ativo. Se ouvir o menu de ajuda, o mod está funcionando.
- Alguns atalhos só funcionam em contextos específicos (atalhos de duelo apenas durante um duelo)

**Idioma errado**
- Pressione F2 para abrir o menu de configurações, depois use Enter para percorrer os idiomas

## Compilar a partir do código-fonte

Requisitos: SDK .NET (qualquer versão que suporte o target net472)

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

A DLL compilada estará em `src/bin/Debug/net472/AccessibleArena.dll`.

As referências de assembly do jogo são esperadas na pasta `libs/`. Copie estas DLLs da sua instalação do MTGA (`MTGA_Data/Managed/`):
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

As DLLs do MelonLoader (`MelonLoader.dll`, `0Harmony.dll`) vêm da sua instalação do MelonLoader.

## Licença

Este projeto é licenciado sob a GNU General Public License v3.0. Veja o arquivo LICENSE para detalhes.

## Links

- GitHub: https://github.com/JeanStiletto/AccessibleArena
- Leitor de tela NVDA (recomendado): https://www.nvaccess.org/download/
- MelonLoader: https://github.com/LavaGang/MelonLoader
- MTG Arena: https://magic.wizards.com/mtgarena
