# Utility Gamer

Utility Gamer e um app desktop para Windows feito para resolver um problema bem especifico: quando um jogo roda em uma resolucao diferente da area de trabalho, o Windows nem sempre entra no comportamento de apresentacao que a gente espera.

No meu caso, ele nasceu por causa do `Counter-Strike 2`. Quando eu jogava com uma resolucao diferente da resolucao atual do desktop, o jogo podia deixar de entrar em `Independent Flip`. O Utility Gamer existe para automatizar esse ajuste antes do jogo abrir e restaurar tudo depois.

## O problema que ele tenta resolver

Exemplo real:

- desktop em `1920x1080`
- jogo em `1280x960` ou outra resolucao competitiva
- ao abrir o jogo, o ambiente nao fica alinhado do jeito que eu queria
- como consequencia, o `Independent Flip` pode nao acontecer

A proposta do app e simples:

1. detectar quando o jogo abre
2. trocar a resolucao do desktop no monitor escolhido
3. opcionalmente trocar o plano de energia
4. quando o jogo fechar, restaurar a resolucao e o plano anterior

Ele nao "forca magicamente" o `Independent Flip`, mas ajuda a preparar o Windows para esse cenario sem voce precisar fazer tudo manualmente toda vez.

## O que o app faz na pratica

O fluxo real do app e este:

1. Voce adiciona o executavel do jogo.
2. O app extrai e guarda o nome do processo.
3. O monitoramento fica rodando em segundo plano.
4. A cada `2 segundos`, ele verifica se aquele processo apareceu.
5. Quando o processo inicia:
   - pode trocar a resolucao do desktop no monitor selecionado
   - pode trocar o plano de energia
   - pode mostrar notificacoes do Windows
6. Quando o processo fecha:
   - restaura a resolucao configurada para retorno
   - restaura o plano de energia anterior

## O que da para configurar por jogo

Cada perfil pode salvar:

- nome do jogo
- caminho do `.exe`
- nome do processo
- monitor alvo
- resolucao de inicio: largura, altura e Hz
- resolucao de retorno: largura, altura e Hz
- se a troca de resolucao fica ligada ou desligada
- plano de energia para ativar quando o jogo abrir

Isso permite, por exemplo, deixar o `CS2` com um perfil proprio e outro jogo com uma configuracao completamente diferente.

## Recursos principais

### 1. Troca automatica de resolucao

O app usa a API `ChangeDisplaySettingsEx` do Windows para trocar a resolucao do monitor escolhido.

Uso esperado:

- antes do jogo: aplicar a resolucao que voce quer alinhar com o cenario competitivo
- depois do jogo: voltar para a resolucao normal do desktop

### 2. Selecao de monitor

Se voce usa mais de um monitor, o perfil pode apontar para um display especifico.

Isso e importante porque o ajuste de resolucao nao precisa acontecer no monitor errado.

### 3. Troca automatica de plano de energia

O app le os planos de energia disponiveis via `powercfg`, salva o plano atual e pode:

- ativar um plano especifico quando o jogo abre
- restaurar o plano anterior quando o jogo fecha

### 4. Tweaks de sistema

O app tambem inclui algumas acoes uteis para quem quer reduzir atrito no Windows:

- `GlobalTimerResolutionRequests = 1`
- desativar `Game Bar / DVR`
- remover `Xbox Gaming Overlay`
- desativar atalho de `Sticky Keys`
- desativar aceleracao do mouse (`Enhance Pointer Precision`)

Esses tweaks sao opcionais e o app mostra um painel de estado para voce conferir se o sistema realmente ficou como esperado.

### 5. Bandeja do sistema

O app pode:

- iniciar com o Windows
- abrir minimizado na bandeja
- continuar monitorando em segundo plano
- permitir iniciar ou parar o monitoramento pelo menu da bandeja

### 6. Reparos rapidos

Os botoes de `SFC` e `DISM` estao ali como atalhos para manutencao do sistema.

Eles nao sao "otimizacoes gamer". Sao apenas wrappers visuais para:

- `sfc /scannow`
- `dism /Online /Cleanup-Image /RestoreHealth`

## O que ele nao faz

Para o README nao ficar vendendo promessa errada, vale deixar claro:

- ele nao garante `Independent Flip`
- ele nao injeta nada no jogo
- ele nao altera configuracoes internas do `CS2`
- ele nao mexe automaticamente em launch options da Steam
- ele nao escolhe sozinho a melhor resolucao para voce

O papel dele e automatizar o ambiente do Windows ao redor do jogo.

## Onde ele ajuda mais

O caso ideal para o app e este:

- voce joga `CS2` ou outro jogo competitivo
- usa resolucao diferente da area de trabalho
- quer reduzir o numero de passos manuais antes de jogar
- quer restaurar tudo depois sem lembrar do que mudou

## Exemplo de uso no CS2

Um fluxo comum seria:

1. Adicionar o executavel do `CS2`.
2. Escolher o monitor principal do jogo.
3. Ativar a troca de resolucao.
4. Definir uma resolucao de inicio igual ao cenario que voce quer testar no desktop.
5. Definir a resolucao de retorno para a sua resolucao normal.
6. Opcionalmente escolher um plano de energia de alto desempenho.
7. Deixar o monitoramento ligado.

Assim, quando o processo do jogo aparecer, o app aplica o perfil; quando sair, ele desfaz.

## Limitacoes e observacoes importantes

