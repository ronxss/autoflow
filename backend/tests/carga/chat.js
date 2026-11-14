// Teste de carga do chat (k6): vários usuários virtuais conversando ao mesmo tempo
// pelo hub SignalR, para medir a estabilidade do SignalR e do SQL Server.
//
// Pré-requisito: API rodando em Development (cria os usuários de demonstração).
//   k6 run -e BASE_URL=http://localhost:5080 -e VUS=200 -e DURACAO=2m backend/tests/carga/chat.js
import http from 'k6/http';
import ws from 'k6/ws';
import { check, fail } from 'k6';
import { Counter, Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080';
const SEPARADOR = '\x1e'; // terminador de registro do protocolo JSON do SignalR
const DURACAO_SESSAO_MS = 20000;

const enviadas = new Counter('chat_mensagens_enviadas');
const recebidas = new Counter('chat_mensagens_recebidas');
const latencia = new Trend('chat_latencia_ms', true);

export const options = {
  vus: Number(__ENV.VUS || 50),
  duration: __ENV.DURACAO || '1m',
  thresholds: {
    chat_latencia_ms: ['p(95)<1000'],
    checks: ['rate>0.99'],
  },
};

const json = { headers: { 'Content-Type': 'application/json' } };

function entrar(email, deviceId) {
  const resposta = http.post(`${BASE_URL}/api/auth/login`,
    JSON.stringify({ email, senha: 'Demo@12345', deviceId }), json);
  if (resposta.status !== 200) fail(`login de ${email} falhou: ${resposta.status}`);
  return resposta.json();
}

function autenticado(token) {
  return { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } };
}

// Roda uma vez: cria (ou reaproveita) uma solicitação entre os usuários de demonstração.
export function setup() {
  const cliente = entrar('cliente@autoflow.dev', 'k6-setup-cliente');
  const trabalhador = entrar('trabalhador@autoflow.dev', 'k6-setup-trabalhador');

  const existentes = http.get(`${BASE_URL}/api/solicitacoes?status=Pendente`, autenticado(cliente.accessToken)).json();
  if (existentes.length > 0) return { solicitacaoId: existentes[0].id };

  const perfil = http.get(`${BASE_URL}/api/trabalhadores/${trabalhador.usuario.id}`,
    autenticado(cliente.accessToken)).json();

  // Próxima quarta-feira às 15:00 UTC (12:00 em Brasília), dentro da grade de demonstração.
  const data = new Date();
  data.setUTCDate(data.getUTCDate() + ((3 - data.getUTCDay() + 7) % 7 || 7));
  data.setUTCHours(15, 0, 0, 0);

  const criada = http.post(`${BASE_URL}/api/solicitacoes`, JSON.stringify({
    servicoId: perfil.servicos[0].id, dataHoraAgendada: data.toISOString(), observacoes: 'Teste de carga',
  }), autenticado(cliente.accessToken));
  if (criada.status !== 200) fail(`não foi possível criar a solicitação: ${criada.status} ${criada.body}`);
  return { solicitacaoId: criada.json().id };
}

// Cada usuário virtual entra como cliente (VUs pares) ou trabalhador (ímpares),
// com um deviceId próprio, e troca mensagens na mesma conversa.
export default function (dados) {
  const email = __VU % 2 === 0 ? 'cliente@autoflow.dev' : 'trabalhador@autoflow.dev';
  const sessao = entrar(email, `k6-vu-${__VU}`);
  const url = `${BASE_URL.replace(/^http/, 'ws')}/hubs/chat?access_token=${encodeURIComponent(sessao.accessToken)}`;

  const resposta = ws.connect(url, {}, (socket) => {
    let sequencia = 0;

    socket.on('open', () => {
      socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + SEPARADOR); // handshake

      socket.setInterval(() => {
        const texto = `vu${__VU}|${Date.now()}`;
        socket.send(JSON.stringify({
          type: 1, invocationId: String(++sequencia), target: 'EnviarMensagem',
          arguments: [{ solicitacaoId: dados.solicitacaoId, texto }],
        }) + SEPARADOR);
        enviadas.add(1);
      }, 1000);

      socket.setTimeout(() => socket.close(), DURACAO_SESSAO_MS);
    });

    socket.on('message', (bruto) => {
      for (const parte of bruto.split(SEPARADOR)) {
        if (!parte) continue;
        const mensagem = JSON.parse(parte);
        if (mensagem.type === 6) socket.send(JSON.stringify({ type: 6 }) + SEPARADOR); // ping
        if (mensagem.type === 1 && mensagem.target === 'MensagemRecebida') {
          recebidas.add(1);
          const [origem, enviadoEm] = mensagem.arguments[0].texto.split('|');
          if (origem === `vu${__VU}`) latencia.add(Date.now() - Number(enviadoEm));
        }
        if (mensagem.type === 3 && mensagem.error) check(null, { 'hub sem erros': () => false });
      }
    });
  });

  check(resposta, { 'websocket conectado (101)': (r) => r && r.status === 101 });
}
