import { Alert, Button, Center, Paper, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core';
import { type FormEvent, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { entrar } from '../api';
import { useSessao } from '../comum';

export default function Login() {
  const sessao = useSessao();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  if (sessao) return <Navigate to="/" replace />;

  async function aoEnviar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);
    try {
      await entrar(email.trim(), senha);
    } catch (falha) {
      setErro(falha instanceof Error ? falha.message : 'Não foi possível entrar.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Center mih="100vh" p="md">
      <Paper withBorder shadow="sm" p="xl" w="100%" maw={400}>
        <form onSubmit={aoEnviar}>
          <Stack>
            <div>
              <Title order={2}>AutoFlow</Title>
              <Text c="dimmed" size="sm">
                Painel administrativo
              </Text>
            </div>
            {erro && <Alert color="red">{erro}</Alert>}
            <TextInput
              label="E-mail"
              type="email"
              autoComplete="username"
              required
              value={email}
              onChange={(e) => setEmail(e.currentTarget.value)}
            />
            <PasswordInput
              label="Senha"
              autoComplete="current-password"
              required
              value={senha}
              onChange={(e) => setSenha(e.currentTarget.value)}
            />
            <Button type="submit" loading={enviando} fullWidth>
              Entrar
            </Button>
          </Stack>
        </form>
      </Paper>
    </Center>
  );
}
