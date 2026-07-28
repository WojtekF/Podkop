import { TimeAgoPipe } from './timeAgo.pipe';

describe('TimeAgoPipe', () => {
  const now = new Date('2026-07-28T12:00:00Z');
  let pipe: TimeAgoPipe;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(now);
    pipe = new TimeAgoPipe();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('describes a just-posted comment without a calendar date', () => {
    expect(pipe.transform('2026-07-28T11:59:45Z')).toBe('less than a minute ago');
  });

  it('renders minutes-scale distances', () => {
    expect(pipe.transform('2026-07-28T11:55:00Z')).toBe('5 minutes ago');
  });

  it('renders hours-scale distances', () => {
    expect(pipe.transform('2026-07-28T11:00:00Z')).toBe('about 1 hour ago');
  });

  it('renders days-scale distances', () => {
    expect(pipe.transform('2026-07-26T12:00:00Z')).toBe('2 days ago');
  });

  it('accepts a Date instance as well as an ISO string', () => {
    expect(pipe.transform(new Date('2026-07-28T11:55:00Z'))).toBe('5 minutes ago');
  });

  it('always carries the past/future suffix', () => {
    expect(pipe.transform('2026-07-25T12:00:00Z')).toMatch(/ago$/);
    expect(pipe.transform('2026-07-31T12:00:00Z')).toMatch(/^in /);
  });
});
