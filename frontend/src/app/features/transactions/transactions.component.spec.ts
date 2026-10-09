import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, RouterOutlet } from '@angular/router';
import { Category, Transaction, Wallet } from '../../core/api.models';
import { LanguageService } from '../../core/language.service';
import { TransactionsComponent } from './transactions.component';
import { TransactionsViewState } from './transactions-view-state.service';
import { getPeriodBounds } from './period';

@Component({ standalone: true, template: '' })
class ReportRouteStub {}

@Component({ standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
class RouterHostStub {}

describe('TransactionsComponent', () => {
  let http: HttpTestingController;
  let clientWidthDescriptor: PropertyDescriptor | undefined;
  let scrollWidthDescriptor: PropertyDescriptor | undefined;
  let scrollToDescriptor: PropertyDescriptor | undefined;

  beforeEach(() => {
    localStorage.removeItem('money-tracker.language');
    clientWidthDescriptor = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'clientWidth');
    scrollWidthDescriptor = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'scrollWidth');
    scrollToDescriptor = Object.getOwnPropertyDescriptor(HTMLElement.prototype, 'scrollTo');
    TestBed.configureTestingModule({
      imports: [TransactionsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: '', component: TransactionsComponent },
          { path: 'categories', component: ReportRouteStub },
          { path: 'report', component: ReportRouteStub },
        ]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const viewState = TestBed.inject(TransactionsViewState);
    viewState.selectedDate.set(new Date());
    viewState.futureSelected.set(false);
    viewState.periodType.set('Month');
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    if (clientWidthDescriptor) {
      Object.defineProperty(HTMLElement.prototype, 'clientWidth', clientWidthDescriptor);
    } else {
      Reflect.deleteProperty(HTMLElement.prototype, 'clientWidth');
    }
    if (scrollWidthDescriptor) {
      Object.defineProperty(HTMLElement.prototype, 'scrollWidth', scrollWidthDescriptor);
    } else {
      Reflect.deleteProperty(HTMLElement.prototype, 'scrollWidth');
    }
    if (scrollToDescriptor) {
      Object.defineProperty(HTMLElement.prototype, 'scrollTo', scrollToDescriptor);
    } else {
      Reflect.deleteProperty(HTMLElement.prototype, 'scrollTo');
    }
  });

  it('navigates to the report when any part of the period summary is clicked', async () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
    fixture.detectChanges();

    const summary = fixture.nativeElement.querySelector('.period-summary') as HTMLAnchorElement;
    expect(summary.getAttribute('href')).toBe('/report');
    expect(summary.querySelector('a')).toBeNull();

    summary.click();
    await fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/report');
  });

  it('preserves the selected period and scrolls it into view when returning from another route', async () => {
    const fixture = TestBed.createComponent(RouterHostStub);
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/');
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const initialComponent = fixture.debugElement.query(
      (element) => element.componentInstance instanceof TransactionsComponent,
    ).componentInstance as TransactionsComponent;
    const historicalPeriod = initialComponent.periods()[25];
    initialComponent.selectPeriod(historicalPeriod.date);
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
    fixture.detectChanges();

    await router.navigateByUrl('/categories');

    const getBoundingClientRect = vi
      .spyOn(HTMLElement.prototype, 'getBoundingClientRect')
      .mockImplementation(function (this: HTMLElement): DOMRect {
        if (this.classList.contains('period-strip')) {
          return { left: 0, right: 300, width: 300 } as DOMRect;
        }
        if (this.getAttribute('aria-pressed') === 'true') {
          const strip = this.closest('.period-strip') as HTMLElement;
          const left = 3200 - strip.scrollLeft;
          return { left, right: left + 100, width: 100 } as DOMRect;
        }
        return { left: 0, right: 0, width: 0 } as DOMRect;
      });
    Object.defineProperty(HTMLElement.prototype, 'clientWidth', {
      configurable: true,
      get() {
        return this.classList.contains('period-strip') ? 300 : 0;
      },
    });
    Object.defineProperty(HTMLElement.prototype, 'scrollWidth', {
      configurable: true,
      get() {
        return this.classList.contains('period-strip') ? 4000 : 0;
      },
    });
    vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
      callback(0);
      return 1;
    });

    await router.navigateByUrl('/');
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
    fixture.detectChanges();

    const strip = fixture.nativeElement.querySelector('.period-strip') as HTMLElement;
    expect(fixture.nativeElement.querySelector('.period-strip button.selected span').textContent)
      .toBe(historicalPeriod.label);
    expect(strip.scrollLeft).toBe(3100);
    expect(getBoundingClientRect).toHaveBeenCalled();
  });

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
      {
        id: 'food',
        name: 'Dining',
        type: 'Expense',
        parentId: null,
        appliesToAllWallets: true,
        icon: null,
        color: null,
        isSystem: false,
        systemKey: null,
      },
      {
        id: 'salary',
        name: 'Salary',
        type: 'Income',
        parentId: null,
        appliesToAllWallets: true,
        icon: null,
        color: null,
        isSystem: false,
        systemKey: null,
      },
    ];
    const wallets: Wallet[] = [
      {
        id: 'cash',
        name: 'Everyday',
        type: 'Regular',
        creditLimit: null,
        initialBalance: 1000,
        currency: 'VND',
        icon: null,
        color: null,
      },
      {
        id: 'travel',
        name: 'Travel',
        type: 'Regular',
        creditLimit: null,
        initialBalance: 2000,
        currency: 'USD',
        icon: null,
        color: null,
      },
    ];

    const transactionRequest = http.expectOne((request) => request.url === '/api/transactions');
    const initialBounds = getPeriodBounds(new Date(), 'Month');
    expect(transactionRequest.request.params.get('from')).toBe(initialBounds.start.toISOString());
    expect(transactionRequest.request.params.get('to')).toBe(
      new Date(initialBounds.end.getTime() - 1).toISOString(),
    );
    transactionRequest.flush(transactions);
    http.expectOne('/api/categories').flush(categories);
    http.expectOne('/api/wallets').flush(wallets);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.transaction-row').length).toBe(3);
    expect(fixture.nativeElement.querySelectorAll('.period-strip button').length).toBe(33);
    expect(fixture.nativeElement.querySelector('.period-strip button.selected span').textContent).toBe(
      'This month',
    );
    expect(fixture.nativeElement.querySelector('.period-strip button:last-child span').textContent).toBe(
      'Future',
    );
    expect(fixture.nativeElement.querySelector('.summary-amount .income-value').textContent).toContain('1,200');
    expect(fixture.nativeElement.querySelector('.summary-amount .expense-value').textContent).toContain('370');
    expect(
      [...fixture.nativeElement.querySelectorAll('.transaction-row')].some((row) =>
        row.textContent.includes('$50.00'),
      ),
    ).toBe(true);

    fixture.componentInstance.selectWallet('cash');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.transaction-row').length).toBe(2);
    expect(fixture.nativeElement.querySelector('.summary-amount .income-value').textContent).toContain('1,200');
    expect(fixture.nativeElement.querySelector('.summary-amount .expense-value').textContent).toContain('320');
    expect(fixture.nativeElement.querySelector('.balance-row strong').textContent).toContain('1,000');

    TestBed.inject(LanguageService).setLanguage('vi');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.period-strip button.selected span').textContent).toBe(
      'tháng này',
    );
    expect(fixture.nativeElement.querySelector('.wallet-button').textContent).toContain('Everyday');
    expect(fixture.nativeElement.querySelector('.balance-label').textContent).toContain('Số dư đầu kỳ');
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

    expect(component.selectedDate()).toEqual(selectedDate);
    expect(component.futureSelected()).toBe(true);
    expect(reload).toHaveBeenCalledOnce();
    reload.mockRestore();
    const swipeRequest = http.expectOne((request) => request.url === '/api/transactions');
    swipeRequest.flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
  });

  it('keeps 31 periods before a historical selection and requests its exact bounds', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const component = fixture.componentInstance;
    const previous = component.periods()[30];
    const previousBounds = getPeriodBounds(previous.date, component.periodType());
    component.selectPeriod(previous.date);

    const previousRequest = http.expectOne((request) => request.url === '/api/transactions');
    expect(previousRequest.request.params.get('from')).toBe(previousBounds.start.toISOString());
    expect(previousRequest.request.params.get('to')).toBe(
      new Date(previousBounds.end.getTime() - 1).toISOString(),
    );
    previousRequest.flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    expect(component.periods()).toHaveLength(34);
    expect(component.periods().slice(0, 31).every((period) => !period.selected)).toBe(true);
    expect(component.periods().at(-1)?.label).toBe('Future');
  });

  it('loads older period batches at the left edge without shifting the visible content', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const component = fixture.componentInstance;
    const strip = fixture.nativeElement.querySelector('.period-strip') as HTMLElement;
    Object.defineProperty(strip, 'scrollWidth', {
      configurable: true,
      get: () => component.periods().length * 100,
    });

    const scheduledFrames: FrameRequestCallback[] = [];
    vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
      scheduledFrames.push(callback);
      return scheduledFrames.length;
    });

    try {
      strip.scrollLeft = 0;
      component.onPeriodStripScroll();
      component.onPeriodStripScroll();

      expect(component.periods()).toHaveLength(64);
      expect(scheduledFrames).toHaveLength(1);

      scheduledFrames.shift()?.(0);
      expect(strip.scrollLeft).toBe(3100);

      scheduledFrames.shift()?.(0);
      strip.scrollLeft = 0;
      component.onPeriodStripScroll();
      expect(component.periods()).toHaveLength(95);
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it('requests Future from the current period end through the end of the supported date range', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const component = fixture.componentInstance;
    component.selectFuture();

    const currentBounds = getPeriodBounds(new Date(), 'Month');
    const futureRequest = http.expectOne((request) => request.url === '/api/transactions');
    expect(futureRequest.request.params.get('from')).toBe(currentBounds.end.toISOString());
    expect(futureRequest.request.params.get('to')).toBe(
      new Date(Date.UTC(9999, 11, 31, 23, 59, 59, 999)).toISOString(),
    );
    futureRequest.flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    fixture.detectChanges();
    expect(component.periodTitle()).toBe('Future');
    expect(fixture.nativeElement.querySelector('.period-strip button.selected span').textContent).toBe(
      'Future',
    );
  });

  it('hides the jump button only while the current period is fully visible', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const component = fixture.componentInstance;
    const strip = fixture.nativeElement.querySelector('.period-strip') as HTMLElement;
    const currentPeriod = strip.querySelector('[data-current-period="true"]') as HTMLElement;
    vi.spyOn(strip, 'getBoundingClientRect').mockReturnValue({
      left: 0,
      right: 300,
      width: 300,
    } as DOMRect);
    const currentBounds = vi.spyOn(currentPeriod, 'getBoundingClientRect');

    currentBounds.mockReturnValue({ left: 25, right: 115, width: 90 } as DOMRect);
    component.updateCurrentPeriodVisibility();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.jump-current-period')).toBeNull();

    currentBounds.mockReturnValue({ left: 270, right: 360, width: 90 } as DOMRect);
    component.updateCurrentPeriodVisibility();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.jump-current-period')).not.toBeNull();
  });

  it('jumps from Future to the current period and requests its date range', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const component = fixture.componentInstance;
    component.selectFuture();
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    fixture.detectChanges();
    const jumpButton = fixture.nativeElement.querySelector(
      '.jump-current-period',
    ) as HTMLButtonElement;
    jumpButton.click();

    const currentBounds = getPeriodBounds(new Date(), component.periodType());
    const currentRequest = http.expectOne((request) => request.url === '/api/transactions');
    expect(currentRequest.request.params.get('from')).toBe(currentBounds.start.toISOString());
    expect(currentRequest.request.params.get('to')).toBe(
      new Date(currentBounds.end.getTime() - 1).toISOString(),
    );
    expect(component.futureSelected()).toBe(false);
    expect(component.selectedDate()).toEqual(currentBounds.start);
    currentRequest.flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
  });

  it('keeps toolbar controls mutually exclusive and closes them on outside clicks', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const component = fixture.componentInstance;
    component.toggleWalletMenu();
    expect(component.walletMenuOpen()).toBe(true);

    component.toggleSearch();
    expect(component.walletMenuOpen()).toBe(false);
    expect(component.searchOpen()).toBe(true);

    component.toggleMenu();
    expect(component.searchOpen()).toBe(false);
    expect(component.menuOpen()).toBe(true);

    component.openPeriodTypes();
    expect(component.menuOpen()).toBe(false);
    expect(component.periodTypesOpen()).toBe(true);

    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(component.walletMenuOpen()).toBe(false);
    expect(component.searchOpen()).toBe(false);
    expect(component.menuOpen()).toBe(false);
    expect(component.periodTypesOpen()).toBe(false);

    component.searchQuery.set('rent');
    component.toggleSearch();
    fixture.detectChanges();
    fixture.nativeElement
      .querySelector('.search-field')
      .dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(component.searchOpen()).toBe(true);

    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(component.searchOpen()).toBe(false);
    expect(component.searchQuery()).toBe('rent');
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

  it('creates a transaction and reloads the selected period', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([
      { id: 'category-1', name: 'Food', type: 'Expense', parentId: null, appliesToAllWallets: true, icon: null, color: null, isSystem: false, systemKey: null },
    ]);
    http.expectOne('/api/wallets').flush([
      { id: 'wallet-1', name: 'Cash', type: 'Regular', creditLimit: null, initialBalance: 0, currency: 'VND', icon: null, color: null },
    ]);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.openCreate();
    http.expectOne('/api/participants').flush([
      { id: 'participant-1', name: 'Someone', note: null, isDefault: true },
    ]);
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
    expect(component.formMode()).toBeNull();
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
  });

  it('initializes a new transaction with the current local date and time', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    const component = fixture.componentInstance;
    const beforeOpen = new Date();
    component.openCreate();
    const afterOpen = new Date();
    http.expectOne('/api/participants').flush([]);

    const initialized = new Date(component.form.controls.occurredAt.value);
    expect(initialized.getTime()).toBeGreaterThanOrEqual(
      new Date(beforeOpen.getFullYear(), beforeOpen.getMonth(), beforeOpen.getDate()).getTime(),
    );
    expect(initialized.getTime()).toBeLessThanOrEqual(afterOpen.getTime());
    expect(initialized.getHours()).toBe(afterOpen.getHours());
    expect(initialized.getMinutes()).toBe(afterOpen.getMinutes());
  });

  it('opens the create form when requested from the navigation bar', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();

    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    TestBed.inject(TransactionsViewState).requestCreateTransaction();
    fixture.detectChanges();

    expect(fixture.componentInstance.formMode()).toBe('create');
    expect(fixture.nativeElement.querySelector('.transaction-dialog-backdrop')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.transaction-form').getAttribute('role')).toBe('dialog');
    expect(fixture.nativeElement.querySelector('.transaction-form')).not.toBeNull();
    http.expectOne('/api/participants').flush([]);

    fixture.destroy();
    const reopenedFixture = TestBed.createComponent(TransactionsComponent);
    reopenedFixture.detectChanges();
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
    reopenedFixture.detectChanges();

    expect(reopenedFixture.componentInstance.formMode()).toBeNull();
    expect(reopenedFixture.nativeElement.querySelector('.transaction-dialog-backdrop')).toBeNull();
  });

  it('filters create categories to the selected wallet assignments', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([
      { id: 'cash-only', name: 'Cash only', type: 'Expense', parentId: null, appliesToAllWallets: false, icon: null, color: null, isSystem: false, systemKey: null },
      { id: 'travel-only', name: 'Travel only', type: 'Expense', parentId: null, appliesToAllWallets: false, icon: null, color: null, isSystem: false, systemKey: null },
      { id: 'everywhere', name: 'Everywhere', type: 'Expense', parentId: null, appliesToAllWallets: true, icon: null, color: null, isSystem: false, systemKey: null },
    ]);
    http.expectOne('/api/wallets').flush([
      { id: 'cash', name: 'Cash', type: 'Regular', creditLimit: null, initialBalance: 0, currency: 'VND', icon: null, color: null },
      { id: 'travel', name: 'Travel', type: 'Regular', creditLimit: null, initialBalance: 0, currency: 'VND', icon: null, color: null },
    ]);

    const component = fixture.componentInstance;
    component.openCreate();
    http.expectOne('/api/participants').flush([]);
    http.expectOne('/api/categories/cash-only/wallets').flush(['cash']);
    http.expectOne('/api/categories/travel-only/wallets').flush(['travel']);
    fixture.detectChanges();

    expect(component.selectableCategories().map(({ id }) => id)).toEqual(['cash-only', 'everywhere']);
    expect(component.form.controls.categoryId.value).toBe('cash-only');

    component.form.controls.walletId.setValue('travel');
    fixture.detectChanges();
    expect(component.selectableCategories().map(({ id }) => id)).toEqual(['travel-only', 'everywhere']);
    expect(component.form.controls.categoryId.value).toBe('travel-only');
    const categoryOptions = [...fixture.nativeElement.querySelectorAll('.transaction-field select[formControlName="categoryId"] option')]
      .map((option: HTMLOptionElement) => option.value);
    expect(categoryOptions).toEqual(['', 'travel-only', 'everywhere']);
  });

  it('shifts the transaction date by one day while preserving its time', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    const component = fixture.componentInstance;
    component.form.controls.occurredAt.setValue('2026-10-09T14:35');

    component.shiftOccurredAtDay(-1);
    expect(component.form.controls.occurredAt.value).toBe('2026-10-08T14:35');

    component.shiftOccurredAtDay(1);
    component.shiftOccurredAtDay(1);
    expect(component.form.controls.occurredAt.value).toBe('2026-10-10T14:35');
  });

  it('closes the transaction form for controls outside the form, keeping form clicks inside', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);

    const component = fixture.componentInstance;
    component.openCreate();
    http.expectOne('/api/participants').flush([]);
    fixture.detectChanges();

    fixture.nativeElement
      .querySelector('.transaction-form input')
      .dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(component.formMode()).toBe('create');

    const otherControl = document.createElement('button');
    document.body.append(otherControl);
    otherControl.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    otherControl.remove();
    expect(component.formMode()).toBeNull();

    component.openCreate();
    http.expectOne('/api/participants').flush([]);
    document.body.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(component.formMode()).toBeNull();
  });

  it('updates a transaction without a confirmation step and reloads the selected period', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();
    const transaction: Transaction = {
      id: 'transaction-1',
      amount: 125,
      occurredAt: new Date().toISOString(),
      categoryId: 'category-1',
      walletId: 'wallet-1',
      participantId: null,
      note: 'Old note',
    };
    http.expectOne((request) => request.url === '/api/transactions').flush([transaction]);
    http.expectOne('/api/categories').flush([
      { id: 'category-1', name: 'Food', type: 'Expense', parentId: null, appliesToAllWallets: true, icon: null, color: null, isSystem: false, systemKey: null },
    ]);
    http.expectOne('/api/wallets').flush([
      { id: 'wallet-1', name: 'Cash', type: 'Regular', creditLimit: null, initialBalance: 0, currency: 'VND', icon: null, color: null },
    ]);

    const component = fixture.componentInstance;
    component.openEdit(transaction);
    http.expectOne('/api/participants').flush([]);
    expect(fixture.nativeElement.querySelector('.transaction-delete-confirmation')).toBeNull();
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
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
  });

  it('requires explicit confirmation before deleting a transaction', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();
    const transaction: Transaction = {
      id: 'transaction-1',
      amount: 125,
      occurredAt: new Date().toISOString(),
      categoryId: 'category-1',
      walletId: 'wallet-1',
      participantId: null,
      note: 'Lunch',
    };
    http.expectOne((request) => request.url === '/api/transactions').flush([transaction]);
    http.expectOne('/api/categories').flush([
      { id: 'category-1', name: 'Food', type: 'Expense', parentId: null, appliesToAllWallets: true, icon: null, color: null, isSystem: false, systemKey: null },
    ]);
    http.expectOne('/api/wallets').flush([
      { id: 'wallet-1', name: 'Cash', type: 'Regular', creditLimit: null, initialBalance: 0, currency: 'VND', icon: null, color: null },
    ]);
    fixture.detectChanges();

    const row = fixture.nativeElement.querySelector('.transaction-row') as HTMLElement;
    (row.querySelector('.transaction-actions button:last-child') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.transaction-delete-confirmation')).not.toBeNull();
    expect(http.match({ method: 'DELETE', url: '/api/transactions/transaction-1' })).toHaveLength(0);

    (
      fixture.nativeElement.querySelector(
        '.transaction-delete-confirmation button:last-child',
      ) as HTMLButtonElement
    ).click();
    const deleteRequest = http.expectOne({ method: 'DELETE', url: '/api/transactions/transaction-1' });
    deleteRequest.flush(null);
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
  });

  it('keeps the form open and displays an error when saving fails', () => {
    const fixture = TestBed.createComponent(TransactionsComponent);
    fixture.detectChanges();
    http.expectOne((request) => request.url === '/api/transactions').flush([]);
    http.expectOne('/api/categories').flush([
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
    http.expectOne('/api/wallets').flush([
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

    const component = fixture.componentInstance;
    component.openCreate();
    http.expectOne('/api/participants').flush([]);
    component.form.setValue({
      amount: 125,
      occurredAt: new Date().toISOString().slice(0, 16),
      walletId: 'wallet-1',
      categoryId: 'category-1',
      participantId: null,
      note: 'Lunch',
    });
    component.saveTransaction();

    http.expectOne({ method: 'POST', url: '/api/transactions' }).flush(
      { error: 'VALIDATION_FAILED' },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(component.formMode()).toBe('create');
    expect(component.saveError()).toBe('transactions.errorSave');
  });

});
