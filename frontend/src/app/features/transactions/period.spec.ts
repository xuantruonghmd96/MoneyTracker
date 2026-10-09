import {
  formatNavigationPeriodLabel,
  formatPeriodLabel,
  getPeriodBounds,
  movePeriod,
} from './period';

describe('period utilities', () => {
  it('returns a one-day half-open range for a day period', () => {
    const { start, end } = getPeriodBounds(new Date(2026, 9, 8, 15), 'Day');

    expect(start).toEqual(new Date(2026, 9, 8));
    expect(end).toEqual(new Date(2026, 9, 9));
  });

  it('uses Monday through Sunday for a week period', () => {
    const { start, end } = getPeriodBounds(new Date(2026, 9, 8), 'Week');

    expect(start).toEqual(new Date(2026, 9, 5));
    expect(end).toEqual(new Date(2026, 9, 12));
  });

  it('returns calendar month boundaries', () => {
    const { start, end } = getPeriodBounds(new Date(2026, 9, 8), 'Month');

    expect(start).toEqual(new Date(2026, 9, 1));
    expect(end).toEqual(new Date(2026, 10, 1));
  });

  it('returns calendar quarter boundaries and a quarter label', () => {
    const { start, end } = getPeriodBounds(new Date(2026, 9, 8), 'Quarter');

    expect(start).toEqual(new Date(2026, 9, 1));
    expect(end).toEqual(new Date(2027, 0, 1));
    expect(formatPeriodLabel(new Date(2026, 9, 8), 'Quarter')).toBe('Q4 2026');
  });

  it('returns calendar year boundaries', () => {
    const { start, end } = getPeriodBounds(new Date(2026, 9, 8), 'Year');

    expect(start).toEqual(new Date(2026, 0, 1));
    expect(end).toEqual(new Date(2027, 0, 1));
  });

  it('uses relative labels for the current and immediately previous periods', () => {
    const current = new Date(2026, 9, 8);
    const previousDay = movePeriod(current, 'Day', -1);
    const previousWeek = movePeriod(current, 'Week', -1);
    const previousMonth = movePeriod(current, 'Month', -1);
    const previousYear = movePeriod(current, 'Year', -1);

    expect(formatNavigationPeriodLabel(previousDay, 'Day', current)).toBe('Yesterday');
    expect(formatNavigationPeriodLabel(current, 'Day', current)).toBe('Today');
    expect(formatNavigationPeriodLabel(previousWeek, 'Week', current)).toBe('Last week');
    expect(formatNavigationPeriodLabel(current, 'Week', current)).toBe('This week');
    expect(formatNavigationPeriodLabel(previousMonth, 'Month', current)).toBe('Last month');
    expect(formatNavigationPeriodLabel(current, 'Month', current)).toBe('This month');
    expect(formatNavigationPeriodLabel(previousYear, 'Year', current)).toBe('Last year');
    expect(formatNavigationPeriodLabel(current, 'Year', current)).toBe('This year');
  });

  it('labels quarters with their Roman numeral and year', () => {
    const current = new Date(2026, 9, 8);

    expect(formatNavigationPeriodLabel(new Date(2026, 6, 1), 'Quarter', current)).toBe('III-2026');
    expect(formatNavigationPeriodLabel(current, 'Quarter', current)).toBe('IV-2026');
  });

  it('formats Vietnamese period labels and relative period names', () => {
    const translations = {
      today: 'Hôm nay',
      yesterday: 'Hôm qua',
      day: 'ngày',
      week: 'tuần',
      month: 'tháng',
      year: 'năm',
      thisPeriod: '{period} này',
      lastPeriod: '{period} trước',
      quarter: 'Quý {quarter} năm {year}',
      navigationQuarter: 'Quý {quarter} năm {year}',
    };
    const current = new Date(2026, 9, 8);

    expect(formatNavigationPeriodLabel(current, 'Day', current, false, 'vi-VN', translations)).toBe(
      'Hôm nay',
    );
    expect(
      formatNavigationPeriodLabel(
        movePeriod(current, 'Month', -1),
        'Month',
        current,
        false,
        'vi-VN',
        translations,
      ),
    ).toBe('tháng trước');
    expect(formatPeriodLabel(current, 'Month', false, 'vi-VN', translations)).toContain('tháng 10');
    expect(formatPeriodLabel(current, 'Quarter', false, 'vi-VN', translations)).toBe(
      'Quý 4 năm 2026',
    );
  });

  it('moves to the adjacent period without skipping a day', () => {
    expect(movePeriod(new Date(2026, 9, 8), 'Day', 1)).toEqual(new Date(2026, 9, 9));
    expect(movePeriod(new Date(2026, 9, 8), 'Week', -1)).toEqual(new Date(2026, 8, 28));
    expect(movePeriod(new Date(2026, 11, 8), 'Month', 1)).toEqual(new Date(2027, 0, 1));
    expect(movePeriod(new Date(2026, 9, 8), 'Quarter', -1)).toEqual(new Date(2026, 6, 1));
    expect(movePeriod(new Date(2026, 9, 8), 'Year', 1)).toEqual(new Date(2027, 0, 1));
  });
});
