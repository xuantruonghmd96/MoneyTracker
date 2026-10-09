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
      { id: 'food', name: 'Dining', type: 'Expense', icon: null, color: null, systemKey: null },
      { id: 'salary', name: 'Salary', type: 'Income', icon: null, color: null, systemKey: null },
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

});
