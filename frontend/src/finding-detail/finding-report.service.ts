import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/** The my-report state of one finding: whether the current user already reported it (issue #32). */
export interface MyReportDto {
  reported: boolean;
}

/**
 * What filing a report carries (issue #32): the id of the reportable Statute Point it cites,
 * plus an optional note — null when the member added none.
 */
export interface FileReportIntent {
  statutePointId: string;
  note: string | null;
}

/**
 * HTTP client for the Moderation slice's my-report endpoints (issue #32). Reports are invisible
 * to regular users — the one member-visible fact is whether the current user already reported a
 * finding, and filing one. A duplicate filing is refused with a 409 whose problem type is
 * `podkop:problem:already-reported`.
 */
@Injectable({
  providedIn: 'root',
})
export class FindingReportService {
  private readonly http = inject(HttpClient);

  /** Whether the current user already reported the finding. */
  getMyReport(findingId: string): Observable<MyReportDto> {
    throw new Error('not implemented');
  }

  /** Files the current user's one report on the finding; answers the fresh my-report state. */
  fileReport(findingId: string, intent: FileReportIntent): Observable<MyReportDto> {
    throw new Error('not implemented');
  }
}
