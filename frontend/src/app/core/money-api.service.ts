import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Category,
  CreateCategoryRequest,
  CreateTransactionRequest,
  CreateWalletRequest,
  MonthlyReport,
  Participant,
  Transaction,
  UpdateCategoryRequest,
  UpdateTransactionRequest,
  UpdateWalletRequest,
  Wallet,
} from './api.models';

@Injectable({ providedIn: 'root' })
export class MoneyApiService {
  constructor(private readonly http: HttpClient) {}

  getMonthlyReport(year: number, month: number): Observable<MonthlyReport> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<MonthlyReport>('/api/reports/monthly', { params });
  }

  getTransactions(from: Date, to: Date): Observable<Transaction[]> {
    const params = new HttpParams().set('from', from.toISOString()).set('to', to.toISOString());
    return this.http.get<Transaction[]>('/api/transactions', { params });
  }

  getTransaction(id: string): Observable<Transaction> {
    return this.http.get<Transaction>(`/api/transactions/${id}`);
  }

  createTransaction(request: CreateTransactionRequest): Observable<Transaction> {
    return this.http.post<Transaction>('/api/transactions', request);
  }

  updateTransaction(id: string, request: UpdateTransactionRequest): Observable<Transaction> {
    return this.http.put<Transaction>(`/api/transactions/${id}`, request);
  }

  deleteTransaction(id: string): Observable<void> {
    return this.http.delete<void>(`/api/transactions/${id}`);
  }

  getParticipants(): Observable<Participant[]> {
    return this.http.get<Participant[]>('/api/participants');
  }

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  getAssignedWallets(categoryId: string): Observable<string[]> {
    return this.http.get<string[]>(`/api/categories/${categoryId}/wallets`);
  }

  setAssignedWallets(categoryId: string, walletIds: string[]): Observable<void> {
    return this.http.put<void>(`/api/categories/${categoryId}/wallets`, walletIds);
  }

  createCategory(request: CreateCategoryRequest): Observable<Category> {
    return this.http.post<Category>('/api/categories', request);
  }

  updateCategory(id: string, request: UpdateCategoryRequest): Observable<Category> {
    return this.http.put<Category>(`/api/categories/${id}`, request);
  }

  deleteCategory(id: string): Observable<void> {
    return this.http.delete<void>(`/api/categories/${id}`);
  }

  getWallets(): Observable<Wallet[]> {
    return this.http.get<Wallet[]>('/api/wallets');
  }

  createWallet(request: CreateWalletRequest): Observable<Wallet> {
    return this.http.post<Wallet>('/api/wallets', request);
  }

  updateWallet(id: string, request: UpdateWalletRequest): Observable<Wallet> {
    return this.http.put<Wallet>(`/api/wallets/${id}`, request);
  }

  deleteWallet(id: string): Observable<void> {
    return this.http.delete<void>(`/api/wallets/${id}`);
  }
}
