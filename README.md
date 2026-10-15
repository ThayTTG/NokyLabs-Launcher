# NokyLabs Launcher

Launcher multiplataforma (Windows + Linux) para os utilitários da NokyLabs, feito em **C# / .NET 8 + Avalonia UI**.

**Versão atual: v0.6.0** — assistente de instalação (com modo portátil e instalação limpa), desinstalador registrado no Windows, atalhos, catálogo, detecção, instalar/atualizar/desinstalar apps, bandeja, iniciar com o sistema, avisos, verificação periódica e atualizações automáticas. Os links de download ainda são placeholders, porque os apps ainda não foram publicados nos Releases do GitHub.

---

## O que precisa estar instalado

- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- (Opcional) **Visual Studio 2022** com a extensão **Avalonia for Visual Studio**, para ver o preview do XAML.

## Como rodar

**Visual Studio:** abra `NokyLabs.sln` e aperte **F5**.

**Terminal** (Windows ou Linux), dentro desta pasta:

```bash
dotnet run --project src/NokyLabs
```

## Como lançar uma versão

1. Anote o que mudou no `CHANGELOG.md` (seção `## [Não lançado]`) e aumente o `<Version>` em `src/NokyLabs/NokyLabs.csproj`.
2. Envie para o branch `release` (`git push origin main:release`).
3. O GitHub Actions compila Windows e Linux e publica o Release sozinho, já levando
   a cópia mais recente do catálogo do site embutida (para funcionar offline).
4. Em até uma hora, o robô do catálogo (no repositório do site) atualiza a versão, o link
   e o SHA-256 do launcher, e quem tem o NokyLabs instalado vê o aviso de versão nova.
   Para não esperar: no repositório `nokylabs`, aba **Actions** › **Robô do catálogo** › **Run workflow**.

Se a versão já tinha sido lançada, o workflow para logo no começo com o aviso
"Versão já lançada": é só aumentar o `<Version>` e enviar de novo.

A cada envio para o `main`, o workflow **Verificar compilação** compila o projeto sem lançar nada,
então um erro de código aparece ali antes de chegar a um lançamento.

## Como gerar os executáveis

```bash
# Windows (gera NokyLabs.exe)
dotnet publish src/NokyLabs -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/windows

# Linux (gera o binário NokyLabs)
dotnet publish src/NokyLabs -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/linux
```

No Linux, dê permissão de execução: `chmod +x NokyLabs`.

---

## Instalação: instalado x portátil

Nem todo programa precisa de instalação. O NokyLabs oferece os dois jeitos, e quem escolhe é o **assistente**, que abre sozinho na primeira vez que o `NokyLabs.exe` publicado é executado:

| Etapa | O que dá para escolher |
|---|---|
| 1. Boas-vindas | **Instalar neste computador** ou **Usar sem instalar (modo portátil)** |
| 2. Local | Pasta do NokyLabs, pasta dos apps (com espaço livre) e, se houver uma instalação anterior, **manter tudo** ou fazer uma **instalação limpa** |
| 3. Integração | Atalho na Área de Trabalho, atalho no menu Iniciar, aparecer em **Aplicativos instalados**, iniciar com o sistema, ficar na bandeja |
| 4. Apps | Quais utilitários já instalar agora + atalhos padrão dos apps |
| 5. Preferências | Verificações, avisos, atualizações automáticas e link do catálogo |
| 6. Instalação | Resumo de tudo, depois a instalação com progresso e registro detalhado |
| 7. Concluído | Abrir o NokyLabs agora (ou não) |

**Modo instalado:** o executável é copiado para `%LOCALAPPDATA%\Programs\NokyLabs` (Linux: `~/.local/share/NokyLabs/Launcher`), sem precisar de administrador. Depois, a cópia instalada abre sozinha e o arquivo baixado pode ser apagado.

**Modo portátil:** é criado um arquivo `NokyLabs.portable` ao lado do executável. Com ele, configurações (`NokyLabs-Dados`) e apps (`NokyLabs-Apps`) ficam na mesma pasta, então dá para levar num pendrive (funciona mesmo que a letra do drive mude). O modo portátil não cria atalhos nem mexe no sistema.

