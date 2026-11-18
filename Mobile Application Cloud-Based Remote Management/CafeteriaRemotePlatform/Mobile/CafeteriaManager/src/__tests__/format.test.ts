import {
  commandStatusKey,
  commandStatusTone,
  daysAgoIso,
  formatDateTime,
  formatMoney,
  formatNumber,
  formatTime,
  greetingKey,
  nowIso,
  roleKey,
  startOfDayIso
} from '@/format';

describe('format helpers', () => {
  afterEach(() => {
    jest.useRealTimers();
  });

  it('maps command statuses to translation keys', () => {
    expect(commandStatusKey('Pending')).toBe('pending');
    expect(commandStatusKey('Dispatched')).toBe('dispatched');
    expect(commandStatusKey('Executing')).toBe('executing');
    expect(commandStatusKey('Completed')).toBe('completed');
    expect(commandStatusKey('Failed')).toBe('failed');
    expect(commandStatusKey('Expired')).toBe('expired');
    expect(commandStatusKey('Cancelled')).toBe('cancelled');
  });

  it('maps command statuses to tones', () => {
    expect(commandStatusTone('Completed')).toBe('success');
    expect(commandStatusTone('Failed')).toBe('danger');
    expect(commandStatusTone('Expired')).toBe('danger');
    expect(commandStatusTone('Pending')).toBe('warning');
    expect(commandStatusTone('Dispatched')).toBe('warning');
    expect(commandStatusTone('Executing')).toBe('primary');
    expect(commandStatusTone('Cancelled')).toBe('neutral');
  });

  it('normalizes roles', () => {
    expect(roleKey('OWNER')).toBe('owner');
    expect(roleKey('Administrator')).toBe('admin');
    expect(roleKey('admin')).toBe('admin');
    expect(roleKey('Manager')).toBe('manager');
    expect(roleKey('cashier')).toBe('cashier');
    expect(roleKey('unknown')).toBe('viewer');
  });

  it('formats dates and falls back for invalid values', () => {
    expect(formatDateTime(null, 'en')).toBe('—');
    expect(formatDateTime('not-a-date', 'en')).toBe('—');
    expect(formatDateTime('2026-11-17T10:30:00.000Z', 'en')).not.toBe('—');
    expect(formatTime(null, 'ar')).toBe('—');
    expect(formatTime('not-a-date', 'ar')).toBe('—');
    expect(formatTime('2026-11-17T10:30:00.000Z', 'en')).not.toBe('—');
  });

  it('formats money and numbers', () => {
    expect(formatMoney(12.5, 'USD', 'en')).toContain('12.50');
    expect(formatMoney(12.5, undefined, 'en')).toContain('12.50');
    expect(formatNumber(1234.5, 'en')).toBe('1,234.5');
  });

  it('returns stable dashboard helpers', () => {
    expect(greetingKey()).toBe('dashboardTitle');
  });

  it('creates ISO timestamps', () => {
    jest.useFakeTimers().setSystemTime(new Date('2026-11-17T10:00:00.000Z'));
    expect(nowIso()).toBe('2026-11-17T10:00:00.000Z');
    expect(new Date(startOfDayIso()).getTime()).toBeLessThanOrEqual(Date.now());
    expect(new Date(daysAgoIso(7)).getTime()).toBeLessThan(new Date(startOfDayIso()).getTime());
  });
});
