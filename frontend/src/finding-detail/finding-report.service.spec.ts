import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ReportService, MyCommentReportsDto, MyReportDto } from './finding-report.service';
import { findingId as id, myCommentReports, myReport } from './finding-detail.fixtures';

describe('FindingReportService', () => {
  let service: ReportService;
  let httpMock: HttpTestingController;

  const endpoint = `/api/findings/${id}/my-report`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs the my-report state from the finding my-report endpoint', () => {
    let received: MyReportDto | undefined;
    service.getMyReport(id).subscribe((state) => (received = state));

    const req = httpMock.expectOne(endpoint);
    expect(req.request.method).toBe('GET');

    req.flush(myReport({ reported: true }));
    expect(received).toEqual(myReport({ reported: true }));
  });

  it('POSTs the cited point and note to file a report', () => {
    let received: MyReportDto | undefined;
    service
      .fileReport(id, {
        statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
        note: 'Links a spam farm.',
      })
      .subscribe((state) => (received = state));

    const req = httpMock.expectOne(endpoint);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
      note: 'Links a spam farm.',
    });

    req.flush(myReport({ reported: true }), { status: 201, statusText: 'Created' });
    expect(received).toEqual(myReport({ reported: true }));
  });

  it('a report without a note POSTs an explicit null note', () => {
    service
      .fileReport(id, {
        statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
        note: null,
      })
      .subscribe();

    const req = httpMock.expectOne(endpoint);
    expect(req.request.body).toEqual({
      statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
      note: null,
    });
  });

  // Issue #33 — the comment-report endpoints of the same Moderation slice.
  it('GETs the batch my-reports state of the discussion in one request', () => {
    let received: MyCommentReportsDto | undefined;
    service.getMyCommentReports(id).subscribe((state) => (received = state));

    const req = httpMock.expectOne(`/api/findings/${id}/comments/my-reports`);
    expect(req.request.method).toBe('GET');

    const reported = myCommentReports({
      reportedCommentIds: ['c0000000-0000-4000-8000-000000000001'],
    });
    req.flush(reported);
    expect(received).toEqual(reported);
  });

  it('POSTs the cited point and note to file a comment report on the comment endpoint', () => {
    const commentId = 'c0000000-0000-4000-8000-000000000001';
    let received: MyReportDto | undefined;
    service
      .fileCommentReport(commentId, {
        statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
        note: 'Spam in the discussion.',
      })
      .subscribe((state) => (received = state));

    const req = httpMock.expectOne(`/api/comments/${commentId}/my-report`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
      note: 'Spam in the discussion.',
    });

    req.flush(myReport({ reported: true }), { status: 201, statusText: 'Created' });
    expect(received).toEqual(myReport({ reported: true }));
  });

  it('propagates the duplicate refusal as an error the caller can inspect', () => {
    let status: number | undefined;
    service
      .fileReport(id, {
        statutePointId: 'aaaa0000-0000-4000-8000-000000000002',
        note: null,
      })
      .subscribe({
        error: (e: { status?: number }) => (status = e.status),
      });

    httpMock
      .expectOne(endpoint)
      .flush({ type: 'podkop:problem:already-reported' }, { status: 409, statusText: 'Conflict' });

    expect(status).toBe(409);
  });
});
