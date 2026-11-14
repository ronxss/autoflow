/* ============================================================================
   AutoFlow — modelo relacional (Microsoft SQL Server / Azure SQL Database)

   Este script cria o mesmo esquema que o EF Core gera a partir de
   AutoFlowDbContext. Use-o para provisionar o banco de produção; em
   desenvolvimento a própria API cria o banco (Banco:InicializarAoSubir).

   Convenções:
   - Todas as datas são UTC (DATETIME2).
   - Colunas marcadas como [cifrado] guardam texto criptografado com AES-256-GCM
     pela aplicação ("v1:" + Base64) — o banco nunca vê o valor em claro.
   - Em repouso, ative também o TDE (padrão no Azure SQL Database).
   ============================================================================ */

CREATE TABLE dbo.Usuarios (
    Id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
    Nome          NVARCHAR(150)    NOT NULL,
    Email         NVARCHAR(256)    NOT NULL,
    GoogleId      NVARCHAR(64)     NULL,
    SenhaHash     NVARCHAR(100)    NULL,                -- BCrypt; nulo para login social
    Telefone      NVARCHAR(256)    NULL,                -- [cifrado]
    Documento     NVARCHAR(256)    NULL,                -- [cifrado] CPF/CNPJ
    FotoUrl       NVARCHAR(500)    NULL,
    Tipo          NVARCHAR(20)     NOT NULL,
    Status        NVARCHAR(20)     NOT NULL,
    DataCadastro  DATETIME2        NOT NULL,
    CONSTRAINT CK_Usuarios_Tipo   CHECK (Tipo IN (N'Cliente', N'Trabalhador', N'Administrador')),
    CONSTRAINT CK_Usuarios_Status CHECK (Status IN (N'Ativo', N'PendenteAprovacao', N'Bloqueado'))
);
CREATE UNIQUE INDEX IX_Usuarios_Email    ON dbo.Usuarios (Email);
CREATE UNIQUE INDEX IX_Usuarios_GoogleId ON dbo.Usuarios (GoogleId) WHERE GoogleId IS NOT NULL;

CREATE TABLE dbo.PerfisTrabalhadores (
    UsuarioId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PerfisTrabalhadores PRIMARY KEY,
    AreaAtuacao      NVARCHAR(100)    NOT NULL,
    Descricao        NVARCHAR(2000)   NOT NULL,
    PrecoBaseHora    DECIMAL(10, 2)   NOT NULL,
    Cidade           NVARCHAR(120)    NULL,
    Latitude         FLOAT            NULL,
    Longitude        FLOAT            NULL,
    Aprovado         BIT              NOT NULL,
    AvaliacaoMedia   FLOAT            NOT NULL,
    TotalAvaliacoes  INT              NOT NULL,
    CONSTRAINT FK_PerfisTrabalhadores_Usuarios FOREIGN KEY (UsuarioId)
        REFERENCES dbo.Usuarios (Id) ON DELETE CASCADE
);
CREATE INDEX IX_PerfisTrabalhadores_AreaAtuacao ON dbo.PerfisTrabalhadores (AreaAtuacao);

-- Grade semanal de atendimento (horário de Brasília). DiaSemana: 0 = domingo ... 6 = sábado.
CREATE TABLE dbo.HorariosAtendimento (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HorariosAtendimento PRIMARY KEY,
    TrabalhadorId  UNIQUEIDENTIFIER NOT NULL,
    DiaSemana      INT              NOT NULL,
    HoraInicio     TIME             NOT NULL,
    HoraFim        TIME             NOT NULL,
    CONSTRAINT FK_HorariosAtendimento_PerfisTrabalhadores FOREIGN KEY (TrabalhadorId)
        REFERENCES dbo.PerfisTrabalhadores (UsuarioId) ON DELETE CASCADE,
    CONSTRAINT CK_HorariosAtendimento_Dia   CHECK (DiaSemana BETWEEN 0 AND 6),
    CONSTRAINT CK_HorariosAtendimento_Faixa CHECK (HoraInicio < HoraFim)
);
CREATE INDEX IX_HorariosAtendimento_TrabalhadorId ON dbo.HorariosAtendimento (TrabalhadorId);