**Instalação limpa:** apaga configurações, cache, atalhos, início automático, registro e os apps instalados pelo NokyLabs (só pastas com `nokylabs-app.json`) antes de instalar. Nada fora disso é tocado.

**Desinstalar:** em *Configurações › Aplicativos › Aplicativos instalados* do Windows (botão Desinstalar), ou em *Configurações › NokyLabs neste computador* dentro do launcher. Dá para escolher se os apps e as configurações também saem. O executável é apagado logo depois que o launcher fecha.

**Para reabrir o assistente:** botão em *Configurações › NokyLabs neste computador*, ou rode `NokyLabs.exe --setup`.

**Pelo Visual Studio**, o assistente e o desinstalador rodam em **modo de demonstração**: mostram tudo, mas não alteram nada no computador. Para testar de verdade, use a versão publicada.

### Instalando apps pelo launcher

Ao clicar em **Instalar** num app, abre uma janela com o destino, o espaço livre e as opções:
- Criar atalho na Área de Trabalho
- Adicionar ao menu Iniciar (Linux: menu de aplicativos)
- Abrir assim que terminar
- Lembrar e não perguntar de novo

Os padrões ficam em *Configurações › Instalação de apps*. Os atalhos criados ficam anotados no `nokylabs-app.json` de cada app e são removidos junto quando o app é desinstalado. Atualizações mantêm os atalhos.

## Como o launcher funciona

### 1. Catálogo (`catalogo.json`)

É a lista de apps, versões e links de download. O launcher procura nesta ordem:

1. **Online** — o link configurado em *Configurações › Catálogo de apps* (buscado ao abrir, se "Verificar atualizações ao abrir" estiver ligado, ou no botão "Buscar agora").
2. **Cópia salva** — a última versão online que deu certo (para funcionar sem internet).
3. **Embutido** — `src/NokyLabs/Assets/catalogo.json`, que vai dentro do launcher.

Formato de um app:

```jsonc
{
  "id": "hotswap",                       // vira o nome da subpasta (letras, números, - e _)
  "name": "HotSwap",
  "description": "Troca rápida de resolução…",
  "version": "1.3.0",                    // versão mais recente publicada
  "accent": "#818CF8",                   // cor do card
  "icon": "M7,7 H20 …",                  // path data numa grade 24x24
  "comingSoon": false,
  "downloads": {
    "windows": {
      "url": "https://github.com/ThayTTG/HotSwap/releases/latest/download/HotSwap-win-x64.zip",
      "executable": "HotSwap.exe",
      "sha256": "",                      // opcional
      "published": false                 // vira true quando o Release existir
    },
    "linux": null                        // null ou ausente = "Indisponível no Linux"
  }
}
```

- Enquanto `"published": false` (ou o link estiver vazio), o card mostra "Download em breve" e o botão fica desativado. Quando o Release existir, troque para `true`.
- O `url` aceita um **caminho local**, ótimo para testar antes de publicar. No JSON, use barras duplas: `"C:\\Testes\\HotSwap-win-x64.zip"`.
- O download pode ser um `.zip` (extraído; se tiver uma pasta única dentro, ela é "achatada") ou um executável solto.

O catálogo oficial fica publicado no site, em `https://thayttg.github.io/nokylabs/catalogo.json`. É esse link que vai no campo do catálogo nas Configurações. A cópia na raiz deste projeto e a de `Assets/` devem ser mantidas iguais a ele.

### 2. Detecção do que está instalado

Cada app fica em `<pasta de origem>/<id>/`. Ao instalar, o launcher grava ali um `nokylabs-app.json` com a versão e o executável. Se você copiar um app para lá na mão (sem esse arquivo), ele aparece como "Instalado · versão desconhecida".

Pasta de origem padrão:
- Windows: `%LOCALAPPDATA%\NokyLabs\Apps`
- Linux: `~/.local/share/NokyLabs/Apps`

### 3. Abrir apps

O botão **Abrir** inicia o executável com a pasta do app como diretório de trabalho. O botão de pasta ao lado abre a pasta do app no gerenciador de arquivos (Explorer no Windows, `xdg-open` no Linux).

