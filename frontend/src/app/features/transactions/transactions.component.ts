import { HttpErrorResponse } from '@angular/common/http';
import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of, Subject, switchMap, tap } from 'rxjs';
import { Category, CategoryType, Transaction, Wallet } from '../../core/api.models';
import { LanguageService, TranslationKey } from '../../core/language.service';
import { MoneyApiService } from '../../core/money-api.service';
import {
  PeriodDirection,
  PeriodType,
  PeriodTranslations,
  formatPeriodLabel,
  formatNavigationPeriodLabel,
  getPeriodBounds,
  movePeriod,
} from './period';

interface TransactionDay {
  key: string;
  dayNumber: string;
  weekday: string;
  dateLabel: string;
  total: number;
  transactions: Transaction[];
}

interface PeriodOption {
  date: Date;
  key: string;
  label: string;
  accessibleLabel: string;
  selected: boolean;
  isFuture: boolean;
}

const PERIOD_TYPES: PeriodType[] = ['Day', 'Week', 'Month', 'Quarter', 'Year'];
const PERIOD_TYPE_TRANSLATION_KEYS: Record<PeriodType, TranslationKey> = {
  Day: 'period.typeDay',
  Week: 'period.typeWeek',
  Month: 'period.typeMonth',
  Quarter: 'period.typeQuarter',
  Year: 'period.typeYear',
};
const PERIODS_BEFORE_SELECTED = 31;
const PERIOD_LOAD_THRESHOLD = 100;
const SWIPE_THRESHOLD = 55;
const FUTURE_RANGE_END = new Date(Date.UTC(9999, 11, 31, 23, 59, 59, 999));

