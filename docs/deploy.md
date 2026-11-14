# Implantação e publicação

## Backend e banco de dados

### Banco: Azure SQL Database (ou SQL Server gerenciado)

1. Crie o servidor e o banco `AutoFlow`. No Azure SQL, o **TDE (Transparent Data Encryption)** e os **backups automáticos** com restauração pontual já vêm ativos; confira a retenção em *Backups* e ajuste conforme a necessidade.
2. Libere no firewall apenas a origem da API (ou use endpoint privado).
3. Aplique o esquema: execute [`database/schema.sql`](../database/schema.sql) no banco.
4. Crie um usuário de banco próprio para a API, com permissão de leitura e escrita, em vez de usar o administrador do servidor.

### API: Azure App Service (contêiner)

A imagem é gerada por [`backend/Dockerfile`](../backend/Dockerfile) e escuta na porta 8080. O mesmo contêiner roda no AWS Elastic Beanstalk ou em uma máquina da DigitalOcean.

```bash
docker build -t autoflow-api ./backend
```

Configure estas variáveis de ambiente no serviço (use dois sublinhados para separar os níveis):

| Variável | Valor |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Padrao` | Conexão com o Azure SQL (`Encrypt=True`) |
| `Jwt__Chave` | Texto aleatório com 32 caracteres ou mais |
| `Criptografia__ChaveBase64` | Saída de `openssl rand -base64 32` |
| `Google__ClientId`, `Google__ClientSecret` | Credenciais OAuth (abaixo) |
| `Cors__Origens__0` | URL do painel, por exemplo `https://painel.seudominio.com.br` |
| `Seed__AdminEmail`, `Seed__AdminSenha` | Primeiro administrador |
| `Banco__InicializarAoSubir` | `true` no primeiro deploy, para criar o administrador; depois `false` |

Guarde as chaves em um cofre (Azure Key Vault) e nunca no repositório. **A chave `Criptografia__ChaveBase64` não pode ser perdida nem trocada sem migrar os dados:** sem ela, telefones, documentos e mensagens gravados ficam ilegíveis.

Ative no App Service: *HTTPS Only*, *WebSockets* (usados pelo SignalR) e *Always On*. Ao escalar para mais de uma instância, ative a afinidade de sessão (ARR Affinity) ou adicione o Azure SignalR Service, para que as conexões do chat alcancem todos os usuários.

### Painel administrativo

```bash
cd admin
VITE_API_URL=https://api.seudominio.com.br npm run build
```

Publique a pasta `admin/dist` em uma hospedagem de arquivos estáticos (Azure Static Web Apps, por exemplo) com redirecionamento de todas as rotas para `index.html`. Inclua a URL do painel em `Cors__Origens__0` na API.

## Login com Google

1. No Google Cloud Console, crie um projeto e configure a tela de consentimento OAuth.
2. Em *Credenciais*, crie um **ID do cliente OAuth** do tipo **Aplicativo da Web**.
3. Em *URIs de redirecionamento autorizados*, cadastre `https://api.seudominio.com.br/api/auth/google/callback`.
4. Copie o ID e a chave secreta para `Google__ClientId` e `Google__ClientSecret`.

O app abre o navegador do sistema em `/api/auth/google/mobile`; a API conduz o fluxo OAuth, valida o ID token do Google e devolve os tokens do AutoFlow para `autoflow://auth`.

Em desenvolvimento, o Google só aceita redirecionamento HTTP para `localhost`. Para testar o login em um emulador ou aparelho, exponha a API por um túnel HTTPS (Dev Tunnels ou ngrok), cadastre a URL do túnel no passo 3 e informe-a em `Google:UrlCallback`.

## Publicação do app

Antes de gerar as versões de loja, troque a URL de produção em [`Services/Configuracao.cs`](../mobile/AutoFlow.Mobile/Services/Configuracao.cs) e os ícones em `Resources/AppIcon`. O identificador do app é `br.com.autoflow.app` (em `AutoFlow.Mobile.csproj`).

### Google Play

1. Gere a chave de upload uma única vez e guarde-a fora do repositório:
   ```bash
   keytool -genkeypair -v -keystore autoflow.keystore -alias autoflow -keyalg RSA -keysize 2048 -validity 10000
   ```
2. Gere o pacote assinado (`.aab`):
   ```bash
   dotnet publish mobile/AutoFlow.Mobile/AutoFlow.Mobile.csproj -f net10.0-android -c Release \
     -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=/caminho/autoflow.keystore \
     -p:AndroidSigningKeyAlias=autoflow -p:AndroidSigningKeyPass=SENHA -p:AndroidSigningStorePass=SENHA
   ```
3. No Google Play Console, crie o app, preencha a ficha da loja, a classificação de conteúdo e o formulário de segurança dos dados (o app coleta nome, e-mail, localização aproximada e mensagens).
4. Envie o `.aab` para o teste interno, valide e promova para produção.

### App Store

Requer um Mac com Xcode e uma conta no Apple Developer Program.

1. No portal da Apple, registre o identificador `br.com.autoflow.app`, crie o certificado de distribuição e o perfil de provisionamento da App Store.
2. Gere o `.ipa`:
   ```bash
   dotnet publish mobile/AutoFlow.Mobile/AutoFlow.Mobile.csproj -f net10.0-ios -c Release \
     -p:ArchiveOnBuild=true -p:CodesignKey="Apple Distribution: SUA EMPRESA (TEAMID)" \
     -p:CodesignProvision="AutoFlow App Store"
   ```
3. Envie pelo Transporter para o App Store Connect, teste no TestFlight e submeta para revisão. Preencha a seção de privacidade do app com os mesmos dados informados no Google Play.

## Conferência antes do lançamento

- [ ] Chaves de produção diferentes das de desenvolvimento e guardadas em cofre
- [ ] `Banco__InicializarAoSubir` desligado depois do primeiro deploy e senha do administrador trocada
- [ ] HTTPS obrigatório e WebSockets ativos na API
- [ ] Backup do banco testado com uma restauração
- [ ] Teste de carga do chat executado contra o ambiente de homologação
- [ ] Política de privacidade e termos de uso publicados (LGPD), com canal para pedidos dos titulares
