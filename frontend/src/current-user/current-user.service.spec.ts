import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TimeoutError } from 'rxjs';
import { CurrentUserService, MyUserDto } from './current-user.service';
import { myUser } from './current-user.fixtures';

describe('CurrentUserService', () => {
  let service: CurrentUserService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CurrentUserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('GETs the acting user from the my-user endpoint', () => {
    let received: MyUserDto | undefined;
    service.getMyUser().subscribe((user) => (received = user));

    const req = httpMock.expectOne('/api/my-user');
    expect(req.request.method).toBe('GET');

    req.flush(myUser());
    expect(received).toEqual(myUser());
  });

  it('fails and cancels the request when no response arrives within 5 seconds', () => {
    vi.useFakeTimers();
    try {
      let error: unknown;
      service.getMyUser().subscribe({ error: (e) => (error = e) });

      const req = httpMock.expectOne('/api/my-user');
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
