export type PeriodType = 'Day' | 'Week' | 'Month' | 'Quarter' | 'Year';
export type PeriodDirection = -1 | 1;

export interface PeriodBounds {
  start: Date;
  end: Date;
}

export function getPeriodBounds(anchor: Date, type: PeriodType): PeriodBounds {
  const year = anchor.getFullYear();
  const month = anchor.getMonth();

  switch (type) {
    case 'Day': {
      const start = new Date(year, month, anchor.getDate());
      return { start, end: new Date(year, month, anchor.getDate() + 1) };
    }
    case 'Week': {
      const start = new Date(year, month, anchor.getDate());
      start.setDate(start.getDate() - ((start.getDay() + 6) % 7));
      const end = new Date(start);
      end.setDate(end.getDate() + 7);
      return { start, end };
    }
    case 'Month':
      return { start: new Date(year, month, 1), end: new Date(year, month + 1, 1) };
    case 'Quarter': {
      const quarterStartMonth = Math.floor(month / 3) * 3;
      return {
        start: new Date(year, quarterStartMonth, 1),
        end: new Date(year, quarterStartMonth + 3, 1),
      };
    }
    case 'Year':
      return { start: new Date(year, 0, 1), end: new Date(year + 1, 0, 1) };
  }
}

export function movePeriod(anchor: Date, type: PeriodType, direction: PeriodDirection): Date {
  const { start } = getPeriodBounds(anchor, type);
  switch (type) {
    case 'Day':
      start.setDate(start.getDate() + direction);
      break;
    case 'Week':
      start.setDate(start.getDate() + 7 * direction);
      break;
    case 'Month':
      start.setMonth(start.getMonth() + direction);
      break;
    case 'Quarter':
      start.setMonth(start.getMonth() + 3 * direction);
      break;
    case 'Year':
      start.setFullYear(start.getFullYear() + direction);
      break;
  }
  return start;
}

export function formatPeriodLabel(anchor: Date, type: PeriodType, short = false): string {
  const { start, end } = getPeriodBounds(anchor, type);
  const formatter = (date: Date, options: Intl.DateTimeFormatOptions) =>
    date.toLocaleDateString('en-US', options);

  switch (type) {
    case 'Day':
      return formatter(start, short ? { month: 'short', day: 'numeric' } : {
        weekday: 'long',
        month: 'long',
        day: 'numeric',
        year: 'numeric',
      });
    case 'Week': {
      const lastDay = new Date(end);
      lastDay.setDate(lastDay.getDate() - 1);
      const startLabel = formatter(start, { month: 'short', day: 'numeric' });
      const endLabel = formatter(lastDay, { month: 'short', day: 'numeric' });
      return short
        ? `${startLabel}–${endLabel}`
        : `${formatter(start, { month: 'long', day: 'numeric' })} – ${formatter(lastDay, {
            month: 'long',
            day: 'numeric',
            year: 'numeric',
          })}`;
    }
    case 'Month':
      return formatter(start, short ? { month: 'short', year: '2-digit' } : {
        month: 'long',
        year: 'numeric',
      });
    case 'Quarter':
      return `Q${Math.floor(start.getMonth() / 3) + 1}${short ? ` '${String(start.getFullYear()).slice(-2)}` : ` ${start.getFullYear()}`}`;
    case 'Year':
      return String(start.getFullYear());
  }
}

export function formatNavigationPeriodLabel(
  anchor: Date,
  type: PeriodType,
  referenceDate: Date,
  short = false,
): string {
  const periodStart = getPeriodBounds(anchor, type).start;
  const currentStart = getPeriodBounds(referenceDate, type).start;
  const comparison = periodStart.getTime() - currentStart.getTime();

  if (type === 'Day') {
    if (comparison === 0) return 'Today';
    if (comparison < 0 && movePeriod(periodStart, type, 1).getTime() === currentStart.getTime()) {
      return 'Yesterday';
    }
    return formatPeriodLabel(anchor, type, true);
  }

  if (type === 'Week' || type === 'Month' || type === 'Year') {
    if (comparison === 0) return `This ${type.toLowerCase()}`;
    if (comparison < 0 && movePeriod(periodStart, type, 1).getTime() === currentStart.getTime()) {
      return `Last ${type.toLowerCase()}`;
    }
  }

  if (type === 'Quarter') {
    const quarter = Math.floor(periodStart.getMonth() / 3);
    return `${['I', 'II', 'III', 'IV'][quarter]}-${periodStart.getFullYear()}`;
  }

  return formatPeriodLabel(anchor, type, short);
}
