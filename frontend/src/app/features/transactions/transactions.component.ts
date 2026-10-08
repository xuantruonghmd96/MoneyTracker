import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of, Subject, switchMap, tap } from 'rxjs';
import { Category, CategoryType, Transaction, Wallet } from '../../core/api.models';
import { MoneyApiService } from '../../core/money-api.service';
import {
  PeriodDirection,
  PeriodType,
  formatPeriodLabel,
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
  label: string;
  accessibleLabel: string;
  selected: boolean;
}

const PERIOD_TYPES: PeriodType[] = ['Day', 'Week', 'Month', 'Quarter', 'Year'];
const SWIPE_THRESHOLD = 55;

@Component({
  selector: 'app-transactions',
  imports: [RouterLink],
  templateUrl: './transactions.component.html',
  styleUrl: './transactions.scss',
})
export class TransactionsComponent implements OnInit {
  private readonly api = inject(MoneyApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly periodRequests = new Subject<PeriodBoundsRequest>();
  private swipeStart: { x: number; y: number; pointerId: number } | null = null;
  readonly periodTypes = PERIOD_TYPES;
  readonly selectedDate = signal(new Date());
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
  readonly errorMessage = signal('');

  readonly periodTitle = computed(() =>
    formatPeriodLabel(this.selectedDate(), this.periodType()),
  );
  readonly periods = computed<PeriodOption[]>(() => {
    const selected = this.selectedDate();
    const type = this.periodType();
    const previous = movePeriod(selected, type, -1);
    const next = movePeriod(selected, type, 1);
    return [previous, selected, next].map((date, index) => ({
      date,
      label: formatPeriodLabel(date, type, true),
      accessibleLabel: `${index === 0 ? 'Previous' : index === 2 ? 'Next' : 'Selected'} ${type.toLowerCase()}: ${formatPeriodLabel(date, type)}`,
      selected: index === 1,
    }));
  });
  readonly selectedWalletName = computed(() => {
    const walletId = this.selectedWalletId();
    if (!walletId) return 'All wallets';
    return this.wallets().find((wallet) => wallet.id === walletId)?.name ?? 'All wallets';
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
          this.errorMessage.set('');
          this.transactions.set([]);
          this.categories.set([]);
          this.wallets.set([]);
        }),
        switchMap(({ start, end }) =>
          forkJoin({
            transactions: this.api.getTransactions(start, end),
            categories: this.api.getCategories(),
            wallets: this.api.getWallets(),
          }).pipe(
            catchError((error: unknown) => {
              this.errorMessage.set(this.getLoadError(error));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((data) => {
        if (data) {
          const { start, end } = getPeriodBounds(this.selectedDate(), this.periodType());
          const startTime = start.getTime();
          const endTime = end.getTime();
          this.transactions.set(
            data.transactions.filter((transaction) => {
              const occurredAt = new Date(transaction.occurredAt).getTime();
              return occurredAt >= startTime && occurredAt < endTime;
            }),
          );
          this.categories.set(data.categories);
          this.wallets.set(data.wallets);
        }
        this.loading.set(false);
      });

    this.reload();
  }

  selectWallet(walletId: string | null): void {
    this.selectedWalletId.set(walletId);
    this.walletMenuOpen.set(false);
  }

  selectPeriod(date: Date): void {
    const bounds = getPeriodBounds(date, this.periodType());
    const currentBounds = getPeriodBounds(this.selectedDate(), this.periodType());
    if (bounds.start.getTime() === currentBounds.start.getTime()) return;
    this.selectedDate.set(date);
    this.reload();
  }

  selectPeriodType(type: PeriodType): void {
    this.periodType.set(type);
    this.periodTypesOpen.set(false);
    this.reload();
  }

  toggleMenu(): void {
    this.periodTypesOpen.set(false);
    this.menuOpen.update((open) => !open);
  }

  openPeriodTypes(): void {
    this.menuOpen.set(false);
    this.periodTypesOpen.set(true);
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
    const { start, end } = getPeriodBounds(this.selectedDate(), this.periodType());
    this.periodRequests.next({ start, end });
  }

  categoryName(id: string): string {
    return this.categories().find((category) => category.id === id)?.name ?? 'Other';
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
    return `${new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(amount)} ₫`;
  }

  private moveSelectedPeriod(direction: PeriodDirection): void {
    this.selectPeriod(movePeriod(this.selectedDate(), this.periodType(), direction));
  }

  private formatWalletMoney(amount: number, currency: string): string {
    try {
      return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency,
        maximumFractionDigits: currency === 'VND' ? 0 : 2,
      }).format(amount);
    } catch {
      return `${new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(amount)} ${currency}`;
    }
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
          dayNumber: date.toLocaleDateString('en-US', { day: '2-digit' }),
          weekday: date.toLocaleDateString('en-US', { weekday: 'long' }),
          dateLabel: date.toLocaleDateString('en-US', { month: 'long', year: 'numeric' }),
          total: transactions.reduce((sum, transaction) => sum + this.transactionAmount(transaction), 0),
          transactions: [...transactions].sort(
            (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime(),
          ),
        };
      })
      .sort((a, b) => b.key.localeCompare(a.key));
  }

  private getLoadError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 0) {
      return 'Could not reach the API. Make sure it is running on localhost:5100.';
    }
    return 'Your transactions could not be loaded. Please try again.';
  }
}

interface PeriodBoundsRequest {
  start: Date;
  end: Date;
}
