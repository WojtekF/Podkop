import { toSignal } from '@angular/core/rxjs-interop';
import { asResult, LoadResult } from '../shared/as-result';
import { Observable, TimeoutError } from 'rxjs';
import { computed } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';

export const getCallState = <T>(method: () => Observable<T>) => {
  const value = toSignal<LoadResult<T> | undefined>(asResult(method()), {
    initialValue: undefined,
  });

  return {
    value,
    loading: computed(() => value() === undefined),
    error: computed(() => value() instanceof HttpErrorResponse || value() instanceof TimeoutError),
    // The loaded payload, narrowed to T: templates read this instead of value() so the
    // type-checker knows the error/pending cases are already excluded.
    data: computed(() => {
      const v = value();
      return v === undefined || v instanceof HttpErrorResponse || v instanceof TimeoutError
        ? undefined
        : v;
    }),
  };
};
