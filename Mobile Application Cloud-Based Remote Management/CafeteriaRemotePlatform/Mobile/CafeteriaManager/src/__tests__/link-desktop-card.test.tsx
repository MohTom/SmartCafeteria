import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { api } from '@/api/client';
import { LinkDesktopCard } from '@/components/LinkDesktopCard';
import { useSelection } from '@/state/SelectionProvider';

jest.mock('@/state/SelectionProvider', () => ({
  useSelection: jest.fn()
}));

jest.mock('@/i18n/I18nProvider', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    language: 'en'
  })
}));

describe('LinkDesktopCard', () => {
  let request: jest.Mock;

  beforeEach(() => {
    jest.clearAllMocks();
    request = jest.spyOn(api, 'request') as jest.Mock;
    (useSelection as jest.Mock).mockReturnValue({
      tenant: { id: 'tenant-1', name: 'Cafeteria', slug: 'cafeteria', role: 'Owner' }
    });
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('generates and displays a claim code', async () => {
    request.mockResolvedValue({
      code: 'ABCD-1234',
      expiresAt: '2026-11-17T10:15:00.000Z'
    });

    await render(<LinkDesktopCard />);
    await fireEvent.press(screen.getByText('generateClaimCode'));

    await waitFor(() => expect(screen.getByText('ABCD-1234')).toBeTruthy());
    expect(request).toHaveBeenCalledWith('/api/tenants/tenant-1/installations/claim-codes', {
      method: 'POST',
      body: '{}'
    });
    expect(screen.getByText(/expires:/)).toBeTruthy();
  });

  it('shows API errors and ignores generation without a tenant', async () => {
    request.mockRejectedValue(new Error('offline'));
    await render(<LinkDesktopCard compact />);
    await fireEvent.press(screen.getByText('generateClaimCode'));
    await waitFor(() => expect(screen.getByText('error')).toBeTruthy());

    (useSelection as jest.Mock).mockReturnValue({ tenant: null });
    await render(<LinkDesktopCard />);
    await fireEvent.press(screen.getByText('generateClaimCode'));
    expect(request).toHaveBeenCalledTimes(1);
  });
});
