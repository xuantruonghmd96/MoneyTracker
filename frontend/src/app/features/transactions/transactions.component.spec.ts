import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Category, Transaction, Wallet } from '../../core/api.models';
import { TransactionsComponent } from './transactions.component';

describe('TransactionsComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TransactionsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('filters by wallet and calculates period totals from the selected wallet', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();

    const now = new Date();
    const makeDate = (day: number) =>
      new Date(now.getFullYear(), now.getMonth(), day, 12).toISOString();
    const transactions: Transaction[] = [
      {
        id: 'expense-cash',
        amount: 320,
        occurredAt: makeDate(2),
        categoryId: 'food',
        walletId: 'cash',
        participantId: null,
        note: 'Lunch',
      },
      {
        id: 'income-cash',
        amount: 1200,
        occurredAt: makeDate(3),
        categoryId: 'salary',
        walletId: 'cash',
        participantId: null,
        note: 'Paycheck',
      },
      {
        id: 'expense-travel',
        amount: 50,
        occurredAt: makeDate(4),
        categoryId: 'food',
        walletId: 'travel',
        participantId: null,
        note: 'Coffee',
      },
    ];
    const categories: Category[] = [
      { id: 'food', name: 'Dining', type: 'Expense', icon: null, color: null, systemKey: null },
      { id: 'salary', name: 'Salary', type: 'Income', icon: null, color: null, systemKey: null },
    ];
    const wallets: Wallet[] = [
      {
        id: 'cash',
        name: 'Everyday',
        type: 'Regular',
        initialBalance: 1000,
        currency: 'VND',
        icon: null,
        color: null,
      },
      {
        id: 'travel',
        name: 'Travel',
        type: 'Regular',
        initialBalance: 2000,
        currency: 'USD',
        icon: null,
        color: null,
      },
    ];

    const transactionRequest = http.expectOne((request) => request.url === '/api/transactions');
    expect(transactionRequest.request.params.has('from')).toBe(true);
    expect(transactionRequest.request.params.has('to')).toBe(true);
    transactionRequest.flush(transactions);
    http.expectOne('/api/categories').flush(categories);
    http.expectOne('/api/wallets').flush(wallets);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.transaction-row').length).toBe(3);
    expect(fixture.nativeElement.querySelectorAll('.period-strip button').length).toBe(3);
    expect(fixture.nativeElement.querySelector('.summary-amount .income-value').textContent).toContain('1,200');
    expect(fixture.nativeElement.querySelector('.summary-amount .expense-value').textContent).toContain('370');

    fixture.componentInstance.selectWallet('cash');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.transaction-row').length).toBe(2);
    expect(fixture.nativeElement.querySelector('.summary-amount .income-value').textContent).toContain('1,200');
    expect(fixture.nativeElement.querySelector('.summary-amount .expense-value').textContent).toContain('320');
    expect(fixture.nativeElement.querySelector('.balance-row strong').textContent).toContain('1.000');
  });

  it('keeps the selected date when changing period type and moves on horizontal swipes', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();
    const transactionRequest = http.expectOne((request) => request.url === '/api/transactions');
    transactionRequest.flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const component = fixture.componentInstance;
    const selectedDate = component.selectedDate();
    component.selectPeriodType('Week');

    expect(component.selectedDate()).toEqual(selectedDate);
    expect(component.periodType()).toBe('Week');

    const weeklyRequest = http.expectOne((request) => request.url === '/api/transactions');
    weeklyRequest.flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const reload = vi.spyOn(component, 'reload');
    component.onSwipeStart({
      pointerType: 'touch',
      button: 0,
      clientX: 200,
      clientY: 100,
      pointerId: 1,
      target: document.createElement('div'),
    });
    component.onSwipeEnd({
      clientX: 100,
      clientY: 105,
      pointerId: 1,
    });

    expect(component.selectedDate().getTime()).toBeGreaterThan(selectedDate.getTime());
    expect(reload).toHaveBeenCalledOnce();
    reload.mockRestore();
    const swipeRequest = http.expectOne((request) => request.url === '/api/transactions');
    swipeRequest.flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
  });

  it('does not change periods for vertical gestures or gestures beginning on controls', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    const component = fixture.componentInstance;
    const selectedDate = component.selectedDate();
    const reload = vi.spyOn(component, 'reload');

    component.onSwipeStart({
      pointerType: 'touch',
      button: 0,
      clientX: 100,
      clientY: 100,
      pointerId: 1,
      target: document.createElement('div'),
    });
    component.onSwipeEnd({
      clientX: 105,
      clientY: 200,
      pointerId: 1,
    });

    component.onSwipeStart({
      pointerType: 'touch',
      button: 0,
      clientX: 200,
      clientY: 100,
      pointerId: 2,
      target: document.createElement('button'),
    });
    component.onSwipeEnd({
      clientX: 100,
      clientY: 100,
      pointerId: 2,
    });

    expect(component.selectedDate()).toEqual(selectedDate);
    expect(reload).not.toHaveBeenCalled();
  });

});
