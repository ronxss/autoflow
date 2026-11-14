import { AppShell, Burger, Button, Group, NavLink, Text, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconBriefcase, IconFlag, IconLayoutDashboard, IconLogout, IconUsers } from '@tabler/icons-react';
import { Navigate, Outlet, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { sair } from './api';
import { useSessao } from './comum';
import Dashboard from './pages/Dashboard';
import Denuncias from './pages/Denuncias';
import Login from './pages/Login';
import Trabalhadores from './pages/Trabalhadores';
import Usuarios from './pages/Usuarios';

const MENU = [
  { caminho: '/', rotulo: 'Dashboard', Icone: IconLayoutDashboard },
  { caminho: '/trabalhadores', rotulo: 'Trabalhadores', Icone: IconBriefcase },
  { caminho: '/usuarios', rotulo: 'Usuários', Icone: IconUsers },
  { caminho: '/denuncias', rotulo: 'Denúncias e suporte', Icone: IconFlag },
];

function Painel() {
  const sessao = useSessao();
  const [menuAberto, menu] = useDisclosure(false);
  const local = useLocation();
  const navegar = useNavigate();

  if (!sessao) return <Navigate to="/login" replace />;

  return (
    <AppShell
      header={{ height: 56 }}
      navbar={{ width: 250, breakpoint: 'sm', collapsed: { mobile: !menuAberto } }}
      padding="md"
    >
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group gap="sm">
            <Burger opened={menuAberto} onClick={menu.toggle} hiddenFrom="sm" size="sm" aria-label="Abrir menu" />
            <Title order={4}>AutoFlow</Title>
            <Text c="dimmed" size="sm" visibleFrom="sm">
              Painel administrativo
            </Text>
          </Group>
          <Group gap="sm">
            <Text size="sm" visibleFrom="sm">
              {sessao.usuario.nome}
            </Text>
            <Button variant="subtle" size="xs" leftSection={<IconLogout size={16} />} onClick={() => void sair()}>
              Sair
            </Button>
          </Group>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="sm">
        {MENU.map(({ caminho, rotulo, Icone }) => (
          <NavLink
            key={caminho}
            label={rotulo}
            leftSection={<Icone size={18} />}
            active={local.pathname === caminho}
            onClick={() => {
              navegar(caminho);
              menu.close();
            }}
          />
        ))}
      </AppShell.Navbar>

      <AppShell.Main>
        <Outlet />
      </AppShell.Main>
    </AppShell>
  );
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<Painel />}>
        <Route index element={<Dashboard />} />
        <Route path="trabalhadores" element={<Trabalhadores />} />
        <Route path="usuarios" element={<Usuarios />} />
        <Route path="denuncias" element={<Denuncias />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
