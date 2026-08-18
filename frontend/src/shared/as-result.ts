import { HttpErrorResponse } from '@angular/common/http';
import { catchError, Observable, of, TimeoutError } from 'rxjs';

export type LoadResult<T> = T | HttpErrorResponse | TimeoutError;

export function asResult<T>(source: Observable<T>): Observable<LoadResult<T>> {
  return source.pipe(catchError((error: HttpErrorResponse | TimeoutError) => of(error)));
}

export const isLoadFailure = <T>(result: LoadResult<T>) => {
  return result instanceof HttpErrorResponse || result instanceof TimeoutError;
};

export const isNotFound = <T>(input: T | HttpErrorResponse): boolean => {
  if (input instanceof HttpErrorResponse) {
    return input.status === 404;
  }
  return false;
};
