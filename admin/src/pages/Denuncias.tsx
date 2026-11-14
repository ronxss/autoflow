import {
  Badge,
  Button,
  Group,
  Modal,
  SegmentedControl,
  Select,
  Stack,
  Table,
  Text,
  Textarea,
  Title,
} from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { api, type Denuncia, type StatusDenuncia } from '../api';
import { Carregando, Falha, formatarData, useAcao } from '../comum';

const STATUS: Record<StatusDenuncia, { cor: string; rotulo: string }> = {
  Aberta: { cor: 'red', rotulo: 'Aberta' },
  EmAnalise: { cor: 'yellow', rotulo: 'Em análise' },
  Resolvida: { cor: 'teal', rotulo: 'Resolvida' },
};

const OPCOES_STATUS = (Object.keys(STATUS) as StatusDenuncia[]).map((valor) => ({
  value: valor,
  label: STATUS[valor].rotulo,
}));

function Analise({ denuncia, aoFechar }: { denuncia: Denuncia; aoFechar: () => void }) {
  const [status, setStatus] = useState<StatusDenuncia>(denuncia.status);
  const [resolucao, setResolucao] = useState(denuncia.resolucao ?? '');
  const salvar = useAcao(
    () => api.atualizarDenuncia(denuncia.id, status, resolucao.trim()),
    'Denúncia atualizada.',
    ['denuncias', 'metricas'],
  );

  return (
    <Stack>
      <div>
        <Text size="sm" c="dimmed">
          Denunciante
        </Text>
        <Text>{denuncia.denuncianteNome}</Text>
      </div>
      <div>
        <Text size="sm" c="dimmed">
          Denunciado
        </Text>
        <Text>{denuncia.denunciadoNome}</Text>
      </div>
      <div>
        <Text size="sm" c="dimmed">
          Motivo
        </Text>
        <Text>{denuncia.motivo}</Text>
        {denuncia.descricao && <Text size="sm">{denuncia.descricao}</Text>}
      </div>
      <Select
        label="Situação"
        data={OPCOES_STATUS}
        value={status}
        allowDeselect={false}
        onChange={(valor) => valor && setStatus(valor as StatusDenuncia)}
      />
      <Textarea
        label="Resolução / anotações do suporte"
        autosize
        minRows={3}
        value={resolucao}
        onChange={(e) => setResolucao(e.currentTarget.value)}
      />
      <Group justify="flex-end">
        <Button variant="default" onClick={aoFechar}>
          Cancelar
        </Button>
        <Button loading={salvar.isPending} onClick={() => salvar.mutate(undefined, { onSuccess: aoFechar })}>
          Salvar
        </Button>
      </Group>
    </Stack>
  );
}

export default function Denuncias() {
  const [filtro, setFiltro] = useState<StatusDenuncia | 'Todas'>('Aberta');
  const [selecionada, setSelecionada] = useState<Denuncia | null>(null);
  const { data, error, isPending } = useQuery({
    queryKey: ['denuncias', filtro],
    queryFn: () => api.denuncias(filtro === 'Todas' ? undefined : filtro),
  });

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Denúncias e suporte</Title>
        <SegmentedControl
          value={filtro}
          onChange={(valor) => setFiltro(valor as StatusDenuncia | 'Todas')}
          data={[...OPCOES_STATUS, { value: 'Todas', label: 'Todas' }]}
        />
      </Group>

      {isPending && <Carregando />}
      {error && <Falha erro={error} />}
      {data?.length === 0 && <Text c="dimmed">Nenhuma denúncia nesta lista.</Text>}

      {data && data.length > 0 && (
        <Table.ScrollContainer minWidth={720}>
          <Table verticalSpacing="sm" highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Data</Table.Th>
                <Table.Th>Denunciado</Table.Th>
                <Table.Th>Motivo</Table.Th>
                <Table.Th>Denunciante</Table.Th>
                <Table.Th>Situação</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data.map((denuncia) => (
                <Table.Tr key={denuncia.id}>
                  <Table.Td>{formatarData(denuncia.criadoEm)}</Table.Td>
                  <Table.Td>{denuncia.denunciadoNome}</Table.Td>
                  <Table.Td maw={280}>
                    <Text size="sm" lineClamp={2}>
                      {denuncia.motivo}
                    </Text>
                  </Table.Td>
                  <Table.Td>{denuncia.denuncianteNome}</Table.Td>
                  <Table.Td>
                    <Badge color={STATUS[denuncia.status].cor} variant="light">
                      {STATUS[denuncia.status].rotulo}
                    </Badge>
                  </Table.Td>
                  <Table.Td ta="right">
                    <Button size="xs" variant="light" onClick={() => setSelecionada(denuncia)}>
                      Analisar
                    </Button>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}

      <Modal opened={selecionada !== null} onClose={() => setSelecionada(null)} title="Analisar denúncia" centered>
        {selecionada && (
          <Analise key={selecionada.id} denuncia={selecionada} aoFechar={() => setSelecionada(null)} />
        )}
      </Modal>
    </Stack>
  );
}
