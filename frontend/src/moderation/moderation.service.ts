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

  dismissCase(targetKind: CaseSummaryDto['targetKind'], targetId: string): Observable<void> {
    return this.http.post<void>(`/api/moderation/cases/${targetKind}/${targetId}/verdict`, {
      verdict: 'Dismissed',
    });
  }
}