CREATE TABLE dbo.Servicos (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Servicos PRIMARY KEY,
    TrabalhadorId   UNIQUEIDENTIFIER NOT NULL,
    Nome            NVARCHAR(150)    NOT NULL,
    Descricao       NVARCHAR(1000)   NULL,
    Preco           DECIMAL(10, 2)   NOT NULL,
    DuracaoMinutos  INT              NOT NULL,
    Ativo           BIT              NOT NULL,
    CONSTRAINT FK_Servicos_PerfisTrabalhadores FOREIGN KEY (TrabalhadorId)
        REFERENCES dbo.PerfisTrabalhadores (UsuarioId) ON DELETE CASCADE
);
CREATE INDEX IX_Servicos_TrabalhadorId ON dbo.Servicos (TrabalhadorId);

CREATE TABLE dbo.SolicitacoesServico (
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SolicitacoesServico PRIMARY KEY,
    ClienteId         UNIQUEIDENTIFIER NOT NULL,
    TrabalhadorId     UNIQUEIDENTIFIER NOT NULL,
    ServicoId         UNIQUEIDENTIFIER NOT NULL,
    Status            NVARCHAR(20)     NOT NULL,
    DataHoraAgendada  DATETIME2        NOT NULL,
    Observacoes       NVARCHAR(1000)   NULL,
    PrecoAcordado     DECIMAL(10, 2)   NOT NULL,
    CriadoEm          DATETIME2        NOT NULL,
    AtualizadoEm      DATETIME2        NOT NULL,
    CONSTRAINT FK_SolicitacoesServico_Cliente     FOREIGN KEY (ClienteId)     REFERENCES dbo.Usuarios (Id),
    CONSTRAINT FK_SolicitacoesServico_Trabalhador FOREIGN KEY (TrabalhadorId) REFERENCES dbo.Usuarios (Id),
    CONSTRAINT FK_SolicitacoesServico_Servicos    FOREIGN KEY (ServicoId)     REFERENCES dbo.Servicos (Id),
    CONSTRAINT CK_SolicitacoesServico_Status CHECK
        (Status IN (N'Pendente', N'Aceito', N'Concluido', N'Cancelado', N'Recusado'))
);
CREATE INDEX IX_SolicitacoesServico_Cliente     ON dbo.SolicitacoesServico (ClienteId, Status);
CREATE INDEX IX_SolicitacoesServico_Trabalhador ON dbo.SolicitacoesServico (TrabalhadorId, Status, DataHoraAgendada);
CREATE INDEX IX_SolicitacoesServico_ServicoId   ON dbo.SolicitacoesServico (ServicoId);

CREATE TABLE dbo.MensagensChat (
    Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MensagensChat PRIMARY KEY,
    RemetenteId     UNIQUEIDENTIFIER NOT NULL,
    DestinatarioId  UNIQUEIDENTIFIER NOT NULL,
    SolicitacaoId   UNIQUEIDENTIFIER NOT NULL,
    Texto           NVARCHAR(MAX)    NOT NULL,          -- [cifrado]
    EnviadoEm       DATETIME2        NOT NULL,
    Lido            BIT              NOT NULL,
    CONSTRAINT FK_MensagensChat_Remetente    FOREIGN KEY (RemetenteId)    REFERENCES dbo.Usuarios (Id),
    CONSTRAINT FK_MensagensChat_Destinatario FOREIGN KEY (DestinatarioId) REFERENCES dbo.Usuarios (Id),
    CONSTRAINT FK_MensagensChat_Solicitacoes FOREIGN KEY (SolicitacaoId)
        REFERENCES dbo.SolicitacoesServico (Id) ON DELETE CASCADE
);
CREATE INDEX IX_MensagensChat_Conversa       ON dbo.MensagensChat (SolicitacaoId, EnviadoEm);
CREATE INDEX IX_MensagensChat_RemetenteId    ON dbo.MensagensChat (RemetenteId);
CREATE INDEX IX_MensagensChat_DestinatarioId ON dbo.MensagensChat (DestinatarioId);