### 3.1 Apps abertos

Enquanto um app instalado está aberto, o card mostra **Rodando** (em verde). Com o mouse por cima, o botão vira **Encerrar**: o launcher pede para o app fechar, como se você clicasse no X, e só força o encerramento se ele não fechar em 5 segundos. Só contam os processos que rodam a partir da pasta do próprio app.

### 4. Instalar e atualizar

1. Baixa para uma pasta temporária, com barra de progresso real.
2. Confere o SHA-256, se informado.
3. Extrai numa pasta separada (`.id.novo`).
4. Renomeia a versão antiga e só então coloca a nova no lugar. Se o app estiver aberto, a troca falha **sem estragar nada** e o launcher pede para fechá-lo.
5. No Linux, dá a permissão de execução automaticamente.

### 5. Desinstalar

No card de um app instalado, o botão de lixeira pede confirmação ("Remover o X deste computador?"). A pasta do app é primeiro renomeada e só depois apagada: se o app estiver aberto, nada é removido e o launcher pede para fechá-lo.

### 6. Bandeja do sistema

Com *Configurações › Minimizar para a bandeja ao fechar* ligado:
- **Windows:** fechar a janela esconde o launcher, que continua no ícone da bandeja. Clique no ícone para voltar; o menu tem **Abrir**, **Verificar atualizações** e **Sair**.
- **Linux:** como nem todo ambiente mostra a bandeja (o GNOME, por exemplo, precisa de extensão), fechar só **minimiza**. Fechar de novo, já minimizado, encerra de verdade.

### 7. Atualização do próprio launcher

O catálogo tem uma seção `launcher` com a versão mais recente e os downloads por sistema. Quando ela é maior que a versão atual (o `<Version>` do `.csproj`), aparece o aviso **"Nova versão do NokyLabs"** na Biblioteca e em Atualizações.

Como funciona por dentro: um programa aberto não pode ser sobrescrito, mas pode ser **renomeado**. O launcher baixa a versão nova, renomeia o executável atual para `NokyLabs.exe.old`, coloca o novo no lugar e pede para reiniciar. Ao abrir, a versão nova apaga o `.old`.

Importante:
- Só funciona na **versão publicada como arquivo único** (os comandos de `dotnet publish` abaixo). Rodando pelo Visual Studio, aparece "Modo de desenvolvimento" em Configurações › Sobre e a atualização fica desativada.
- O launcher precisa estar numa pasta onde você possa escrever (ex.: Documentos). Em "Arquivos de Programas" o Windows bloqueia a troca.

### 8. Iniciar com o sistema

Feito do jeito nativo, sem scripts e sem precisar de administrador:

