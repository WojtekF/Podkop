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
  };
};
