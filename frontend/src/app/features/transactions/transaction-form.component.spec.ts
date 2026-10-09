import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, RouterOutlet } from '@angular/router';
import { Transaction } from '../../core/api.models';
import { TransactionFormComponent } from './transaction-form.component';

@Component({ standalone: true, template: '' })
class EmptyRouteComponent {}

@Component({ standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
class RouterHostComponent {}

describe('TransactionFormComponent', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.removeItem('money-tracker.language');
    TestBed.configureTestingModule({
      imports: [RouterHostComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: '', component: EmptyRouteComponent },
          { path: 'transactions/new', component: TransactionFormComponent },
          { path: 'transactions/:id/edit', component: TransactionFormComponent },
        ]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  it('creates a transaction from its own screen and returns to the list', async () => {
    const fixture = TestBed.createComponent(RouterHostComponent);
    await router.navigateByUrl('/transactions/new');
    fixture.detectChanges();

    http
      .expectOne('/api/categories')
      .flush([
        {
          id: 'category-1',
          name: 'Food',
          type: 'Expense',
          parentId: null,
          appliesToAllWallets: true,
          icon: null,
          color: null,
          isSystem: false,
          systemKey: null,
        },
      ]);
    http
      .expectOne('/api/wallets')
      .flush([
        {
          id: 'wallet-1',
          name: 'Cash',
          type: 'Regular',
          creditLimit: null,
          initialBalance: 0,
          currency: 'VND',
          icon: null,
          color: null,
        },
      ]);
    http
      .expectOne('/api/participants')
      .flush([{ id: 'participant-1', name: 'Someone', note: null, isDefault: true }]);
    fixture.detectChanges();

    const component = fixture.debugElement.query(
      (element) => element.componentInstance instanceof TransactionFormComponent,
    ).componentInstance as TransactionFormComponent;
    expect(fixture.nativeElement.querySelector('.transaction-form-page')).not.toBeNull();
    component.form.setValue({
      amount: 125,
      occurredAt: '2026-10-09T10:30',
      walletId: 'wallet-1',
      categoryId: 'category-1',
      participantId: null,
      note: 'Lunch',
    });
    component.saveTransaction();

    const createRequest = http.expectOne({ method: 'POST', url: '/api/transactions' });
    expect(createRequest.request.body).toMatchObject({
      amount: 125,
      occurredAt: new Date('2026-10-09T10:30').toISOString(),
      walletId: 'wallet-1',
      categoryId: 'category-1',
      participantId: null,
      note: 'Lunch',
    });
    expect(createRequest.request.body.id).toEqual(expect.any(String));
    createRequest.flush({
      id: 'transaction-1',
      amount: 125,
      occurredAt: '2026-10-09T10:30:00.000Z',
      categoryId: 'category-1',
      walletId: 'wallet-1',
      participantId: null,
      note: 'Lunch',
    });

    await fixture.whenStable();
    expect(router.url).toBe('/');
  });

  it('loads and updates an existing transaction on its edit screen', async () => {
    const fixture = TestBed.createComponent(RouterHostComponent);
    await router.navigateByUrl('/transactions/transaction-1/edit');
    fixture.detectChanges();

    const transaction: Transaction = {
      id: 'transaction-1',
      amount: 125,
      occurredAt: '2026-10-09T10:30:00.000Z',
      categoryId: 'category-1',
      walletId: 'wallet-1',
      participantId: null,
      note: 'Old note',
    };
    http
      .expectOne('/api/categories')
      .flush([
        {
          id: 'category-1',
          name: 'Food',
          type: 'Expense',
          parentId: null,
          appliesToAllWallets: true,
          icon: null,
          color: null,
          isSystem: false,
          systemKey: null,
        },
      ]);
    http
      .expectOne('/api/wallets')
      .flush([
        {
          id: 'wallet-1',
          name: 'Cash',
          type: 'Regular',
          creditLimit: null,
          initialBalance: 0,
          currency: 'VND',
          icon: null,
          color: null,
        },
      ]);
    http.expectOne('/api/transactions/transaction-1').flush(transaction);
    http.expectOne('/api/participants').flush([]);
    fixture.detectChanges();

    const component = fixture.debugElement.query(
      (element) => element.componentInstance instanceof TransactionFormComponent,
    ).componentInstance as TransactionFormComponent;
    expect(component.mode()).toBe('edit');
    component.form.setValue({
      amount: 200,
      occurredAt: '2026-10-09T11:00',
      walletId: 'wallet-1',
      categoryId: 'category-1',
      participantId: null,
      note: 'Updated note',
    });
    component.saveTransaction();

    const updateRequest = http.expectOne({ method: 'PUT', url: '/api/transactions/transaction-1' });
    expect(updateRequest.request.body).toMatchObject({
      amount: 200,
      walletId: 'wallet-1',
      categoryId: 'category-1',
      note: 'Updated note',
    });
    expect(updateRequest.request.body.id).toBeUndefined();
    updateRequest.flush({ ...transaction, amount: 200, note: 'Updated note' });
    await fixture.whenStable();

    expect(router.url).toBe('/');
  });
});