- **Windows:** grava o valor `NokyLabs` em `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. É essa chave que aparece em **Gerenciador de Tarefas › Aplicativos de inicialização** (com o nome "NokyLabs Launcher" e o editor "NokyLabs", vindos do `.csproj`). Dá para ligar/desligar tanto pelas Configurações do launcher quanto pelo Gerenciador de Tarefas, e um respeita o outro.
- **Linux:** cria `~/.config/autostart/nokylabs.desktop`, o padrão das sessões gráficas.

Quando o sistema abre o launcher, ele recebe `--minimized`: com a bandeja ligada (Windows) começa só com o ícone; senão, começa minimizado.

> Dica: ligue essa opção na versão **publicada**. No modo de desenvolvimento, quem fica registrado é o executável da pasta `bin`.

### 9. Avisos de novidades

Quando uma verificação encontra atualizações (ou instala alguma sozinha) e o launcher não está em foco, aparece um aviso roxo no canto inferior direito da tela. Clicar nele abre a página de Atualizações. Cada novidade é avisada uma vez só por sessão. O ícone da bandeja também mostra a quantidade ao passar o mouse.

### 10. Verificação periódica e atualizações automáticas

- **Verificar de tempos em tempos:** com um link de catálogo configurado, o launcher procura novidades sozinho enquanto está aberto (internamente, a cada 6 horas; se a internet falhar, tenta de novo em 30 minutos). O intervalo fica em `MainWindowViewModel.CheckInterval`.
- **Atualizar apps automaticamente:** instala as versões novas assim que aparecem. Apps abertos no momento ficam para a próxima verificação.
- **Atualizar o launcher automaticamente:** baixa a versão nova sozinho; ela passa a valer na próxima vez que o launcher abrir (ou na hora, pelo botão "Reiniciar agora").

As duas opções automáticas vêm **desligadas** por padrão.

### 11. Configurações

Salvas automaticamente a cada mudança em:
- Windows: `%APPDATA%\NokyLabs\settings.json`
- Linux: `~/.config/NokyLabs/settings.json`

---

## Quando publicar uma versão nova do launcher

1. Aumente o `<Version>` em `src/NokyLabs/NokyLabs.csproj` (ex.: `0.4.0`).
2. Gere os executáveis com os comandos de `dotnet publish` acima.
3. Compacte cada um (`NokyLabs-win-x64.zip` com o `NokyLabs.exe`; `NokyLabs-linux-x64.zip` com o `NokyLabs`) e anexe num Release do repositório do launcher.
4. No `catalogo.json` publicado, atualize `launcher.version` (e `notes`, se quiser). Quem estiver numa versão anterior vê o aviso na próxima verificação.

**Para testar sem publicar:** gere uma versão 0.4.0, deixe o `.zip` numa pasta, coloque o caminho local no `url` do `launcher` num catálogo de teste, aponte o campo do catálogo para esse arquivo e rode a versão publicada 0.3.0.

## Quando publicar um app no GitHub

1. Gere o executável do app e compacte em `.zip` (ex.: `HotSwap-win-x64.zip`).
2. No repositório do app, crie um **Release** (ex.: tag `v1.3.0`) e anexe o `.zip`.
3. No `catalogo.json` do site (repositório `nokylabs`), troque `"published": false` por `true` no app e confira o nome do arquivo e do executável. O formato `releases/latest/download/…` sempre aponta para o release mais recente.
4. Atualize o `version` do app no catálogo.
5. (Opcional) Gere o SHA-256 no PowerShell com `Get-FileHash .\HotSwap-win-x64.zip` e cole no campo `sha256`.

## Estrutura do projeto

```
NokyLabs/
├─ catalogo.json                     cópia para publicar no GitHub
├─ NokyLabs.sln
└─ src/NokyLabs/
   ├─ Assets/                        ícone "n" + catálogo embutido
   ├─ Styles/                        cores, ícones e estilos
   ├─ Models/                        catálogo, manifesto, configurações, status
   ├─ Services/
   │  ├─ CatalogService.cs           embutido / cache / online
   │  ├─ InstallService.cs           detectar, baixar, extrair, trocar versão
   │  ├─ LaunchService.cs            abrir apps e pastas em cada sistema
   │  ├─ SelfUpdateService.cs        atualização do próprio launcher
   │  ├─ StartupService.cs           iniciar com o sistema (Run do Windows / autostart do Linux)
   │  ├─ SetupService.cs             assistente: instalar, portátil, instalação limpa, desinstalar
   │  ├─ ShortcutService.cs          atalhos .lnk (Windows) e .desktop (Linux)
   │  ├─ SystemRegistrationService   registro em Aplicativos instalados do Windows
   │  ├─ SettingsService.cs          settings.json
   │  ├─ AppPaths.cs                 caminhos por sistema
   │  └─ Shared.cs                   JSON, HttpClient, sistema atual, versões
   ├─ ViewModels/                    lógica das telas (MVVM)
   ├─ Views/MainWindow.axaml(.cs)    a janela
   ├─ Views/LauncherUpdateBanner     aviso de nova versão do launcher
   ├─ Views/NotificationWindow       aviso no canto da tela
   ├─ Views/SetupWindow              assistente de instalação (7 etapas)
   ├─ Views/UninstallWindow          desinstalador
   ├─ Views/AppInstallDialog         opções ao instalar cada app
   └─ App.axaml.cs                   inicialização, bandeja e reinício
```

## Ideias para o futuro

- Assinatura digital do executável (evita o aviso do SmartScreen no Windows)
- Escolher o idioma no assistente
