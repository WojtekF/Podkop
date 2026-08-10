import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TimeoutError } from 'rxjs';
import { DocumentsService, PrivacyPolicyDto, StatuteDto } from './documents.service';
import { privacyPolicy, statute } from './documents.fixtures';

describe('DocumentsService', () => {
  let service: DocumentsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DocumentsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs the current statute from the statute endpoint', () => {
    let received: StatuteDto | undefined;
    service.getCurrentStatute().subscribe((doc) => (received = doc));

    const req = httpMock.expectOne('/api/statute');
    expect(req.request.method).toBe('GET');

    const body = statute();
    req.flush(body);
    expect(received).toEqual(body);
  });

  it('GETs the current privacy policy from the privacy-policy endpoint', () => {
    let received: PrivacyPolicyDto | undefined;
    service.getCurrentPrivacyPolicy().subscribe((doc) => (received = doc));

    const req = httpMock.expectOne('/api/privacy-policy');
    expect(req.request.method).toBe('GET');

    const body = privacyPolicy();
    req.flush(body);
    expect(received).toEqual(body);
  });

  it('propagates a server error as an error the caller can inspect', () => {
    let status: number | undefined;
    service.getCurrentStatute().subscribe({
      error: (e: { status?: number }) => (status = e.status),
    });

    httpMock
      .expectOne('/api/statute')
      .flush('broken', { status: 500, statusText: 'Internal Server Error' });

    expect(status).toBe(500);
  });

  it('fails and cancels the request when no response arrives within 5 seconds', () => {
    vi.useFakeTimers();
    try {
      let error: unknown;
      service.getCurrentStatute().subscribe({ error: (e) => (error = e) });

      const req = httpMock.expectOne('/api/statute');
      vi.advanceTimersByTime(4999);
      expect(req.cancelled).toBe(false);

      vi.advanceTimersByTime(1);
      expect(error).toBeInstanceOf(TimeoutError);
      expect(req.cancelled).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });
});