CREATE TABLE dbo.Avaliacoes (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Avaliacoes PRIMARY KEY,
    SolicitacaoId  UNIQUEIDENTIFIER NOT NULL,
    ClienteId      UNIQUEIDENTIFIER NOT NULL,
    TrabalhadorId  UNIQUEIDENTIFIER NOT NULL,
    Nota           INT              NOT NULL,
    Comentario     NVARCHAR(1000)   NULL,
    CriadoEm       DATETIME2        NOT NULL,
    CONSTRAINT FK_Avaliacoes_Solicitacoes FOREIGN KEY (SolicitacaoId)
        REFERENCES dbo.SolicitacoesServico (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Avaliacoes_Cliente     FOREIGN KEY (ClienteId)     REFERENCES dbo.Usuarios (Id),
    CONSTRAINT FK_Avaliacoes_Trabalhador FOREIGN KEY (TrabalhadorId) REFERENCES dbo.Usuarios (Id),
    CONSTRAINT CK_Avaliacoes_Nota CHECK (Nota BETWEEN 1 AND 5)
);
CREATE UNIQUE INDEX IX_Avaliacoes_SolicitacaoId ON dbo.Avaliacoes (SolicitacaoId);
CREATE INDEX IX_Avaliacoes_TrabalhadorId        ON dbo.Avaliacoes (TrabalhadorId);
CREATE INDEX IX_Avaliacoes_ClienteId            ON dbo.Avaliacoes (ClienteId);

CREATE TABLE dbo.Denuncias (
    Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Denuncias PRIMARY KEY,
    DenuncianteId  UNIQUEIDENTIFIER NOT NULL,
    DenunciadoId   UNIQUEIDENTIFIER NOT NULL,
    Motivo         NVARCHAR(200)    NOT NULL,
    Descricao      NVARCHAR(2000)   NULL,
    Status         NVARCHAR(20)     NOT NULL,
    Resolucao      NVARCHAR(2000)   NULL,
    CriadoEm       DATETIME2        NOT NULL,
    ResolvidoEm    DATETIME2        NULL,
    CONSTRAINT FK_Denuncias_Denunciante FOREIGN KEY (DenuncianteId) REFERENCES dbo.Usuarios (Id),
    CONSTRAINT FK_Denuncias_Denunciado  FOREIGN KEY (DenunciadoId)  REFERENCES dbo.Usuarios (Id),
    CONSTRAINT CK_Denuncias_Status CHECK (Status IN (N'Aberta', N'EmAnalise', N'Resolvida'))
);
CREATE INDEX IX_Denuncias_Status        ON dbo.Denuncias (Status);
CREATE INDEX IX_Denuncias_DenuncianteId ON dbo.Denuncias (DenuncianteId);
CREATE INDEX IX_Denuncias_DenunciadoId  ON dbo.Denuncias (DenunciadoId);

-- Sessões por aparelho: guarda só o SHA-256 do refresh token, renovado após PIN/biometria.
CREATE TABLE dbo.SessoesDispositivo (
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SessoesDispositivo PRIMARY KEY,
    UsuarioId         UNIQUEIDENTIFIER NOT NULL,
    DeviceId          NVARCHAR(100)    NOT NULL,
    RefreshTokenHash  NVARCHAR(64)     NOT NULL,
    CriadoEm          DATETIME2        NOT NULL,
    ExpiraEm          DATETIME2        NOT NULL,
    RevogadoEm        DATETIME2        NULL,
    CONSTRAINT FK_SessoesDispositivo_Usuarios FOREIGN KEY (UsuarioId)
        REFERENCES dbo.Usuarios (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_SessoesDispositivo_RefreshTokenHash ON dbo.SessoesDispositivo (RefreshTokenHash);
CREATE INDEX IX_SessoesDispositivo_Usuario ON dbo.SessoesDispositivo (UsuarioId, DeviceId);
