import { formatPeriodLabel, getPeriodBounds, movePeriod } from './period';

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

  it('moves to the adjacent period without skipping a day', () => {
    expect(movePeriod(new Date(2026, 9, 8), 'Day', 1)).toEqual(new Date(2026, 9, 9));
    expect(movePeriod(new Date(2026, 9, 8), 'Week', -1)).toEqual(new Date(2026, 8, 28));
    expect(movePeriod(new Date(2026, 11, 8), 'Month', 1)).toEqual(new Date(2027, 0, 1));
    expect(movePeriod(new Date(2026, 9, 8), 'Quarter', -1)).toEqual(new Date(2026, 6, 1));
    expect(movePeriod(new Date(2026, 9, 8), 'Year', 1)).toEqual(new Date(2027, 0, 1));
  });
});
