/* tslint:disable:no-unused-variable */

import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  provideHttpClientTesting,
  HttpTestingController,
} from '@angular/common/http/testing';
import { SinkService } from './sink.service';

describe('Service: Sink', () => {
  let service: SinkService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SinkService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SinkService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should ...', () => {
    expect(service).toBeTruthy();
  });

  it('should GET the item collection', () => {
    service.getItems().subscribe();

    const req = httpMock.expectOne('/api/sink');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });
});
