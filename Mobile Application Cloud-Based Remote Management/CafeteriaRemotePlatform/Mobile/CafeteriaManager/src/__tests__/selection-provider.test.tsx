import type { ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react-native';
import { api } from '@/api/client';
import type { Installation, UserProfile } from '@/api/types';
import { useAuth } from '@/auth/AuthProvider';
import { SelectionProvider, useSelection } from '@/state/SelectionProvider';
import {
  getSelectedInstallationId,
  getSelectedTenantId,
  saveSelectedInstallationId,
  saveSelectedTenantId
} from '@/storage';

jest.mock('@/auth/AuthProvider', () => ({
  useAuth: jest.fn()
}));

jest.mock('@/storage', () => ({
  getSelectedInstallationId: jest.fn(),
  getSelectedTenantId: jest.fn(),
  saveSelectedInstallationId: jest.fn(),
  saveSelectedTenantId: jest.fn()
}));

const profile: UserProfile = {
  id: 'user-1',
  email: 'owner@example.test',
  displayName: 'Owner',
  tenants: [
    { id: 'tenant-1', name: 'Cafeteria One', slug: 'one', role: 'Owner' },
    { id: 'tenant-2', name: 'Cafeteria Two', slug: 'two', role: 'Admin' }
  ]
};

const installation: Installation = {
  id: 'installation-1',
  tenantId: 'tenant-1',
  name: 'Front desk',
  machineName: 'CAFE-PC',
  appVersion: '1.0.0',
  isOnline: true,
  lastSeenAt: '2026-11-17T10:00:00.000Z',
  createdAt: '2026-11-01T10:00:00.000Z',
  access: { canViewReports: true, canManageUsers: true, canAdjustInventory: true }
};

function wrapper({ children }: { children: ReactNode }) {
  return <SelectionProvider>{children}</SelectionProvider>;
}

async function renderSelection() {
  return renderHook(() => useSelection(), { wrapper });
}

describe('SelectionProvider', () => {
  let request: jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    request = jest.spyOn(api, 'request') as jest.Mock;
    (useAuth as jest.Mock).mockReturnValue({ status: 'signedOut', profile: null });
    (getSelectedTenantId as jest.Mock).mockResolvedValue(null);
    (getSelectedInstallationId as jest.Mock).mockResolvedValue(null);
    (saveSelectedTenantId as jest.Mock).mockResolvedValue(undefined);
    (saveSelectedInstallationId as jest.Mock).mockResolvedValue(undefined);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('is ready without a session', async () => {
    const { result } = await renderSelection();
    await waitFor(() => expect(result.current.ready).toBe(true));
    expect(result.current.tenant).toBeNull();
    expect(request).not.toHaveBeenCalled();
  });

  it('auto-selects a single tenant and loads installations', async () => {
    (useAuth as jest.Mock).mockReturnValue({
      status: 'signedIn',
      profile: { ...profile, tenants: [profile.tenants[0]] }
    });
    request.mockResolvedValue([installation]);

    const { result } = await renderSelection();

    await waitFor(() => expect(result.current.tenant?.id).toBe('tenant-1'));
    await waitFor(() => expect(result.current.installations).toEqual([installation]));
    expect(saveSelectedTenantId).toHaveBeenCalledWith('tenant-1');
    expect(result.current.loadingInstallations).toBe(false);
  });

  it('restores a valid stored installation', async () => {
    (useAuth as jest.Mock).mockReturnValue({ status: 'signedIn', profile });
    (getSelectedTenantId as jest.Mock).mockResolvedValue('tenant-1');
    (getSelectedInstallationId as jest.Mock).mockResolvedValue('installation-1');
    request.mockResolvedValue([installation]);

    const { result } = await renderSelection();

    await waitFor(() => expect(result.current.installation?.id).toBe('installation-1'));
  });

  it('supports tenant and installation selection', async () => {
    (useAuth as jest.Mock).mockReturnValue({ status: 'signedIn', profile });
    request.mockResolvedValue([installation]);
    const { result } = await renderSelection();
    await waitFor(() => expect(result.current.ready).toBe(true));

    await act(async () => {
      await result.current.selectTenant('tenant-2');
    });
    expect(result.current.tenant?.id).toBe('tenant-2');
    expect(saveSelectedTenantId).toHaveBeenCalledWith('tenant-2');
    expect(saveSelectedInstallationId).toHaveBeenCalledWith(null);

    await act(async () => {
      await result.current.selectInstallation('installation-1');
    });
    expect(saveSelectedInstallationId).toHaveBeenCalledWith('installation-1');
  });

  it('updates an installation in place', async () => {
    (useAuth as jest.Mock).mockReturnValue({
      status: 'signedIn',
      profile: { ...profile, tenants: [profile.tenants[0]] }
    });
    request.mockResolvedValue([installation]);
    const { result } = await renderSelection();
    await waitFor(() => expect(result.current.installations).toHaveLength(1));

    await act(() => {
      result.current.updateInstallation({ ...installation, isOnline: false, name: 'Back office' });
    });

    expect(result.current.installations[0]).toMatchObject({
      id: 'installation-1',
      isOnline: false,
      name: 'Back office'
    });
  });

  it('reports installation loading failures', async () => {
    (useAuth as jest.Mock).mockReturnValue({
      status: 'signedIn',
      profile: { ...profile, tenants: [profile.tenants[0]] }
    });
    request.mockRejectedValue(new Error('offline'));

    const { result } = await renderSelection();
    await waitFor(() => expect(result.current.installationsError).toBe('offline'));
  });

  it('clears selections', async () => {
    (useAuth as jest.Mock).mockReturnValue({ status: 'signedIn', profile });
    request.mockResolvedValue([installation]);
    const { result } = await renderSelection();
    await waitFor(() => expect(result.current.ready).toBe(true));

    await act(async () => {
      await result.current.clearSelection();
    });

    expect(result.current.tenant).toBeNull();
    expect(result.current.installation).toBeNull();
    expect(result.current.installations).toEqual([]);
  });

  it('requires a provider', async () => {
    const spy = jest.spyOn(console, 'error').mockImplementation(() => undefined);
    await expect(renderHook(() => useSelection())).rejects.toThrow(
      'useSelection must be used inside SelectionProvider.'
    );
    spy.mockRestore();
  });
});
