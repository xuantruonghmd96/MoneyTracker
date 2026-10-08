export interface User {
  id: string;
  email: string;
  displayName: string;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: User;
}

export interface ApiError {
  error?: string;
  fields?: Record<string, string>;
}

export type CategoryType = 'Income' | 'Expense' | 'Debt';

export interface Category {
  id: string;
  name: string;
  type: CategoryType;
  icon: string | null;
  color: string | null;
  systemKey: string | null;
}

export interface Wallet {
  id: string;
  name: string;
  type: 'Regular' | 'Credit';
  initialBalance: number;
  currency: string;
  icon: string | null;
  color: string | null;
}

export interface Transaction {
  id: string;
  amount: number;
  occurredAt: string;
  categoryId: string;
  walletId: string;
  participantId: string | null;
  note: string | null;
}

export interface MonthlyReport {
  year: number;
  month: number;
  totalIncome: number;
  totalExpense: number;
  totalDebtOut: number;
  totalDebtIn: number;
  net: number;
  byCategory: Array<{
    categoryId: string;
    name: string;
    isSystem: boolean;
    systemKey: string | null;
    type: CategoryType;
    parentId: string | null;
    amount: number;
    transactionCount: number;
  }>;
  topExpenses: Array<{
    id: string;
    amount: number;
    occurredAt: string;
    note: string | null;
    categoryId: string;
    categoryName: string;
  }>;
}
