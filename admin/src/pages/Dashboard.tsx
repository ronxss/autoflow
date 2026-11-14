import { Group, Paper, Progress, SimpleGrid, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import {
  IconAlertTriangle,
  IconBriefcase,
  IconCash,
  IconHourglass,
  IconUserCheck,
  IconUsers,
} from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { api } from '../api';
import { Carregando, Falha, formatarMoeda } from '../comum';

function Indicador(props: { rotulo: string; valor: string; detalhe?: string; cor: string; icone: ReactNode }) {
  return (
    <Paper withBorder p="md">
      <Group justify="space-between" align="flex-start" wrap="nowrap">
        <div>
          <Text size="sm" c="dimmed">
            {props.rotulo}
          </Text>
          <Text fz={28} fw={700} lh={1.2}>
            {props.valor}
          </Text>
          {props.detalhe && (
            <Text size="xs" c="dimmed">
              {props.detalhe}
            </Text>
          )}
        </div>
        <ThemeIcon color={props.cor} variant="light" size="lg">
          {props.icone}
        </ThemeIcon>
      </Group>
    </Paper>
  );
}

export default function Dashboard() {
  const { data, error, isPending } = useQuery({
    queryKey: ['metricas'],
    queryFn: api.metricas,
    refetchInterval: 30_000,
  });

  if (isPending) return <Carregando />;
  if (error) return <Falha erro={error} />;

  const emAndamento = data.solicitacoesPendentes + data.solicitacoesAceitas;
  const totalSolicitacoes = emAndamento + data.solicitacoesConcluidas + data.solicitacoesCanceladas;
  const faixas = [
    { rotulo: 'Aguardando resposta', valor: data.solicitacoesPendentes, cor: 'yellow' },
    { rotulo: 'Aceitas', valor: data.solicitacoesAceitas, cor: 'indigo' },
    { rotulo: 'Concluídas', valor: data.solicitacoesConcluidas, cor: 'teal' },
    { rotulo: 'Canceladas ou recusadas', valor: data.solicitacoesCanceladas, cor: 'gray' },
  ];

  return (
    <Stack>
      <Title order={2}>Dashboard</Title>

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
        <Indicador
          rotulo="Usuários ativos"
          valor={String(data.usuariosAtivos)}
          detalhe={`${data.totalClientes} clientes · ${data.totalTrabalhadores} trabalhadores`}
          cor="indigo"
          icone={<IconUsers size={20} />}
        />
        <Indicador
          rotulo="Solicitações em andamento"
          valor={String(emAndamento)}
          detalhe={`${data.solicitacoesPendentes} aguardando resposta`}
          cor="yellow"
          icone={<IconHourglass size={20} />}
        />
        <Indicador
          rotulo="Volume de transações"
          valor={formatarMoeda(data.volumeTransacoes)}
          detalhe="Valor dos atendimentos concluídos"
          cor="teal"
          icone={<IconCash size={20} />}
        />
        <Indicador
          rotulo="Atendimentos concluídos"
          valor={String(data.solicitacoesConcluidas)}
          cor="teal"
          icone={<IconUserCheck size={20} />}
        />
        <Indicador
          rotulo="Cadastros aguardando moderação"
          valor={String(data.trabalhadoresPendentes)}
          cor="orange"
          icone={<IconBriefcase size={20} />}
        />
        <Indicador
          rotulo="Denúncias em aberto"
          valor={String(data.denunciasAbertas)}
          cor="red"
          icone={<IconAlertTriangle size={20} />}
        />
      </SimpleGrid>

      <Paper withBorder p="md">
        <Title order={4} mb="sm">
          Solicitações por situação
        </Title>
        <Stack gap="sm">
          {faixas.map((faixa) => (
            <div key={faixa.rotulo}>
              <Group justify="space-between">
                <Text size="sm">{faixa.rotulo}</Text>
                <Text size="sm" fw={600}>
                  {faixa.valor}
                </Text>
              </Group>
              <Progress
                value={totalSolicitacoes === 0 ? 0 : (faixa.valor / totalSolicitacoes) * 100}
                color={faixa.cor}
                aria-label={faixa.rotulo}
              />
            </div>
          ))}
        </Stack>
      </Paper>
    </Stack>
  );
}
