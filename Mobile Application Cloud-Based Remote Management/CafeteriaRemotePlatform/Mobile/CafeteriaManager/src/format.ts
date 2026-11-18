import type { TranslationKey } from '@/i18n/I18nProvider';
import type { RemoteCommand } from '@/api/types';

const statusKeys: Record<RemoteCommand['status'], TranslationKey> = {
  Pending: 'pending',
  Dispatched: 'dispatched',
  Executing: 'executing',
  Completed: 'completed',
  Failed: 'failed',
  Expired: 'expired',
  Cancelled: 'cancelled'
};

export function commandStatusKey(status: RemoteCommand['status']): TranslationKey {
  return statusKeys[status] ?? 'status';
}

export function commandStatusTone(
  status: RemoteCommand['status']
): 'neutral' | 'success' | 'warning' | 'danger' | 'primary' {
  switch (status) {
    case 'Completed':
      return 'success';
    case 'Failed':
    case 'Expired':
      return 'danger';
    case 'Pending':
    case 'Dispatched':
      return 'warning';
    case 'Executing':
      return 'primary';
    default:
      return 'neutral';
  }
}

export function roleKey(role: string): TranslationKey {
  switch (role.toLowerCase()) {
    case 'owner':
      return 'owner';
    case 'admin':
    case 'administrator':
      return 'admin';
    case 'manager':
      return 'manager';
    case 'cashier':
      return 'cashier';
    default:
      return 'viewer';
  }
}

export function formatDateTime(
  value: string | null | undefined,
  language: 'ar' | 'en',
  fallback = '—'
): string {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return fallback;
  try {
    return date.toLocaleString(language === 'ar' ? 'ar' : 'en', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  } catch {
    return date.toISOString();
  }
}

export function formatTime(value: string | null | undefined, language: 'ar' | 'en', fallback = '—'): string {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return fallback;
  try {
    return date.toLocaleTimeString(language === 'ar' ? 'ar' : 'en', {
      hour: '2-digit',
      minute: '2-digit'
    });
  } catch {
    return date.toISOString();
  }
}

export function formatMoney(value: number, currency: string | undefined, language: 'ar' | 'en'): string {
  const code = currency?.trim() || 'SAR';
  try {
    return new Intl.NumberFormat(language === 'ar' ? 'ar' : 'en', {
      style: 'currency',
      currency: code,
      maximumFractionDigits: 2
    }).format(value);
  } catch {
    return `${value.toFixed(2)} ${code}`;
  }
}

export function formatNumber(value: number, language: 'ar' | 'en'): string {
  try {
    return new Intl.NumberFormat(language === 'ar' ? 'ar' : 'en').format(value);
  } catch {
    return String(value);
  }
}

export function greetingKey(): TranslationKey {
  return 'dashboardTitle';
}

export function startOfDayIso(): string {
  const date = new Date();
  date.setHours(0, 0, 0, 0);
  return date.toISOString();
}

export function daysAgoIso(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() - days);
  date.setHours(0, 0, 0, 0);
  return date.toISOString();
}

export function nowIso(): string {
  return new Date().toISOString();
}
