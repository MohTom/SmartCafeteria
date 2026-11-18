import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode
} from 'react';
import { api } from '@/api/client';
import type { Installation, TenantSummary } from '@/api/types';
import { useAuth } from '@/auth/AuthProvider';
import {
  getSelectedInstallationId,
  getSelectedTenantId,
  saveSelectedInstallationId,
  saveSelectedTenantId
} from '@/storage';

type SelectionValue = {
  ready: boolean;
  tenant: TenantSummary | null;
  installation: Installation | null;
  installations: Installation[];
  loadingInstallations: boolean;
  installationsError: string | null;
  selectTenant: (tenantId: string) => Promise<void>;
  selectInstallation: (installationId: string) => Promise<void>;
  refreshInstallations: () => Promise<void>;
  updateInstallation: (installation: Installation) => void;
  clearSelection: () => Promise<void>;
};

const SelectionContext = createContext<SelectionValue | null>(null);

export function SelectionProvider({ children }: { children: ReactNode }) {
  const { status, profile } = useAuth();
  const [ready, setReady] = useState(false);
  const [tenantId, setTenantId] = useState<string | null>(null);
  const [installationId, setInstallationIdState] = useState<string | null>(null);
  const [installations, setInstallations] = useState<Installation[]>([]);
  const [loadingInstallations, setLoadingInstallations] = useState(false);
  const [installationsError, setInstallationsError] = useState<string | null>(null);

  const tenant = useMemo(
    () => profile?.tenants.find((item) => item.id === tenantId) ?? null,
    [profile, tenantId]
  );

  const installation = useMemo(
    () => installations.find((item) => item.id === installationId) ?? null,
    [installations, installationId]
  );

  useEffect(() => {
    if (status !== 'signedIn' || !profile) {
      setTenantId(null);
      setInstallationIdState(null);
      setInstallations([]);
      setReady(true);
      return;
    }

    setReady(false);
    let active = true;
    (async () => {
      const [storedTenantId, storedInstallationId] = await Promise.all([
        getSelectedTenantId(),
        getSelectedInstallationId()
      ]);
      if (!active) return;

      const validTenant =
        storedTenantId && profile.tenants.some((item) => item.id === storedTenantId)
          ? storedTenantId
          : profile.tenants.length === 1
            ? profile.tenants[0].id
            : null;

      setTenantId(validTenant);
      setInstallationIdState(storedInstallationId);
      if (validTenant !== storedTenantId) {
        await saveSelectedTenantId(validTenant);
      }
      if (active) setReady(true);
    })();

    return () => {
      active = false;
    };
  }, [status, profile]);

  const refreshInstallations = useCallback(async () => {
    if (!tenantId) {
      setInstallations([]);
      return;
    }

    setLoadingInstallations(true);
    setInstallationsError(null);
    try {
      const rows = await api.request<Installation[]>(
        `/api/tenants/${tenantId}/installations`
      );
      setInstallations(rows);
      setInstallationIdState((current) => {
        const next = current && rows.some((item) => item.id === current) ? current : null;
        if (next !== current) void saveSelectedInstallationId(next);
        return next;
      });
    } catch (error) {
      setInstallationsError(error instanceof Error ? error.message : 'Could not load installations.');
    } finally {
      setLoadingInstallations(false);
    }
  }, [tenantId]);

  useEffect(() => {
    if (status === 'signedIn' && tenantId) {
      void refreshInstallations();
    }
  }, [status, tenantId, refreshInstallations]);

  const selectTenant = useCallback(async (nextTenantId: string) => {
    setTenantId(nextTenantId);
    setInstallationIdState(null);
    setInstallations([]);
    await Promise.all([
      saveSelectedTenantId(nextTenantId),
      saveSelectedInstallationId(null)
    ]);
  }, []);

  const selectInstallation = useCallback(async (nextInstallationId: string) => {
    setInstallationIdState(nextInstallationId);
    await saveSelectedInstallationId(nextInstallationId);
  }, []);

  const updateInstallation = useCallback((updated: Installation) => {
    setInstallations((current) =>
      current.map((item) => (item.id === updated.id ? { ...item, ...updated } : item))
    );
  }, []);

  const clearSelection = useCallback(async () => {
    setTenantId(null);
    setInstallationIdState(null);
    setInstallations([]);
    await Promise.all([saveSelectedTenantId(null), saveSelectedInstallationId(null)]);
  }, []);

  const value = useMemo<SelectionValue>(
    () => ({
      ready,
      tenant,
      installation,
      installations,
      loadingInstallations,
      installationsError,
      selectTenant,
      selectInstallation,
      refreshInstallations,
      updateInstallation,
      clearSelection
    }),
    [
      ready,
      tenant,
      installation,
      installations,
      loadingInstallations,
      installationsError,
      selectTenant,
      selectInstallation,
      refreshInstallations,
      updateInstallation,
      clearSelection
    ]
  );

  return <SelectionContext.Provider value={value}>{children}</SelectionContext.Provider>;
}

export function useSelection(): SelectionValue {
  const value = useContext(SelectionContext);
  if (!value) throw new Error('useSelection must be used inside SelectionProvider.');
  return value;
}