- O projeto e `Windows-only`.
- O app roda com `requireAdministrator`, porque mexe em registro, energia, inicializacao e alguns comandos de sistema.
- Alguns tweaks sao globais no Windows, nao por jogo.
- Mudar a resolucao do desktop pode causar piscada de tela, reorganizacao temporaria de janelas ou mudanca perceptivel no monitor.
- Se o jogo abrir por launcher e o processo final for outro, voce precisa monitorar o processo certo.
- A interface do app hoje e somente `dark mode`.

## Idiomas da interface

No app, os idiomas atualmente disponiveis sao:

- `pt-BR`
- `en-US`

Observacao: este README esta em `pt-BR`, e a interface do aplicativo hoje usa `pt-BR` e `en-US`.

## Detalhes tecnicos

Tecnologias usadas:

- `C#`
- `.NET 9`
- `WinUI 3`
- `Windows App SDK 1.8`
- `MSTest`

Partes principais da implementacao:

- `DisplayHelper`: leitura de monitores e troca de resolucao
- `PowerPlanHelper`: leitura e troca de planos de energia
- `ProcessWatcher`: monitoramento dos processos e aplicacao/restauracao dos perfis
- `ToastHelper`: notificacoes do Windows
- `MainPage`: UI, configuracao dos perfis, tweaks e acoes do usuario

## Requisitos

- Windows 10 `2004` ou superior (`10.0.19041.0+`)
- permissao de administrador

## Estrutura do repositorio

```text
UtilityGamer.WinUI/   app principal
UtilityGamer.Tests/   testes automatizados
docs/                 documentacao auxiliar
```

## Compilar

```powershell
dotnet build "utility gamer.sln" /p:NuGetAudit=false
```

Observacao importante: o projeto esta configurado como `self-contained`, entao a saida gerada ja inclui o runtime necessario para executar o app. Isso evita exigir que o usuario final instale `.NET 9` separadamente.

Resumo pratico:

- para usuario final, prefira sempre a versao `self-contained`
- se voce distribuir uma build comum da pasta `bin`, informe que o usuario precisa ter o `.NET 9 Desktop Runtime`

## Rodar

Como o projeto esta configurado como WinUI 3 desktop nao empacotado, depois da build voce pode abrir o executavel diretamente:

```powershell
.\UtilityGamer.WinUI\bin\x86\Debug\net9.0-windows10.0.19041.0\win-x86\UtilityGamer.WinUI.exe
```

Se voce quiser distribuir exatamente essa build para outra pessoa, envie a pasta inteira `win-x86` e nao apenas o `.exe`. E nessa pasta que ficam os binarios do runtime embutido.

## Publicar para distribuir

Para entregar o app para outra pessoa, o ideal nao e mandar a pasta de desenvolvimento do `build`, e sim uma pasta de `publish` pronta para distribuicao.

O projeto agora publica em modo `self-contained`, com `.NET` e `Windows App SDK Runtime` incluidos no pacote final.

Comando recomendado para usuario final em Windows `x64`:

```powershell
.\scripts\Publish-Release.ps1 -RuntimeIdentifier win-x64
```

Se voce quiser gerar para `x86`:

```powershell
.\scripts\Publish-Release.ps1 -RuntimeIdentifier win-x86
```

O resultado fica em:

```text
artifacts\publish\win-x64\
artifacts\publish\win-x86\
artifacts\publish\win-arm64\
```

Para GitHub Release ou distribuicao manual, envie a pasta inteira do runtime desejado. O usuario final nao precisa baixar `.NET 9` separadamente para executar o app.

## Gerar instalador EXE

Para criar um instalador `.exe` para GitHub Releases, instale o `Inno Setup 6` na maquina de build e rode:

```powershell
.\scripts\Build-Installer.ps1
```

Esse comando publica o app em:

```text
artifacts\bin-build\winui\
```

E gera o instalador em:

```text
artifacts\installer\UtilityGamerSetup-1.0.0-x64.exe
```

Para mudar a versao do nome do instalador:

```powershell
.\scripts\Build-Installer.ps1 -Version 1.0.1
```

Se a pasta `artifacts\bin-build\winui\` ja estiver pronta e voce quiser apenas recompilar o instalador:

```powershell
.\scripts\Build-Installer.ps1 -SkipPublish
```

## Se voce distribuir a build comum

Se, em vez da pasta de `publish`, voce preferir enviar a pasta gerada em `bin`, deixe isso claro para o usuario:

- ele deve ter o `.NET 9 Desktop Runtime`
- ele deve receber a pasta inteira do output, e nao apenas o `.exe`

Em outras palavras:

- `publish self-contained`: melhor para usuario final
- `bin` comum: exige mais cuidado e depende de `.NET 9`

## Testar

```powershell
dotnet test "UtilityGamer.Tests\UtilityGamer.Tests.csproj"
```

## Onde as configuracoes ficam salvas

O arquivo de configuracao do usuario fica em:

```text
%LocalAppData%\UtilityGamer\settings.json
```

Ali ficam salvos:

- perfis dos jogos
- resolucoes padrao
- opcoes de notificacao
- startup
- idioma
- estado geral da UI
- posicao e tamanho da janela

## Resumo curto

Utility Gamer nao tenta ser um "otimizador universal de FPS". Ele e uma ferramenta bem direta para automatizar o ambiente do Windows antes de abrir o jogo, principalmente em cenarios em que a resolucao do jogo e diferente da resolucao do desktop e isso atrapalha o comportamento esperado de apresentacao.
