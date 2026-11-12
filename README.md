<p align="center">
  <img src="icons8-engrenagem-100.png" width="64" height="64" alt="Ícone do Utility Gamer">
</p>

# Utility Gamer

Aplicativo para Windows que acompanha a abertura e o fechamento de jogos para ajustar resolução, frequência do monitor e plano de energia. Cada jogo pode ter um perfil próprio, incluindo bloqueio opcional da tecla Windows.

Este README descreve o código da branch **`main`**. Os recursos em desenvolvimento na `dev`, como Redes, Atualização e prioridades CPU/GPU, ainda não fazem parte desta versão.

[Instalação](#instalação) · [Primeiro uso](#primeiro-uso) · [Recursos](#recursos) · [Limitações](#limitações) · [Desenvolvimento](#desenvolvimento)

## Instalação

1. Consulte as [releases do projeto](https://github.com/saulomiller/utility-gamer/releases) e confira os arquivos disponíveis para a versão escolhida.
2. Se houver um instalador x64, execute-o e siga as instruções.
3. Abra o Utility Gamer pelo menu Iniciar.

O projeto usa WinUI 3 e declara plataforma mínima Windows `10.0.19041.0`. O script de instalador desta branch gera pacotes x64 autocontidos; o SDK .NET é necessário para desenvolver, não para usar esse pacote.

> [!NOTE]
> Um executável sem assinatura pode ser bloqueado pelos mecanismos de proteção do Windows. Baixar um arquivo de uma release não comprova que ele esteja assinado. Confira a origem e a versão do pacote.

## Primeiro uso

1. Adicione o executável do jogo à lista de aplicativos.
2. Escolha o monitor e ative a troca de resolução para esse perfil, se desejar.
3. Defina a resolução e a frequência usadas enquanto o jogo estiver aberto.
4. Configure também a resolução e a frequência de retorno do desktop. Esta versão utiliza os valores definidos no perfil.
5. Opcionalmente escolha um plano de energia e o bloqueio da tecla Windows.
6. Ative o monitoramento e abra o jogo.

A verificação de processos acontece a cada dois segundos. Quando detecta o jogo, o app solicita os ajustes do perfil; quando detecta seu encerramento, tenta retornar ao modo de desktop configurado e ao plano de energia anterior.

Antes de pausar o monitoramento ou encerrar o Utility Gamer, feche o jogo e confira o retorno do desktop. Nesta implementação, parar o monitoramento não executa o mesmo fluxo de restauração do encerramento do jogo.

## Recursos

- **Resolução por jogo:** configuração de largura, altura e Hz, com monitor de destino e modo de retorno.
- **Energia:** escolha de um plano para a sessão e tentativa de restauração do plano anterior quando o jogo fecha.
- **Tecla Windows:** bloqueio opcional durante a sessão monitorada.
- **Ajustes do sistema:** timer, Game Bar/DVR, Xbox Gaming Overlay, atalho de teclas de aderência e aceleração do mouse.
- **Bandeja e preferências:** iniciar com o Windows, abrir minimizado, manter o monitoramento em segundo plano e escolher notificações.
- **Reparos:** execução de SFC e DISM pela interface.

### Ajustes do sistema

Nesta branch, as caixas da área de ajustes são escolhas para o botão **Aplicar ajustes**. Esses ajustes são executados manualmente; não devem ser confundidos com os efeitos aplicados ao abrir um jogo.

Os ajustes de sistema podem permanecer no Windows após o app fechar. Não presuma que desmarcar uma opção desfaz todas as ações anteriores, especialmente a remoção do Xbox Gaming Overlay. Confira o estado apresentado pela interface e o resultado de cada operação.

SFC e DISM são ferramentas de manutenção do Windows e podem exigir administrador:

```powershell
sfc /scannow
dism /Online /Cleanup-Image /RestoreHealth
```

### Interface

A captura abaixo pertence à apresentação já utilizada nesta branch. O pacote instalado pode diferir conforme a release:

![Interface do Utility Gamer](https://github.com/user-attachments/assets/03a0c695-6952-408c-a5be-f594c92cf8fb)

## Limitações

- O app não garante aumento de FPS, redução de input lag ou Independent Flip. A resolução é apenas uma parte da configuração de vídeo.
- Os perfis não alteram automaticamente as configurações internas do CS2 nem as opções de inicialização da Steam.
- Use resoluções e frequências compatíveis com o monitor. Os valores de retorno do perfil precisam corresponder ao desktop desejado.
- A detecção por processo tem intervalo de consulta; os ajustes não são necessariamente aplicados antes da primeira imagem do jogo.
- Falhas de API, permissões e diferenças de driver podem impedir aplicação ou restauração. Confira os estados após o jogo fechar.
- A refatoração com diário de recuperação e os novos serviços da `dev` não estão presentes nesta `main`.

## Configurações locais

O código desta branch armazena as configurações em:

```text
%LOCALAPPDATA%\UtilityGamer\settings.json
```

Faça uma cópia antes de editar esse arquivo manualmente e encerre o aplicativo para evitar concorrência com a gravação das configurações.

## Desenvolvimento

A `main` usa **C#, .NET 9, WinUI 3 e Windows App SDK 1.8.251106002**. Ela contém os projetos `UtilityGamer.WinUI` e `UtilityGamer.Tests`; não há um projeto Core separado nesta branch.

Prepare o SDK .NET 9 e as ferramentas Windows/WinUI compatíveis com o projeto. Para gerar o instalador, instale também o Inno Setup 6.

Na raiz do checkout da `main`, em PowerShell:

```powershell
dotnet restore UtilityGamer.WinUI/UtilityGamer.WinUI.csproj -r win-x64 -p:Platform=x64
dotnet restore UtilityGamer.Tests/UtilityGamer.Tests.csproj
dotnet build UtilityGamer.WinUI/UtilityGamer.WinUI.csproj -c Release -p:Platform=x64 --no-restore
dotnet test UtilityGamer.Tests/UtilityGamer.Tests.csproj -c Release --no-restore
```

O workflow desta branch também executa build e testes nos eventos configurados para `main` e `master`.

### Gerar instalador

```powershell
.\scripts\Build-Installer.ps1 -RuntimeIdentifier win-x64 -Configuration Release
```

Confira os parâmetros e diretórios de saída em `scripts/Build-Installer.ps1`. Não use os parâmetros de assinatura ou validação documentados para a `dev` sem verificar se existem nesta versão do script.

Esta atualização modifica apenas a documentação. Ela não comprova uma nova compilação, assinatura, publicação de release ou correção dos problemas conhecidos do aplicativo.
