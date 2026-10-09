import { Injectable, signal } from '@angular/core';
import { PeriodType } from './period';

@Injectable({ providedIn: 'root' })
export class TransactionsViewState {
  readonly selectedDate = signal(new Date());
  readonly futureSelected = signal(false);
  readonly periodType = signal<PeriodType>('Month');
}
