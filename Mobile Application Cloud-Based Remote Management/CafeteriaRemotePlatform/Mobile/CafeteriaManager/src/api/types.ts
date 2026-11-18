export type AuthTokens = {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
};

export type TenantSummary = {
  id: string;
  name: string;
  slug: string;
  role: string;
};

export type UserProfile = {
  id: string;
  email: string;
  displayName: string;
  tenants: TenantSummary[];
};

export type AuthResponse = {
  tokens: AuthTokens;
  user: UserProfile;
};

export type InstallationAccess = {
  canViewReports: boolean;
  canManageUsers: boolean;
  canAdjustInventory: boolean;
};

export type Installation = {
  id: string;
  tenantId: string;
  name: string;
  machineName: string;
  appVersion: string;
  isOnline: boolean;
  lastSeenAt: string | null;
  createdAt: string;
  access: InstallationAccess;
};

export type RemoteCommand = {
  id: string;
  tenantId: string;
  installationId: string;
  requestedByUserId: string;
  type: string;
  payloadJson: string;
  status: 'Pending' | 'Dispatched' | 'Executing' | 'Completed' | 'Failed' | 'Expired' | 'Cancelled';
  resultJson: string | null;
  error: string | null;
  createdAt: string;
  expiresAt: string;
  dispatchedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
};

export type NotificationItem = {
  id: string;
  tenantId: string;
  installationId: string | null;
  title: string;
  body: string;
  type: string;
  isRead: boolean;
  createdAt: string;
};

export type UnreadCount = {
  count: number;
};

export type InvitationCreated = {
  id: string;
  email: string;
  role: string;
  inviteCode: string;
  expiresAt: string;
};

export type ClaimCode = {
  code: string;
  expiresAt: string;
};

export type TenantMember = {
  userId: string;
  email: string;
  displayName: string;
  role: string;
  isActive: boolean;
  createdAt: string;
};

export type DashboardSnapshot = {
  totalRevenue: number;
  completedOrders: number;
  lowStockCount: number;
  totalProducts: number;
  currency: string;
  generatedAt: string;
};

export type AnalyticsSummary = {
  revenue: number;
  orders: number;
  averageBasket: number;
  unitsSold: number;
  itemsPerOrder: number;
  expenses: number;
  otherIncome: number;
  netProfit: number;
  marginPercent: number;
  cancelledOrders: number;
  cancelledValue: number;
  activeProducts: number;
  deadStockCount: number;
  topProductName: string;
  topCategoryName: string;
  busiestHourLabel: string;
};

export type AnalyticsKpi = {
  label: string;
  value: string;
};

export type AnalyticsRow = {
  rank: string;
  primary: string;
  secondary: string;
  tertiary: string;
  value: string;
  barRatio: number;
};

export type AnalyticsReport = {
  kind: number;
  title: string;
  columns: string[];
  rows: AnalyticsRow[];
  emptyMessage: string;
};

export type AnalyticsBundle = {
  start: string | null;
  end: string | null;
  periodLabel: string;
  summary: AnalyticsSummary;
  kpis: AnalyticsKpi[];
  reports: AnalyticsReport[];
};

export type DesktopUser = {
  id: string;
  username: string;
  displayName: string;
  role: string;
  isActive: boolean;
  createdAt: string;
};

export type DesktopProduct = {
  id: string;
  nameAr: string;
  nameEn: string;
  category: string;
  inventory: number;
  maxInventory: number;
  lowStock: boolean;
};
