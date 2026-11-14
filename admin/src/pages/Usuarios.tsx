import { Button, Group, Pagination, Stack, Table, Text, TextInput, Title } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { IconSearch } from '@tabler/icons-react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { api, type StatusUsuario, type Usuario } from '../api';
import { Carregando, Falha, SeloStatusUsuario, formatarData, useAcao } from '../comum';

export default function Usuarios() {
  const [busca, setBusca] = useState('');
  const [termo] = useDebouncedValue(busca, 300);
  const [pagina, setPagina] = useState(1);

  const { data, error, isPending } = useQuery({
    queryKey: ['usuarios', termo, pagina],
    queryFn: () => api.usuarios(termo, pagina),
    placeholderData: keepPreviousData,
  });
  const alterarStatus = useAcao(
    (v: { id: string; status: StatusUsuario }) => api.alterarStatusUsuario(v.id, v.status),
    'Status atualizado.',
    ['usuarios', 'metricas'],
  );

  const totalPaginas = data ? Math.max(1, Math.ceil(data.total / data.tamanhoPagina)) : 1;

  function acao(usuario: Usuario) {
    if (usuario.tipo === 'Administrador') return null;
    const bloqueado = usuario.status === 'Bloqueado';
    return (
      <Button
        size="xs"
        variant="light"
        color={bloqueado ? 'teal' : 'red'}
        loading={alterarStatus.isPending && alterarStatus.variables?.id === usuario.id}
        onClick={() => alterarStatus.mutate({ id: usuario.id, status: bloqueado ? 'Ativo' : 'Bloqueado' })}
      >
        {bloqueado ? 'Desbloquear' : 'Bloquear'}
      </Button>
    );
  }

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Usuários</Title>
        <TextInput
          placeholder="Buscar por nome ou e-mail"
          aria-label="Buscar usuários"
          leftSection={<IconSearch size={16} />}
          value={busca}
          onChange={(e) => {
            setBusca(e.currentTarget.value);
            setPagina(1);
          }}
          w={{ base: '100%', sm: 320 }}
        />
      </Group>

      {isPending && <Carregando />}
      {error && <Falha erro={error} />}
      {data?.itens.length === 0 && <Text c="dimmed">Nenhum usuário encontrado.</Text>}

      {data && data.itens.length > 0 && (
        <>
          <Table.ScrollContainer minWidth={680}>
            <Table verticalSpacing="sm" highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Nome</Table.Th>
                  <Table.Th>Tipo</Table.Th>
                  <Table.Th>Cadastro</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {data.itens.map((usuario) => (
                  <Table.Tr key={usuario.id}>
                    <Table.Td>
                      <Text fw={600}>{usuario.nome}</Text>
                      <Text size="xs" c="dimmed">
                        {usuario.email}
                      </Text>
                    </Table.Td>
                    <Table.Td>{usuario.tipo}</Table.Td>
                    <Table.Td>{formatarData(usuario.dataCadastro)}</Table.Td>
                    <Table.Td>
                      <SeloStatusUsuario status={usuario.status} />
                    </Table.Td>
                    <Table.Td ta="right">{acao(usuario)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          <Group justify="space-between">
            <Text size="sm" c="dimmed">
              {data.total} usuário(s)
            </Text>
            <Pagination total={totalPaginas} value={pagina} onChange={setPagina} />
          </Group>
        </>
      )}
    </Stack>
  );
}
