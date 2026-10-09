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
  parentId: string | null;
  appliesToAllWallets: boolean;
  icon: string | null;
  color: string | null;
  isSystem: boolean;
  systemKey: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateCategoryRequest {
  id?: string;
  name: string;
  type: CategoryType;
  parentId: string | null;
  appliesToAllWallets: boolean;
  icon: string | null;
  color: string | null;
  assignToWalletIds?: string[] | null;
}

export interface UpdateCategoryRequest {
  name: string;
  parentId: string | null;
  appliesToAllWallets: boolean;
  icon: string | null;
  color: string | null;
}

export interface Wallet {
  id: string;
  name: string;
  type: 'Regular' | 'Credit';
  creditLimit: number | null;
  initialBalance: number;
  currency: string;
  icon: string | null;
  color: string | null;
}

export interface CreateWalletRequest {
  id?: string;
  name: string;
  type: Wallet['type'];
  creditLimit: number | null;
  initialBalance: number;
  currency: string;
  icon: string | null;
  color: string | null;
}

export interface UpdateWalletRequest {
  name: string;
  creditLimit: number | null;
  initialBalance: number;
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

export interface Participant {
  id: string;
  name: string;
  note: string | null;
  isDefault: boolean;
}

export interface CreateTransactionRequest {
  id?: string;
  amount: number;
  occurredAt: string;
  walletId: string;
  categoryId: string;
  participantId: string | null;
  note: string | null;
}

export type UpdateTransactionRequest = Omit<CreateTransactionRequest, 'id'>;

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
