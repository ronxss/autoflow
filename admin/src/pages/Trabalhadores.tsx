import { Badge, Button, Group, SegmentedControl, Stack, Table, Text, Title } from '@mantine/core';
import { IconCheck, IconX } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { api } from '../api';
import { Carregando, Falha, formatarData, useAcao } from '../comum';

type Filtro = 'pendentes' | 'aprovados' | 'todos';

const APROVADO_POR_FILTRO: Record<Filtro, boolean | undefined> = {
  pendentes: false,
  aprovados: true,
  todos: undefined,
};

export default function Trabalhadores() {
  const [filtro, setFiltro] = useState<Filtro>('pendentes');
  const { data, error, isPending } = useQuery({
    queryKey: ['trabalhadores', filtro],
    queryFn: () => api.trabalhadores(APROVADO_POR_FILTRO[filtro]),
  });
  const consultas = ['trabalhadores', 'metricas'];
  const aprovar = useAcao(api.aprovarTrabalhador, 'Cadastro aprovado.', consultas);
  const reprovar = useAcao(api.reprovarTrabalhador, 'Cadastro reprovado.', consultas);

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Moderação de trabalhadores</Title>
        <SegmentedControl
          value={filtro}
          onChange={(valor) => setFiltro(valor as Filtro)}
          data={[
            { value: 'pendentes', label: 'Pendentes' },
            { value: 'aprovados', label: 'Aprovados' },
            { value: 'todos', label: 'Todos' },
          ]}
        />
      </Group>

      {isPending && <Carregando />}
      {error && <Falha erro={error} />}
      {data?.length === 0 && <Text c="dimmed">Nenhum cadastro nesta lista.</Text>}

      {data && data.length > 0 && (
        <Table.ScrollContainer minWidth={760}>
          <Table verticalSpacing="sm" highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Profissional</Table.Th>
                <Table.Th>Área de atuação</Table.Th>
                <Table.Th>Cidade</Table.Th>
                <Table.Th>Cadastro</Table.Th>
                <Table.Th>Situação</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {data.map((t) => (
                <Table.Tr key={t.usuarioId}>
                  <Table.Td maw={320}>
                    <Text fw={600}>{t.nome}</Text>
                    <Text size="xs" c="dimmed">
                      {t.email}
                    </Text>
                    <Text size="xs" lineClamp={2}>
                      {t.descricao}
                    </Text>
                  </Table.Td>
                  <Table.Td>{t.areaAtuacao}</Table.Td>
                  <Table.Td>{t.cidade ?? '—'}</Table.Td>
                  <Table.Td>{formatarData(t.dataCadastro)}</Table.Td>
                  <Table.Td>
                    <Badge color={t.aprovado ? 'teal' : 'yellow'} variant="light">
                      {t.aprovado ? 'Aprovado' : 'Pendente'}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    <Group gap="xs" justify="flex-end" wrap="nowrap">
                      {!t.aprovado && (
                        <Button
                          size="xs"
                          color="teal"
                          leftSection={<IconCheck size={14} />}
                          loading={aprovar.isPending && aprovar.variables === t.usuarioId}
                          onClick={() => aprovar.mutate(t.usuarioId)}
                        >
                          Aprovar
                        </Button>
                      )}
                      {t.aprovado && (
                        <Button
                          size="xs"
                          color="red"
                          variant="light"
                          leftSection={<IconX size={14} />}
                          loading={reprovar.isPending && reprovar.variables === t.usuarioId}
                          onClick={() => reprovar.mutate(t.usuarioId)}
                        >
                          Reprovar
                        </Button>
                      )}
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Stack>
  );
}
