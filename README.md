# AutoFlow

Plataforma que conecta clientes a prestadores de serviço: busca por área de atuação, proximidade, avaliação e preço, agendamento, chat em tempo real e um painel de moderação.

| Parte | Tecnologia | Pasta |
| --- | --- | --- |
| API | ASP.NET Core (.NET 10), EF Core, SignalR | [`backend/`](backend) |
| Banco de dados | Microsoft SQL Server / Azure SQL | [`database/schema.sql`](database/schema.sql) |
| Painel administrativo | React, Vite, Mantine | [`admin/`](admin) |
| App Android e iOS | .NET MAUI | [`mobile/`](mobile) |

## Estrutura

```
backend/
  src/AutoFlow.Domain          Entidades, enums e regras de negócio (sem dependências)
  src/AutoFlow.Contracts       DTOs compartilhados entre a API e o app MAUI
  src/AutoFlow.Application     Casos de uso (serviços) e interfaces
  src/AutoFlow.Infrastructure  EF Core/SQL Server, criptografia, JWT, Google
  src/AutoFlow.Api             Controllers REST, hub SignalR, configuração
  tests/AutoFlow.Tests         Testes com xUnit e Moq
  tests/carga/chat.js          Teste de carga do chat (k6)
admin/                         Painel administrativo
mobile/AutoFlow.Mobile         App .NET MAUI
database/schema.sql            Modelo relacional
docs/deploy.md                 Implantação e publicação nas lojas
```

## Como rodar em desenvolvimento

Pré-requisitos: .NET SDK 10, Node.js 22, Docker e, para o app, o workload MAUI (`dotnet workload install maui`).

### 1. Banco e API

```bash
docker compose up -d sqlserver
dotnet run --project backend/src/AutoFlow.Api
```

A API sobe em `http://localhost:5080` (`/health` para conferir, `/openapi/v1.json` para a especificação). Em desenvolvimento ela cria o banco e estes usuários:

| Perfil | E-mail | Senha |
| --- | --- | --- |
| Administrador | `admin@autoflow.dev` | `Admin@12345` |
| Cliente | `cliente@autoflow.dev` | `Demo@12345` |
| Trabalhador (aprovado) | `trabalhador@autoflow.dev` | `Demo@12345` |

Para subir banco e API juntos em contêineres: `docker compose up -d --build`.

### 2. Painel administrativo

```bash
cd admin
npm install
npm run dev
```

Abra `http://localhost:5173` e entre com o administrador acima. A URL da API vem de `VITE_API_URL` (veja `admin/.env.example`).

### 3. App mobile

```bash
dotnet build mobile/AutoFlow.Mobile/AutoFlow.Mobile.csproj -t:Run -f net10.0-android
```

Em compilações Debug o app aponta para a API local (`http://10.0.2.2:5080` no emulador Android) e mostra, na tela de login, um acesso por e-mail e senha para os usuários de demonstração. O iOS (`-f net10.0-ios`) precisa de um Mac.

O botão **Entrar com Google** depende das credenciais OAuth descritas em [docs/deploy.md](docs/deploy.md#login-com-google).

## Configuração

As chaves ficam em `appsettings.json` (vazias) e são preenchidas por variáveis de ambiente em produção. Os valores de `appsettings.Development.json` servem apenas para desenvolvimento local.

| Chave | Para que serve |
| --- | --- |
| `ConnectionStrings:Padrao` | Conexão com o SQL Server |
| `Jwt:Chave` | Chave de assinatura dos tokens (32 caracteres ou mais) |
| `Criptografia:ChaveBase64` | Chave AES-256 dos campos sensíveis (`openssl rand -base64 32`) |
| `Google:ClientId`, `Google:ClientSecret` | Credenciais OAuth do login com Google |
| `Cors:Origens` | Endereços do painel autorizados a chamar a API |
| `Seed:AdminEmail`, `Seed:AdminSenha` | Primeiro administrador, criado se não houver nenhum |
| `Banco:InicializarAoSubir` | Cria o banco (se não existir) e os dados iniciais ao iniciar |

## Segurança

- **Em trânsito:** HTTPS obrigatório em produção (HSTS e redirecionamento).
- **Em repouso:** telefone, documento e o texto das mensagens são cifrados com AES-256-GCM no código antes de chegar ao banco; senhas usam BCrypt; do refresh token só o SHA-256 é gravado. No Azure SQL, o TDE cifra o banco inteiro.
- **Autenticação:** login com Google (OAuth 2.0 / OpenID Connect) com o ID token validado na API; o painel usa e-mail e senha. A API emite um JWT de 15 minutos e um refresh token por aparelho, rotacionado a cada uso.
- **PIN / biometria:** o app guarda o refresh token no Keystore/Keychain e só o usa depois do desbloqueio pelo bloqueio de tela do aparelho, ao abrir e ao voltar depois de 2 minutos em segundo plano.
- **Abuso:** limite de tentativas por IP nos endpoints de autenticação; contas bloqueadas perdem as sessões.

## Testes

```bash
dotnet test backend/AutoFlow.slnx                                   # unitários e de integração (EF Core em memória)
k6 run -e VUS=200 -e DURACAO=2m backend/tests/carga/chat.js         # carga do chat (API em Development)
```

O GitHub Actions ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) compila e testa o backend, compila o painel e compila o app para Android a cada push.

## Correspondência com o roteiro

| Fase | Onde está |
| --- | --- |
| 1. Arquitetura e modelagem | `backend/src/AutoFlow.Domain`, `database/schema.sql` |
| 2. Backend, segurança e chat | `backend/src` |
| 3. Painel administrativo | `admin/` |
| 4. App mobile | `mobile/AutoFlow.Mobile` |
| 5. Testes e QA | `backend/tests`, CI |
| 6. Implantação e publicação | `backend/Dockerfile`, `docker-compose.yml`, `docs/deploy.md` |

Além das tabelas do roteiro, o modelo inclui `HorariosAtendimento`, `Avaliacoes`, `Denuncias` e `SessoesDispositivo`, exigidas pela busca por avaliação, pela gestão de denúncias e pelas sessões por aparelho.

## Fora do escopo desta versão

- **Pagamentos:** não há integração com gateway. O "volume de transações" do dashboard soma o valor dos atendimentos concluídos.
- **Notificações push** e **envio de documentos** na moderação.
- **Migrations do EF Core:** o esquema é criado pela API em desenvolvimento ou por `database/schema.sql`.
