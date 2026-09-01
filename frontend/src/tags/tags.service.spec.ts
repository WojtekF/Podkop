import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  provideHttpClientTesting,
  HttpTestingController,
  TestRequest,
} from '@angular/common/http/testing';
import { TagPageDto, TagsService } from './tags.service';
import { ref, tagPage } from './tags.fixtures';

describe('TagsService', () => {
  let service: TagsService;
  let httpMock: HttpTestingController;

  const params = (req: TestRequest) => new URL(req.request.urlWithParams, 'http://test').searchParams;

  const expectTagRequest = () => httpMock.expectOne((r) => r.url.startsWith('/api/tags/'));

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TagsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs the named page of the named tag', () => {
    let received: TagPageDto | undefined;
    service.getTagPage('dotnet', 'all', 3).subscribe((page) => (received = page));

    const req = expectTagRequest();
    expect(req.request.method).toBe('GET');
    expect(req.request.url).toBe('/api/tags/dotnet');
    expect(params(req).get('page')).toBe('3');

    const body = tagPage([ref(1)]);
    req.flush(body);
    expect(received).toEqual(body);
  });

  it('always names the page, even for page 1', () => {
    service.getTagPage('dotnet', 'all', 1).subscribe();

    const req = expectTagRequest();
    expect(params(req).get('page')).toBe('1');
    req.flush(tagPage([]));
  });

  it('sends the type filter it was given', () => {
    service.getTagPage('dotnet', 'entries', 1).subscribe();

    const req = expectTagRequest();
    expect(params(req).get('type')).toBe('entries');
    req.flush(tagPage([]));
  });

  it('sends the name exactly as it was given — folding is the server’s job', () => {
    // Any casing lands on the canonical page, so the client must not pre-normalise and risk
    // disagreeing with the server about what the canonical form is.
    service.getTagPage('DotNet', 'all', 1).subscribe();

    const req = expectTagRequest();
    expect(req.request.url).toBe('/api/tags/DotNet');
    req.flush(tagPage([]));
  });

  it('does not send a limit — the server default applies', () => {
    service.getTagPage('dotnet', 'all', 1).subscribe();

    const req = expectTagRequest();
    expect(params(req).has('limit')).toBe(false);
    req.flush(tagPage([]));
  });

  it('passes a 404 through so the caller can show the not-found state', () => {
    let status: number | undefined;
    service.getTagPage('nosuchtag', 'all', 1).subscribe({ error: (e) => (status = e.status) });

    expectTagRequest().flush('missing', { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });

  it('fails and cancels the request when no response arrives within 5 seconds', () => {
    vi.useFakeTimers();
    try {
      let errored = false;
      service.getTagPage('dotnet', 'all', 1).subscribe({ error: () => (errored = true) });

      const req = expectTagRequest();
      vi.advanceTimersByTime(4999);
      expect(req.cancelled).toBe(false);

      vi.advanceTimersByTime(1);
      expect(errored).toBe(true);
      expect(req.cancelled).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });
});
