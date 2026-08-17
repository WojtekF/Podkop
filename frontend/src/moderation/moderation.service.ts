import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, timeout } from 'rxjs';

/**
 * One pending report of a case (issue #34): the cited Statute Point as the version the report
 * pinned worded it — the citation composed section.point (e.g. "2.1"), the same form the
 * statute page renders — the reporter's optional note, and when it was filed. Reporter
 * identities never reach the client.
 */
export interface CaseReportDto {
  pointCitation: string;
  pointText: string;
  note: string | null;
  filedAt: string;
}

/**
 * One open case of the moderator queue (issue #34): a reported finding or comment with all its
 * pending reports. findingId names the finding page where the content lives — the finding
 * itself, or the finding a reported comment belongs to; the preview arrives already cut to the
 * server's cap.
 */
export interface CaseSummaryDto {
  targetKind: 'Finding' | 'Comment';
  targetId: string;
  findingId: string;
  preview: string;
  author: string;
  reportCount: number;
  reports: CaseReportDto[];
}

/**
 * HTTP client for the Moderation slice's moderators-only surface (issue #34). A non-moderator's
 * call is refused with a 403 whose problem type is `podkop:problem:moderators-only`.
 */
@Injectable({
  providedIn: 'root',
})
export class ModerationService {
  private readonly http = inject(HttpClient);

  /** The queue of open cases, in the server's oldest-grievance-first order. */
  getCaseQueue(): Observable<CaseSummaryDto[]> {
    return this.http.get<CaseSummaryDto[]>('/api/moderation/cases').pipe(timeout(5000));
  }

  /**
   * Issues the Dismissed verdict on one open case (issue #35): a POST to
   * `/api/moderation/cases/{targetKind}/{targetId}/verdict` carrying the body
   * `{ verdict: 'Dismissed' }`. Success answers 204 with no body and the call completes empty.
   * Refusals arrive as problem responses — moderators-only and own-case as 403, and 404
   * `podkop:problem:unknown-case` when no open case exists for the target (never reported, or
   * already resolved). An unanswered call gives up after the same five seconds the queue fetch
   * waits.
   */
  dismissCase(targetKind: CaseSummaryDto['targetKind'], targetId: string): Observable<void> {
    throw new Error('not implemented');
  }
}
