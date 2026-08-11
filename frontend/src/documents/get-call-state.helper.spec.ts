import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, TimeoutError } from 'rxjs';
import { getCallState } from './get-call-state.helper';

describe('getCallState', () => {
  // getCallState reads the injection context (toSignal), so specs enter one the same way the
  // page components do via their field initializers.
  const create = <T>(source: Subject<T>) =>
    TestBed.runInInjectionContext(() => getCallState<T>(source));

  it('is loading with no data before the source emits', () => {
    const source = new Subject<string>();
    const state = create(source);

    expect(state.loading()).toBe(true);
    expect(state.error()).toBe(false);
    expect(state.value()).toBeUndefined();
    expect(state.data()).toBeUndefined();
  });

  it('leaves loading and exposes the payload through data once the source emits', () => {
    const source = new Subject<string>();
    const state = create(source);

    source.next('document');

    expect(state.loading()).toBe(false);
    expect(state.error()).toBe(false);
    expect(state.value()).toBe('document');
    expect(state.data()).toBe('document');
  });

  it('flags an HTTP failure as an error state with no data', () => {
    const source = new Subject<string>();
    const state = create(source);

    source.error(new HttpErrorResponse({ status: 500, statusText: 'Internal Server Error' }));

    expect(state.loading()).toBe(false);
    expect(state.error()).toBe(true);
    expect(state.data()).toBeUndefined();
  });

  it('flags a timeout as an error state with no data', () => {
    const source = new Subject<string>();
    const state = create(source);

    source.error(new TimeoutError());

    expect(state.loading()).toBe(false);
    expect(state.error()).toBe(true);
    expect(state.data()).toBeUndefined();
  });
});
