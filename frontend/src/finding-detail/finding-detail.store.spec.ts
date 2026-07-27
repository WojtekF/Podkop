import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { findingDetail as detail, findingId as id } from './finding-detail.fixtures';
import { FindingDetailStore } from './finding-detail.store';

describe('FindingDetailStore', () => {
  let store: InstanceType<typeof FindingDetailStore>;
  let httpMock: HttpTestingController;

  const otherId = '0d4f9a3e-2222-4222-8333-444455556666';

  const expectDetailRequest = (findingId: string) => httpMock.expectOne(`/api/findings/${findingId}`);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FindingDetailStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(FindingDetailStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('starts loading with no finding', () => {
    expect(store.status()).toBe('loading');
    expect(store.finding()).toBeNull();
  });

  it('load fetches the finding and exposes it as loaded', () => {
    store.load(id);

    expect(store.status()).toBe('loading');
    expectDetailRequest(id).flush(detail());

    expect(store.status()).toBe('loaded');
    expect(store.finding()).toEqual(detail());
  });

  it('a 404 puts the store in the not-found state, distinct from a load error', () => {
    store.load(id);

    expectDetailRequest(id).flush('missing', { status: 404, statusText: 'Not Found' });

    expect(store.status()).toBe('notFound');
    expect(store.finding()).toBeNull();
  });

  it('any other failure puts the store in the error state', () => {
    store.load(id);

    expectDetailRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(store.status()).toBe('error');
  });

  it('retry re-requests the id that failed', () => {
    store.load(id);
    expectDetailRequest(id).flush('boom', { status: 500, statusText: 'Server Error' });

    store.retry();

    expectDetailRequest(id).flush(detail());
    expect(store.status()).toBe('loaded');
    expect(store.finding()).toEqual(detail());
  });

  it('loading a different id replaces the finding it holds', () => {
    store.load(id);
    expectDetailRequest(id).flush(detail());

    store.load(otherId);
    expectDetailRequest(otherId).flush(detail({ id: otherId, title: 'Another finding' }));

    expect(store.finding()?.id).toBe(otherId);
    expect(store.finding()?.title).toBe('Another finding');
  });
});
