// Cliente HTTP do painel: sessão do administrador, renovação de token e chamadas da API.

const BASE_URL = String(import.meta.env.VITE_API_URL ?? 'http://localhost:5080').replace(/\/$/, '');
const CHAVE_SESSAO = 'autoflow.admin.sessao';
const CHAVE_APARELHO = 'autoflow.admin.aparelho';

export type TipoUsuario = 'Cliente' | 'Trabalhador' | 'Administrador';
export type StatusUsuario = 'Ativo' | 'PendenteAprovacao' | 'Bloqueado';
export type StatusDenuncia = 'Aberta' | 'EmAnalise' | 'Resolvida';

export interface Usuario {
  id: string;
  nome: string;
  email: string;
  fotoUrl: string | null;
  tipo: TipoUsuario;
  status: StatusUsuario;
  dataCadastro: string;
}

export interface Sessao {
  accessToken: string;
  refreshToken: string;
  expiraEm: string;
  usuario: Usuario;
}

export interface Metricas {
  usuariosAtivos: number;
  totalClientes: number;
  totalTrabalhadores: number;
  trabalhadoresPendentes: number;
  solicitacoesPendentes: number;
  solicitacoesAceitas: number;
  solicitacoesConcluidas: number;
  solicitacoesCanceladas: number;
  volumeTransacoes: number;
  denunciasAbertas: number;
}

export interface TrabalhadorModeracao {
  usuarioId: string;
  nome: string;
  email: string;
  areaAtuacao: string;
  descricao: string;
  cidade: string | null;
  aprovado: boolean;
  status: StatusUsuario;
  dataCadastro: string;
}

export interface Pagina<T> {
  itens: T[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

export interface Denuncia {
  id: string;
  denuncianteId: string;
  denuncianteNome: string;
  denunciadoId: string;
  denunciadoNome: string;
  motivo: string;
  descricao: string | null;
  status: StatusDenuncia;
  resolucao: string | null;
  criadoEm: string;
}

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, mensagem: string) {
    super(mensagem);
    this.status = status;
  }
}

// ---------- Sessão (fica só na aba: sessionStorage) ----------

let sessao: Sessao | null = carregarSessao();
const ouvintes = new Set<() => void>();

function carregarSessao(): Sessao | null {
  try {
    const bruto = sessionStorage.getItem(CHAVE_SESSAO);
    return bruto ? (JSON.parse(bruto) as Sessao) : null;
  } catch {
    return null;
  }
}

function definirSessao(nova: Sessao | null) {
  sessao = nova;
  if (nova) sessionStorage.setItem(CHAVE_SESSAO, JSON.stringify(nova));
  else sessionStorage.removeItem(CHAVE_SESSAO);
  ouvintes.forEach((avisar) => avisar());
}

export function obterSessao(): Sessao | null {
  return sessao;
}

export function assinarSessao(avisar: () => void): () => void {
  ouvintes.add(avisar);
  return () => ouvintes.delete(avisar);
}

function idDoAparelho(): string {
  let id = localStorage.getItem(CHAVE_APARELHO);
  if (!id) {
    id = `painel-${crypto.randomUUID()}`;
    localStorage.setItem(CHAVE_APARELHO, id);
  }
  return id;
}

// ---------- Requisições ----------

async function lerErro(resposta: Response): Promise<ApiError> {
  const problema = (await resposta.json().catch(() => null)) as { detail?: string; title?: string } | null;
  return new ApiError(resposta.status, problema?.detail ?? problema?.title ?? `Erro ${resposta.status}`);
}

function enviar(metodo: string, caminho: string, corpo?: unknown): Promise<Response> {
  const cabecalhos: Record<string, string> = {};
  if (corpo !== undefined) cabecalhos['Content-Type'] = 'application/json';
  if (sessao) cabecalhos.Authorization = `Bearer ${sessao.accessToken}`;
  return fetch(BASE_URL + caminho, {
    method: metodo,
    headers: cabecalhos,
    body: corpo === undefined ? undefined : JSON.stringify(corpo),
  });
}

let renovacaoEmCurso: Promise<boolean> | null = null;

// Uma renovação por vez: o refresh token é rotacionado a cada uso.
function renovarSessao(): Promise<boolean> {
  renovacaoEmCurso ??= (async () => {
    try {
      if (!sessao) return false;
      const resposta = await fetch(`${BASE_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: sessao.refreshToken, deviceId: idDoAparelho() }),
      });
      if (!resposta.ok) return false;
      definirSessao((await resposta.json()) as Sessao);
      return true;
    } catch {
      return false;
    } finally {
      renovacaoEmCurso = null;
    }
  })();
  return renovacaoEmCurso;
}

async function requisitar<T>(metodo: string, caminho: string, corpo?: unknown): Promise<T> {
  let resposta = await enviar(metodo, caminho, corpo);
  if (resposta.status === 401 && (await renovarSessao())) {
    resposta = await enviar(metodo, caminho, corpo);
  }
  if (resposta.status === 401) definirSessao(null);
  if (!resposta.ok) throw await lerErro(resposta);
  if (resposta.status === 204) return undefined as T;
  return (await resposta.json()) as T;
}

// ---------- Autenticação ----------

export async function entrar(email: string, senha: string): Promise<void> {
  const resposta = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, senha, deviceId: idDoAparelho() }),
  });
  if (!resposta.ok) throw await lerErro(resposta);

  const nova = (await resposta.json()) as Sessao;
  if (nova.usuario.tipo !== 'Administrador') {
    throw new ApiError(403, 'Este painel é restrito a administradores.');
  }
  definirSessao(nova);
}

export async function sair(): Promise<void> {
  try {
    await enviar('POST', '/api/auth/logout', { deviceId: idDoAparelho() });
  } finally {
    definirSessao(null);
  }
}

// ---------- Endpoints do painel ----------

export const api = {
  metricas: () => requisitar<Metricas>('GET', '/api/admin/metricas'),

  trabalhadores: (aprovado?: boolean) =>
    requisitar<TrabalhadorModeracao[]>(
      'GET',
      `/api/admin/trabalhadores${aprovado === undefined ? '' : `?aprovado=${aprovado}`}`,
    ),
  aprovarTrabalhador: (id: string) => requisitar<void>('POST', `/api/admin/trabalhadores/${id}/aprovar`),
  reprovarTrabalhador: (id: string) => requisitar<void>('POST', `/api/admin/trabalhadores/${id}/reprovar`),

  usuarios: (termo: string, pagina: number) =>
    requisitar<Pagina<Usuario>>(
      'GET',
      `/api/admin/usuarios?pagina=${pagina}&tamanhoPagina=15&termo=${encodeURIComponent(termo)}`,
    ),
  alterarStatusUsuario: (id: string, status: StatusUsuario) =>
    requisitar<void>('PUT', `/api/admin/usuarios/${id}/status`, { status }),

  denuncias: (status?: StatusDenuncia) =>
    requisitar<Denuncia[]>('GET', `/api/admin/denuncias${status ? `?status=${status}` : ''}`),
  atualizarDenuncia: (id: string, status: StatusDenuncia, resolucao: string) =>
    requisitar<void>('PUT', `/api/admin/denuncias/${id}`, { status, resolucao: resolucao || null }),
};
