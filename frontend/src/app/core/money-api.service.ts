import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Category, MonthlyReport, Transaction, Wallet } from './api.models';

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

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  getWallets(): Observable<Wallet[]> {
    return this.http.get<Wallet[]>('/api/wallets');
  }
}
