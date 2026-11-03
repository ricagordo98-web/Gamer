# Gamer

Gestão do backlog de jogos (substitui o Excel `Jogos Backlog.xlsx`). Uma só UI Blazor, três formas de a usar: web (PWA), app Windows e app Android, todas a falar com a mesma API.

## Estrutura

| Projeto | O que faz |
|---|---|
| `src/Gamer.Contracts` | DTOs e enums partilhados entre API e clientes |
| `src/Gamer.Api` | ASP.NET Core minimal API + EF Core (SQLite) + JWT. Serve também a app web |
| `src/Gamer.Ui` | Razor Class Library com **todas** as páginas, componentes, CSS e serviços de cliente |
| `src/Gamer.Web` | Host Blazor WebAssembly (só liga `Gamer.Ui` ao browser) |
| `src/Gamer.Maui` | Host MAUI Blazor Hybrid (Windows e Android; iOS/Mac só compilam num Mac) |
| `tests/Gamer.Tests` | xUnit + FluentAssertions + NSubstitute |
| `tools/` | Scripts: `build-release.ps1`, `Gamer.bat`, `make-icons.ps1` |

## Como as peças encaixam

```
Gamer.Web  ─┐
            ├─> Gamer.Ui ──HTTP/JWT──> Gamer.Api ──> SQLite (data/gamer.db)
Gamer.Maui ─┘
```

- Os hosts só fornecem o que depende da plataforma: `IKeyValueStore` (localStorage no web, `Preferences` no MAUI) e o endereço do servidor por omissão. Tudo o resto está em `Gamer.Ui`.
- O estado da UI vive em `GameLibrary` (lista, filtros, chamadas à API). As páginas só desenham.
- A API está organizada por funcionalidade: `Features/Games`, `Features/Auth`, `Features/Platforms`, `Features/Import`.

## Modelo de dados

- `Game`: título, plataforma, `GameStatus` (`Backlog`, `Playing`, `Completed`), `OnDeck` (corre no Steam Deck), nota 1-10, notas, datas.
- `Platform`: nome, família (PlayStation, Nintendo, PC, Other) e formato (físico/digital). As 16 plataformas do Excel são semeadas na migração.
- Mapeamento do Excel: `Terminado=1` → Completed; senão `A Jogar=1` → Playing; senão Backlog. `Deck=1` → `OnDeck`. O prefixo `L -` da consola = físico, `PC -` = digital.

## Correr em desenvolvimento

Requer o SDK em `global.json` (.NET 10) e o workload MAUI.

```powershell
dotnet run --project src/Gamer.Api            # API + web em http://localhost:5199 (depois de publicar o web, ver abaixo)
dotnet run --project src/Gamer.Web            # web em modo dev (no Login, em "Servidor", aponta para http://localhost:5199)
dotnet test                                   # testes
dotnet build src/Gamer.Maui -f net10.0-windows10.0.19041.0
```

Migrações EF (ferramenta local em `tools/ef`, não versionada):

```powershell
dotnet tool install dotnet-ef --tool-path tools/ef --version 10.0.0
tools/ef/dotnet-ef migrations add <Nome> -p src/Gamer.Api -o Data/Migrations
```

A API aplica as migrações sozinha no arranque.

## Release (Windows)

```powershell
powershell -ExecutionPolicy Bypass -File tools/build-release.ps1
```

Gera `Release/` com `Api/` (API + web), `App/` (app Windows), `Gamer.bat` e `gamer.ico`, e cria o atalho **Gamer** no Desktop. A base de dados (`Release/Api/data`) é preservada entre builds.

`Gamer.bat` arranca a API em segundo plano (se ainda não estiver a correr), abre a app e, ao fechar a app, pára a API que ele próprio arrancou.

## Configuração (`Release/Api/appsettings.json`)

- `Urls`: `http://0.0.0.0:5199` (acessível na rede local, necessário para o telemóvel).
- `Auth:Username` / `Auth:Password`: **muda a palavra-passe por omissão** (`ricardo` / `gamer`). `Auth:JwtKey` deve ter 32+ caracteres; muda-a também.
- `ConnectionStrings:Gamer`: caminho do SQLite, relativo à pasta da API.

## Usar no telemóvel

1. Garante que o PC e o telemóvel estão na mesma rede e que a Firewall do Windows permite a porta 5199 (a primeira execução da API pede autorização).
2. Web/PWA: abre `http://<IP-do-PC>:5199` no browser do telemóvel e usa "Adicionar ao ecrã principal".
3. App Android: compila `dotnet build src/Gamer.Maui -f net10.0-android -c Release` e instala o APK; no Login, em "Servidor", mete `http://<IP-do-PC>:5199`.

## Importar o Excel

Definições → Importar do Excel → escolhe o `.xlsx` (colunas `Jogo`, `Consola`, `A Jogar`, `Terminado`, `Deck`). Jogos repetidos (mesmo título e plataforma) são ignorados e listados.

## Ícone

`tools/make-icons.ps1` gera `assets/gamer.ico` e os PNG da PWA. O mesmo desenho existe em SVG em `Gamer.Ui/Components/Logo.razor` e `Gamer.Maui/Resources/AppIcon/`.
