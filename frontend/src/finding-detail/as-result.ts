import { HttpErrorResponse } from '@angular/common/http';
import { catchError, Observable, of, TimeoutError } from 'rxjs';

export type LoadResult<T> = T | HttpErrorResponse | TimeoutError;

export function asResult<T>(source: Observable<T>): Observable<LoadResult<T>> {
  return source.pipe(catchError((error: HttpErrorResponse | TimeoutError) => of(error)));
}
