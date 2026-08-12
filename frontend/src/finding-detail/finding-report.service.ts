import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

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

/** A comment-targeted filing (issue #33): the same report intent plus the comment it targets. */
export interface FileCommentReportIntent extends FileReportIntent {
  commentId: string;
}

/**
 * The batch my-reports state of one finding's discussion (issue #33): the ids of the comments —
 * top-level and replies alike — the current user already reported.
 */
export interface MyCommentReportsDto {
  reportedCommentIds: string[];
}

/**
 * HTTP client for the Moderation slice's my-report endpoints (issues #32/#33). Reports are
 * invisible to regular users — the member-visible facts are whether the current user already
 * reported a finding or a comment, and filing one. A duplicate filing is refused with a 409
 * whose problem type is `podkop:problem:already-reported`.
 */
@Injectable({
  providedIn: 'root',
})
export class FindingReportService {
  private readonly http = inject(HttpClient);

  /** Whether the current user already reported the finding. */
  getMyReport(findingId: string): Observable<MyReportDto> {
    return this.http.get<MyReportDto>(`/api/findings/${findingId}/my-report`).pipe(timeout(5000));
  }

  /** Files the current user's one report on the finding; answers the fresh my-report state. */
  fileReport(findingId: string, intent: FileReportIntent): Observable<MyReportDto> {
    return this.http
      .post<MyReportDto>(`/api/findings/${findingId}/my-report`, intent)
      .pipe(timeout(5000));
  }

  /**
   * Which comments of the finding's discussion the current user already reported — one batch
   * request for the whole thread list, loaded with the page (issue #33).
   */
  getMyCommentReports(findingId: string): Observable<MyCommentReportsDto> {
    throw new Error('not implemented');
  }

  /** Files the current user's one report on the comment; answers the fresh my-report state. */
  fileCommentReport(commentId: string, intent: FileReportIntent): Observable<MyReportDto> {
    throw new Error('not implemented');
  }
}