@Component({
  selector: 'app-transactions',
  imports: [RouterLink],
  templateUrl: './transactions.component.html',
  styleUrl: './transactions.scss',
})
export class TransactionsComponent implements OnInit, AfterViewInit {
  private readonly api = inject(MoneyApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly language = inject(LanguageService);
  private readonly periodRequests = new Subject<PeriodBoundsRequest>();
  private swipeStart: { x: number; y: number; pointerId: number } | null = null;
  private readonly periodsBeforeSelected = signal(PERIODS_BEFORE_SELECTED);
  private loadingEarlierPeriods = false;
  @ViewChild('periodStrip') private periodStrip?: ElementRef<HTMLElement>;
  readonly currentPeriodVisible = signal(false);
  readonly periodTypes = PERIOD_TYPES;
  readonly selectedDate = signal(new Date());
  readonly futureSelected = signal(false);
  readonly periodType = signal<PeriodType>('Month');
  readonly selectedWalletId = signal<string | null>(null);
  readonly walletMenuOpen = signal(false);
  readonly menuOpen = signal(false);
  readonly periodTypesOpen = signal(false);
  readonly searchOpen = signal(false);
  readonly searchQuery = signal('');
  readonly transactions = signal<Transaction[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly wallets = signal<Wallet[]>([]);
  readonly loading = signal(true);
  private readonly errorKey = signal<TranslationKey | null>(null);
  readonly errorMessage = computed(() => {
    const key = this.errorKey();
    return key ? this.language.t(key) : '';
  });

  readonly periodTitle = computed(() =>
    this.futureSelected()
      ? this.language.t('transactions.future')
      : formatNavigationPeriodLabel(
          this.selectedDate(),
          this.periodType(),
          new Date(),
          false,
          this.language.locale(),
          this.periodTranslations(),
        ),
  );
  readonly periods = computed<PeriodOption[]>(() => {
    const selected = this.selectedDate();
    const type = this.periodType();
    const referenceDate = new Date();
    const currentStart = getPeriodBounds(referenceDate, type).start;
    const selectedStart = getPeriodBounds(selected, type).start;
    const dates: Date[] = [];
    const earlierDates: Date[] = [];
    let previous = selectedStart;

    for (let count = 0; count < this.periodsBeforeSelected(); count++) {
      previous = movePeriod(previous, type, -1);
      earlierDates.push(previous);
    }
    dates.push(...earlierDates.reverse());
    dates.push(selectedStart);

    let next = movePeriod(selectedStart, type, 1);
    while (next.getTime() <= currentStart.getTime()) {
      dates.push(next);
      next = movePeriod(next, type, 1);
    }

    const options = dates.map((date) => {
      const bounds = getPeriodBounds(date, type);
      const label = formatNavigationPeriodLabel(
        date,
        type,
        referenceDate,
        true,
        this.language.locale(),
        this.periodTranslations(),
      );
      return {
        date,
        key: bounds.start.toISOString(),
        label,
        accessibleLabel: `${label}, ${formatPeriodLabel(
          date,
          type,
          false,
          this.language.locale(),
          this.periodTranslations(),
        )}`,
        selected: !this.futureSelected() && bounds.start.getTime() === selectedStart.getTime(),
        isFuture: false,
      };
    });

    const currentBounds = getPeriodBounds(referenceDate, type);
    options.push({
      date: currentBounds.end,
      key: 'future',
      label: this.language.t('transactions.future'),
      accessibleLabel: this.language.t('transactions.futureAccessible'),
      selected: this.futureSelected(),
      isFuture: true,
    });
    return options;
  });
  readonly selectedWalletName = computed(() => {
    const walletId = this.selectedWalletId();
    if (!walletId) return this.language.t('transactions.allWallets');
    return (
      this.wallets().find((wallet) => wallet.id === walletId)?.name ??
      this.language.t('transactions.allWallets')
    );
  });
  readonly openingBalance = computed(() => {
    const walletId = this.selectedWalletId();
    if (walletId) {
      const wallet = this.wallets().find((item) => item.id === walletId);
      return wallet ? this.formatWalletMoney(wallet.initialBalance, wallet.currency) : this.formatMoney(0);
    }

    const totals = new Map<string, number>();
    for (const wallet of this.wallets()) {
      totals.set(wallet.currency, (totals.get(wallet.currency) ?? 0) + wallet.initialBalance);
    }
    if (totals.size === 0) return this.formatWalletMoney(0, 'VND');
    return [...totals.entries()].map(([currency, amount]) => this.formatWalletMoney(amount, currency)).join(' · ');
  });
  readonly filteredTransactions = computed(() => {
    const walletId = this.selectedWalletId();
    const query = this.searchQuery().trim().toLocaleLowerCase();
    return this.transactions().filter((transaction) => {
      if (walletId && transaction.walletId !== walletId) return false;
      if (!query) return true;
      return [
        transaction.note ?? '',
        this.categoryName(transaction.categoryId),
        this.walletName(transaction.walletId),
        transaction.amount.toString(),
      ]
        .join(' ')
        .toLocaleLowerCase()
        .includes(query);
    });
  });
  readonly periodTotals = computed(() => {
    let income = 0;
    let expense = 0;
    for (const transaction of this.transactionsForSelectedWallet()) {
      const signed = this.transactionAmount(transaction);
      if (signed > 0) income += signed;
      else expense += Math.abs(signed);
    }
    return { income, expense, net: income - expense };
  });
  readonly transactionDays = computed(() => this.groupTransactions());

  ngOnInit(): void {
    this.periodRequests
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.errorKey.set(null);
          this.transactions.set([]);
          this.categories.set([]);
          this.wallets.set([]);
        }),
        switchMap(({ start, end, inclusiveEnd }) =>
          forkJoin({
            transactions: this.api.getTransactions(
              start,
              inclusiveEnd ? end : new Date(end.getTime() - 1),
            ),
            categories: this.api.getCategories(),
            wallets: this.api.getWallets(),
          }).pipe(
            catchError((error: unknown) => {
              this.errorKey.set(this.getLoadError(error));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((data) => {
        if (data) {
          const { start, end, inclusiveEnd } = this.getSelectedRange();
          const startTime = start.getTime();
          const endTime = end.getTime();
          this.transactions.set(
            data.transactions.filter((transaction) => {
              const occurredAt = new Date(transaction.occurredAt).getTime();
              return occurredAt >= startTime && (inclusiveEnd ? occurredAt <= endTime : occurredAt < endTime);
            }),
          );
          this.categories.set(data.categories);
          this.wallets.set(data.wallets);
        }
        this.loading.set(false);
      });

    this.reload();
  }

  ngAfterViewInit(): void {
    this.scrollSelectedPeriodIntoView(false);
    this.updateCurrentPeriodVisibility();
  }

  selectWallet(walletId: string | null): void {
    this.selectedWalletId.set(walletId);
    this.walletMenuOpen.set(false);
  }

  toggleWalletMenu(): void {
    const shouldOpen = !this.walletMenuOpen();
    this.closeToolbarControls();
    this.walletMenuOpen.set(shouldOpen);
  }

  toggleSearch(): void {
    const shouldOpen = !this.searchOpen();
    this.closeToolbarControls();
    this.searchOpen.set(shouldOpen);
  }

  selectPeriod(date: Date, isFuture = false): void {
    if (isFuture) {
      this.selectFuture();
      return;
    }

    const bounds = getPeriodBounds(date, this.periodType());
    const currentBounds = getPeriodBounds(new Date(), this.periodType());
    if (bounds.start.getTime() > currentBounds.start.getTime()) return;
    const selectedBounds = getPeriodBounds(this.selectedDate(), this.periodType());
    if (bounds.start.getTime() === selectedBounds.start.getTime() && !this.futureSelected()) return;
    this.futureSelected.set(false);
    this.selectedDate.set(date);
    this.reload();
    this.scrollSelectedPeriodIntoView();
  }

  selectFuture(): void {
    if (this.futureSelected()) return;
    this.futureSelected.set(true);
    this.reload();
    this.scrollSelectedPeriodIntoView();
  }

  goToCurrentPeriod(): void {
    const now = new Date();
    const currentBounds = getPeriodBounds(now, this.periodType());
    const selectedBounds = getPeriodBounds(this.selectedDate(), this.periodType());
    const selectionChanged =
      this.futureSelected() || selectedBounds.start.getTime() !== currentBounds.start.getTime();

    this.futureSelected.set(false);
    this.selectedDate.set(currentBounds.start);
    if (selectionChanged) this.reload();
    this.scrollSelectedPeriodIntoView();
  }

  updateCurrentPeriodVisibility(): void {
    const strip = this.periodStrip?.nativeElement;
    const current = strip?.querySelector<HTMLElement>('[data-current-period="true"]');
    if (!strip || !current) {
      this.currentPeriodVisible.set(false);
      return;
    }

    const stripBounds = strip.getBoundingClientRect();
    const currentBounds = current.getBoundingClientRect();
    const visible =
      currentBounds.width > 0 &&
      currentBounds.left >= stripBounds.left &&
      currentBounds.right <= stripBounds.right;
    this.currentPeriodVisible.set(visible);
  }

  onPeriodStripScroll(): void {
    this.updateCurrentPeriodVisibility();

    const strip = this.periodStrip?.nativeElement;
    if (
      !strip ||
      this.loadingEarlierPeriods ||
      strip.scrollLeft > PERIOD_LOAD_THRESHOLD
    ) {
      return;
    }

    this.loadingEarlierPeriods = true;
    const previousScrollWidth = strip.scrollWidth;
    this.periodsBeforeSelected.update((count) => count + PERIODS_BEFORE_SELECTED);

    if (typeof requestAnimationFrame !== 'function') {
      this.loadingEarlierPeriods = false;
      return;
    }

    requestAnimationFrame(() => {
      strip.scrollLeft += strip.scrollWidth - previousScrollWidth;
      this.updateCurrentPeriodVisibility();
      requestAnimationFrame(() => {
        this.loadingEarlierPeriods = false;
      });
    });
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.updateCurrentPeriodVisibility();
  }

  selectPeriodType(type: PeriodType): void {
    this.periodType.set(type);
    this.periodsBeforeSelected.set(PERIODS_BEFORE_SELECTED);
    this.loadingEarlierPeriods = false;
    this.periodTypesOpen.set(false);
    this.reload();
    this.scrollSelectedPeriodIntoView();
  }

  toggleMenu(): void {
    const shouldOpen = !this.menuOpen() && !this.periodTypesOpen();
    this.closeToolbarControls();
    this.menuOpen.set(shouldOpen);
  }

  openPeriodTypes(): void {
    this.menuOpen.set(false);
    this.periodTypesOpen.set(true);
  }

  @HostListener('document:click', ['$event'])
  closeToolbarControlsOnOutsideClick(event: MouseEvent): void {
    const target = event.target;
    if (
      target instanceof Element &&
      target.closest('.toolbar-row, .search-field')
    ) {
      return;
    }
    this.closeToolbarControls();
  }

  isCurrentPeriod(date: Date): boolean {
    const now = new Date();
    const bounds = getPeriodBounds(date, this.periodType());
    return now >= bounds.start && now < bounds.end;
  }

  setSearchQuery(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLInputElement) this.searchQuery.set(target.value);
  }

  onSwipeStart(
    event: Pick<PointerEvent, 'pointerType' | 'button' | 'clientX' | 'clientY' | 'pointerId' | 'target'>,
  ): void {
    if (event.pointerType === 'mouse' && event.button !== 0) return;
    const target = event.target;
    if (
      target instanceof Element &&
      target.closest('button, a, input, select, textarea, [role="menu"], [role="listbox"]')
    ) {
      this.swipeStart = null;
      return;
    }
    this.swipeStart = { x: event.clientX, y: event.clientY, pointerId: event.pointerId };
  }

  onSwipeEnd(event: Pick<PointerEvent, 'clientX' | 'clientY' | 'pointerId'>): void {
    const start = this.swipeStart;
    this.swipeStart = null;
    if (!start || start.pointerId !== event.pointerId) return;

    const deltaX = event.clientX - start.x;
    const deltaY = event.clientY - start.y;
    if (Math.abs(deltaX) < SWIPE_THRESHOLD || Math.abs(deltaX) <= Math.abs(deltaY) * 1.25) return;
    this.moveSelectedPeriod(deltaX < 0 ? 1 : -1);
  }

  resetSwipe(): void {
    this.swipeStart = null;
  }

  reload(): void {
    this.periodRequests.next(this.getSelectedRange());
  }

  categoryName(id: string): string {
    return (
      this.categories().find((category) => category.id === id)?.name ??
      this.language.t('transactions.categoryFallback')
    );
  }

  categoryColor(id: string): string {
    return this.categories().find((category) => category.id === id)?.color ?? '';
  }

  categoryIcon(id: string): string {
    const category = this.categories().find((item) => item.id === id);
    const name = `${category?.name ?? ''} ${category?.icon ?? ''}`.toLocaleLowerCase();
    if (/food|eat|meal|restaurant|coffee|drink|ăn|cafe/.test(name)) return '◒';
    if (/transport|car|fuel|gas|travel|xe|ride/.test(name)) return '↗';
    if (/home|house|rent|electric|water|nhà|điện/.test(name)) return '⌂';
    if (/shop|cloth|beauty|mua|shopping/.test(name)) return '✳';
    return category?.type === 'Income' ? '↙' : '✦';
  }

  transactionType(id: string): CategoryType {
    return this.categories().find((category) => category.id === id)?.type ?? 'Expense';
  }

  transactionAmount(transaction: Transaction): number {
    const category = this.categories().find((item) => item.id === transaction.categoryId);
    if (category?.type === 'Income') return transaction.amount;
    if (
      category?.type === 'Debt' &&
      ['DEBT_BORROW', 'DEBT_COLLECT'].includes(category.systemKey ?? '')
    ) {
      return transaction.amount;
    }
    return -transaction.amount;
  }

  walletName(id: string): string {
    return this.wallets().find((wallet) => wallet.id === id)?.name ?? 'Personal';
  }

  formatMoney(amount: number): string {
    return this.formatWalletMoney(amount, 'VND');
  }

  formatWalletMoney(amount: number, currency: string): string {
    return new Intl.NumberFormat(this.language.locale(), {
      style: 'currency',
      currency,
      maximumFractionDigits: currency === 'VND' ? 0 : 2,
    }).format(amount);
  }

  walletCurrency(walletId: string): string {
    return this.wallets().find((wallet) => wallet.id === walletId)?.currency ?? 'VND';
  }

  periodTypeLabel(type: PeriodType): string {
    return this.language.t(PERIOD_TYPE_TRANSLATION_KEYS[type]);
  }

  private moveSelectedPeriod(direction: PeriodDirection): void {
    if (this.futureSelected()) {
      if (direction < 0) this.selectFuturePeriodPredecessor();
      return;
    }

    const currentBounds = getPeriodBounds(new Date(), this.periodType());
    const selectedBounds = getPeriodBounds(this.selectedDate(), this.periodType());
    if (direction > 0 && selectedBounds.start.getTime() === currentBounds.start.getTime()) {
      this.selectFuture();
      return;
    }
    this.selectPeriod(movePeriod(this.selectedDate(), this.periodType(), direction));
  }

  private selectFuturePeriodPredecessor(): void {
    this.futureSelected.set(false);
    this.selectedDate.set(getPeriodBounds(new Date(), this.periodType()).start);
    this.reload();
    this.scrollSelectedPeriodIntoView();
  }

  private closeToolbarControls(): void {
    this.walletMenuOpen.set(false);
    this.searchOpen.set(false);
    this.menuOpen.set(false);
    this.periodTypesOpen.set(false);
  }

  private getSelectedRange(): PeriodBoundsRequest {
    if (this.futureSelected()) {
      return {
        start: getPeriodBounds(new Date(), this.periodType()).end,
        end: FUTURE_RANGE_END,
        inclusiveEnd: true,
      };
    }

    const { start, end } = getPeriodBounds(this.selectedDate(), this.periodType());
    return { start, end, inclusiveEnd: false };
  }

  private scrollSelectedPeriodIntoView(smooth = true): void {
    if (typeof requestAnimationFrame !== 'function') return;
    requestAnimationFrame(() => {
      const selected = this.periodStrip?.nativeElement.querySelector<HTMLElement>(
        '[aria-pressed="true"]',
      );
      selected?.scrollIntoView?.({
        behavior: smooth ? 'smooth' : 'auto',
        block: 'nearest',
        inline: 'center',
      });
      this.updateCurrentPeriodVisibility();
    });
  }

  private transactionsForSelectedWallet(): Transaction[] {
    const walletId = this.selectedWalletId();
    return walletId
      ? this.transactions().filter((transaction) => transaction.walletId === walletId)
      : this.transactions();
  }

  private groupTransactions(): TransactionDay[] {
    const groups = new Map<string, Transaction[]>();
    for (const transaction of this.filteredTransactions()) {
      const date = new Date(transaction.occurredAt);
      const key = `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
      const transactions = groups.get(key) ?? [];
      transactions.push(transaction);
      groups.set(key, transactions);
    }

    return [...groups.entries()]
      .map(([key, transactions]) => {
        const date = new Date(transactions[0].occurredAt);
        return {
          key,
          dayNumber: date.toLocaleDateString(this.language.locale(), { day: '2-digit' }),
          weekday: date.toLocaleDateString(this.language.locale(), { weekday: 'long' }),
          dateLabel: date.toLocaleDateString(this.language.locale(), { month: 'long', year: 'numeric' }),
          total: transactions.reduce((sum, transaction) => sum + this.transactionAmount(transaction), 0),
          transactions: [...transactions].sort(
            (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime(),
          ),
        };
      })
      .sort((a, b) => b.key.localeCompare(a.key));
  }

  private getLoadError(error: unknown): TranslationKey {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'transactions.errorOffline';
    }
    return 'transactions.errorLoad';
  }

  private periodTranslations(): PeriodTranslations {
    return {
      today: this.language.t('period.today'),
      yesterday: this.language.t('period.yesterday'),
      day: this.language.t('period.day'),
      week: this.language.t('period.week'),
      month: this.language.t('period.month'),
      year: this.language.t('period.year'),
      thisPeriod: this.language.t('period.this'),
      lastPeriod: this.language.t('period.last'),
      quarter: this.language.t('period.quarter'),
      navigationQuarter: this.language.t('period.navigationQuarter'),
    };
  }
}

interface PeriodBoundsRequest {
  start: Date;
  end: Date;
  inclusiveEnd: boolean;
}
