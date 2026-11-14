import { Alert, Badge, Center, Loader } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSyncExternalStore } from 'react';
import { assinarSessao, obterSessao, type StatusUsuario } from './api';

export function useSessao() {
  return useSyncExternalStore(assinarSessao, obterSessao);
}

/** Mutação com aviso de sucesso/erro e recarga das consultas afetadas. */
export function useAcao<V>(executar: (variaveis: V) => Promise<unknown>, sucesso: string, consultas: string[]) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: executar,
    onSuccess: () => {
      notifications.show({ color: 'teal', message: sucesso });
      consultas.forEach((chave) => void cliente.invalidateQueries({ queryKey: [chave] }));
    },
    onError: (erro: Error) => {
      notifications.show({ color: 'red', title: 'Não foi possível concluir', message: erro.message });
    },
  });
}

export function Carregando() {
  return (
    <Center py="xl">
      <Loader />
    </Center>
  );
}

export function Falha({ erro }: { erro: unknown }) {
  return (
    <Alert color="red" title="Não foi possível carregar os dados">
      {erro instanceof Error ? erro.message : 'Erro desconhecido.'}
    </Alert>
  );
}

const STATUS_USUARIO: Record<StatusUsuario, { cor: string; rotulo: string }> = {
  Ativo: { cor: 'teal', rotulo: 'Ativo' },
  PendenteAprovacao: { cor: 'yellow', rotulo: 'Pendente' },
  Bloqueado: { cor: 'red', rotulo: 'Bloqueado' },
};

export function SeloStatusUsuario({ status }: { status: StatusUsuario }) {
  const { cor, rotulo } = STATUS_USUARIO[status];
  return (
    <Badge color={cor} variant="light">
      {rotulo}
    </Badge>
  );
}

export const formatarData = (iso: string) => new Date(iso).toLocaleDateString('pt-BR');

export const formatarMoeda = (valor: number) =>
  valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
