import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { myUser } from '../current-user/current-user.fixtures';
import { moderatorGuard } from './moderator.guard';

@Component({ template: 'main page' })
class MainPageStub {}

@Component({ template: 'case queue' })
class CaseQueueStub {}

describe('moderatorGuard', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: '', component: MainPageStub },
          { path: 'moderation', component: CaseQueueStub, canActivate: [moderatorGuard] },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  // The guard reads the root CurrentUserStore, whose first injection starts the one
  // app-wide who-am-I fetch — so navigating parks on that request until the spec answers
  // it. The navigation promise resolving only after the flush IS the waiting behavior.
  // The router reaches the guard asynchronously (its pipeline hops through microtasks),
  // so the helper yields one macrotask before expecting the request to be open.
  const expectMyUserRequest = async () => {
    await new Promise((resolve) => setTimeout(resolve));
    return httpMock.expectOne({ method: 'GET', url: '/api/my-user' });
  };

  it('admits a moderator once the who-am-I answer arrives', async () => {
    const navigation = harness.navigateByUrl('/moderation');
    (await expectMyUserRequest()).flush(myUser());
    await navigation;

    expect(TestBed.inject(Router).url).toBe('/moderation');
  });

  it('turns a member around to the main page', async () => {
    const navigation = harness.navigateByUrl('/moderation');
    (await expectMyUserRequest()).flush(myUser({ userName: 'linus_t', role: 'Member' }));
    await navigation;

    expect(TestBed.inject(Router).url).toBe('/');
  });

  it('turns an unloadable acting user around to the main page', async () => {
    const navigation = harness.navigateByUrl('/moderation');
    (await expectMyUserRequest()).flush('boom', { status: 500, statusText: 'Server Error' });
    await navigation;

    expect(TestBed.inject(Router).url).toBe('/');
  });
});
