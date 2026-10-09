import { HttpErrorResponse } from '@angular/common/http';
import {
  afterNextRender,
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  HostListener,
  Injector,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, map, of, Subject, switchMap, tap } from 'rxjs';
import {
  Category,
  CategoryType,
  CreateTransactionRequest,
  Participant,
  Transaction,
  UpdateTransactionRequest,
  Wallet,
} from '../../core/api.models';
import { LanguageService, TranslationKey } from '../../core/language.service';
import { MoneyApiService } from '../../core/money-api.service';
import { TransactionsViewState } from './transactions-view-state.service';
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
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './transactions.component.html',
  styleUrl: './transactions.scss',
})
export class TransactionsComponent implements OnInit, AfterViewInit {
  private readonly api = inject(MoneyApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly viewState = inject(TransactionsViewState);
  readonly language = inject(LanguageService);
  private readonly periodRequests = new Subject<PeriodBoundsRequest>();
  private swipeStart: { x: number; y: number; pointerId: number } | null = null;
  private readonly periodsBeforeSelected = signal(PERIODS_BEFORE_SELECTED);
  private loadingEarlierPeriods = false;
  @ViewChild('periodStrip') private periodStrip?: ElementRef<HTMLElement>;
  readonly currentPeriodVisible = signal(false);
  readonly periodTypes = PERIOD_TYPES;
  readonly selectedDate = this.viewState.selectedDate;
  readonly futureSelected = this.viewState.futureSelected;
  readonly periodType = this.viewState.periodType;
  readonly selectedWalletId = signal<string | null>(null);
  readonly walletMenuOpen = signal(false);
  readonly menuOpen = signal(false);
  readonly periodTypesOpen = signal(false);
  readonly searchOpen = signal(false);
  readonly searchQuery = signal('');
  readonly transactions = signal<Transaction[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly wallets = signal<Wallet[]>([]);
  readonly participants = signal<Participant[]>([]);
  readonly loading = signal(true);
  readonly categoryWalletAssignments = signal<Record<string, string[]>>({});
  readonly categoryAssignmentsLoading = signal(false);
  readonly categoryAssignmentsError = signal(false);
  private categoryAssignmentsLoaded = false;
  readonly formWalletId = signal('');
  readonly selectableCategories = computed(() => {
    if (this.formMode() !== 'create') return this.categories();
    const walletId = this.formWalletId();
    if (!walletId) return [];
    const assignments = this.categoryWalletAssignments();
    return this.categories().filter(
      (category) => assignments[category.id]?.includes(walletId) ?? false,
    );
  });
  private readonly createRequestEffect = effect(() => {
    if (!this.viewState.createTransactionRequested() || this.loading()) return;
    if (this.viewState.consumeCreateTransactionRequest()) this.openCreate();
  });
  readonly formMode = signal<'create' | 'edit' | null>(null);
  readonly editingTransaction = signal<Transaction | null>(null);
  readonly saving = signal(false);
  readonly participantsLoading = signal(false);
  readonly participantsError = signal<TranslationKey | null>(null);
  readonly saveError = signal<TranslationKey | null>(null);
  readonly confirmDeleteId = signal<string | null>(null);
  readonly deletingId = signal<string | null>(null);
  readonly deleteError = signal<TranslationKey | null>(null);
  readonly form = new FormGroup({
    amount: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.01)],
    }),
    occurredAt: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    walletId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    categoryId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    participantId: new FormControl<string | null>(null),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2048)] }),
  });
  private readonly walletFormSubscription = this.form.controls.walletId.valueChanges
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe((walletId) => {
      this.formWalletId.set(walletId);
      this.syncCreateCategory();
    });
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
        this.scrollSelectedPeriodIntoView(false);
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
    if (!(target instanceof Element)) return;

    if (!target.closest('.toolbar-row, .search-field')) {
      this.closeToolbarControls();
    }

    if (this.formMode() && !target.closest('.transaction-form')) this.closeForm();
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

  openCreate(): void {
    if (this.saving()) return;
    this.formMode.set('create');
    this.editingTransaction.set(null);
    this.saveError.set(null);
    this.participantsError.set(null);
    this.loadParticipants();
    this.loadCategoryWalletAssignments();
    const occurredAt = new Date();
    this.form.reset({
      amount: 0,
      occurredAt: this.toLocalDateTimeInput(occurredAt),
      walletId: this.selectedWalletId() ?? this.wallets()[0]?.id ?? '',
      categoryId: this.categories()[0]?.id ?? '',
      participantId: null,
      note: '',
    });
    this.formWalletId.set(this.form.controls.walletId.value);
    this.syncCreateCategory();
  }

  openEdit(transaction: Transaction): void {
    if (this.saving()) return;
    this.formMode.set('edit');
    this.editingTransaction.set(transaction);
    this.saveError.set(null);
    this.participantsError.set(null);
    this.loadParticipants();
    this.form.reset({
      amount: transaction.amount,
      occurredAt: this.toLocalDateTimeInput(new Date(transaction.occurredAt)),
      walletId: transaction.walletId,
      categoryId: transaction.categoryId,
      participantId: transaction.participantId,
      note: transaction.note ?? '',
    });
  }

  closeForm(): void {
    if (this.saving()) return;
    this.formMode.set(null);
    this.editingTransaction.set(null);
    this.saveError.set(null);
  }

  saveTransaction(): void {
    if (
      this.form.invalid ||
      this.saving() ||
      this.participantsLoading() ||
      this.categoryAssignmentsLoading() ||
      this.categoryAssignmentsError()
    ) {
      this.form.markAllAsTouched();
      return;
    }

    const { amount, occurredAt, walletId, categoryId, participantId, note } =
      this.form.getRawValue();
    const request = {
      amount,
      occurredAt: new Date(occurredAt).toISOString(),
      walletId,
      categoryId,
      participantId,
      note: note.trim() || null,
    };
    const transaction = this.editingTransaction();
    this.saving.set(true);
    this.saveError.set(null);
    const request$ = transaction
      ? this.api.updateTransaction(transaction.id, request satisfies UpdateTransactionRequest)
      : this.api.createTransaction({
          id: crypto.randomUUID(),
          ...request,
        } satisfies CreateTransactionRequest);

    request$
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.formMode.set(null);
          this.editingTransaction.set(null);
          this.reload();
        },
        error: () => this.saveError.set('transactions.errorSave'),
      });
  }

  requestDelete(transaction: Transaction): void {
    this.confirmDeleteId.set(transaction.id);
    this.deleteError.set(null);
  }

  cancelDelete(): void {
    this.confirmDeleteId.set(null);
    this.deleteError.set(null);
  }

  deleteTransaction(transaction: Transaction): void {
    if (this.deletingId()) return;
    this.deletingId.set(transaction.id);
    this.deleteError.set(null);
    this.api
      .deleteTransaction(transaction.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.deletingId.set(null)),
      )
      .subscribe({
        next: () => {
          this.confirmDeleteId.set(null);
          this.reload();
        },
        error: () => this.deleteError.set('transactions.errorDelete'),
      });
  }

  private toLocalDateTimeInput(date: Date): string {
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 16);
  }

  private loadParticipants(): void {
    if (this.participants().length || this.participantsLoading()) return;
    this.participantsLoading.set(true);
    this.api
      .getParticipants()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.participantsLoading.set(false)),
      )
      .subscribe({
        next: (participants) => this.participants.set(participants),
        error: () => this.participantsError.set('transactions.errorParticipants'),
      });
  }

  private loadCategoryWalletAssignments(): void {
    if (this.categoryAssignmentsLoaded || this.categoryAssignmentsLoading()) return;
    const assignedCategories = this.categories().filter((category) => !category.appliesToAllWallets);
    if (!assignedCategories.length) {
      this.categoryWalletAssignments.set(
        Object.fromEntries(this.categories().map((category) => [category.id, this.wallets().map(({ id }) => id)])),
      );
      this.categoryAssignmentsLoaded = true;
      return;
    }

    this.categoryAssignmentsLoading.set(true);
    this.categoryAssignmentsError.set(false);
    forkJoin(
      assignedCategories.map((category) =>
        this.api
          .getAssignedWallets(category.id)
          .pipe(map((walletIds) => [category.id, walletIds] as const)),
      ),
    )
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.categoryAssignmentsLoading.set(false)),
      )
      .subscribe({
        next: (assignments) => {
          this.categoryWalletAssignments.set({
            ...Object.fromEntries(
              this.categories()
                .filter((category) => category.appliesToAllWallets)
                .map((category) => [category.id, this.wallets().map(({ id }) => id)]),
            ),
            ...Object.fromEntries(assignments),
          });
          this.categoryAssignmentsLoaded = true;
          this.syncCreateCategory();
        },
        error: () => this.categoryAssignmentsError.set(true),
      });
  }

  retryCategoryAssignments(): void {
    if (this.categoryAssignmentsLoading()) return;
    this.categoryAssignmentsLoaded = false;
    this.categoryWalletAssignments.set({});
    this.loadCategoryWalletAssignments();
  }

  private syncCreateCategory(): void {
    if (this.formMode() !== 'create') return;
    const selectableCategories = this.selectableCategories();
    const selectedCategoryId = this.form.controls.categoryId.value;
    if (selectableCategories.some((category) => category.id === selectedCategoryId)) return;
    this.form.controls.categoryId.setValue(selectableCategories[0]?.id ?? '');
  }

  shiftOccurredAtDay(days: number): void {
    const value = this.form.controls.occurredAt.value;
    if (!value) return;
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return;
    date.setDate(date.getDate() + days);
    this.form.controls.occurredAt.setValue(this.toLocalDateTimeInput(date));
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
    afterNextRender(
      () => {
        const positionSelectedPeriod = () => {
          const strip = this.periodStrip?.nativeElement;
          const selected = strip?.querySelector<HTMLElement>('[aria-pressed="true"]');
          if (!strip || !selected) return;

          const stripBounds = strip.getBoundingClientRect();
          const selectedBounds = selected.getBoundingClientRect();
          if (strip.clientWidth === 0 || selectedBounds.width === 0) return;

          const maxScrollLeft = strip.scrollWidth - strip.clientWidth;
          const left = Math.max(
            0,
            Math.min(
              maxScrollLeft,
              strip.scrollLeft +
                selectedBounds.left -
                stripBounds.left -
                (strip.clientWidth - selectedBounds.width) / 2,
            ),
          );

          if (smooth) {
            strip.scrollTo({ left, behavior: 'smooth' });
          } else {
            const previousBehavior = strip.style.scrollBehavior;
            strip.style.scrollBehavior = 'auto';
            strip.scrollLeft = left;
            strip.style.scrollBehavior = previousBehavior;
          }
          this.updateCurrentPeriodVisibility();
        };

        if (typeof requestAnimationFrame === 'function') {
          requestAnimationFrame(positionSelectedPeriod);
        } else {
          positionSelectedPeriod();
        }
      },
      { injector: this.injector },
    );
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
