import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { myUser } from '../current-user/current-user.fixtures';
import { App } from './app';

describe('App', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('links the Statute and the Privacy Policy from the shell (issue #30)', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const shell = fixture.nativeElement as HTMLElement;

    const statuteLink = shell.querySelector('a.statute-link');
    const privacyPolicyLink = shell.querySelector('a.privacy-policy-link');

    expect(statuteLink?.getAttribute('href')).toBe('/statute');
    expect(statuteLink?.textContent).toContain('Statute');
    expect(privacyPolicyLink?.getAttribute('href')).toBe('/privacy-policy');
    expect(privacyPolicyLink?.textContent).toContain('Privacy Policy');
  });

  // Creating the shell injects the root CurrentUserStore, whose first injection starts the
  // one who-am-I fetch; every spec runs in a fresh TestBed, so each stages its own store and
  // answers the request with the role under test before reading the nav.
  const expectMyUserRequest = () => httpMock.expectOne({ method: 'GET', url: '/api/my-user' });

  it('shows the moderator area entry to a moderator (issue #34)', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expectMyUserRequest().flush(myUser());
    fixture.detectChanges();

    const moderationLink = (fixture.nativeElement as HTMLElement).querySelector(
      'a.moderation-link',
    );
    expect(moderationLink?.getAttribute('href')).toBe('/moderation');
    expect(moderationLink?.textContent).toContain('Moderation');
  });

  // The absence specs lean on the presence spec above to keep the selector honest: if
  // a.moderation-link ever stops matching the rendered entry, that spec fails loudly, so
  // these cannot pass vacuously against a shell whose entry merely drifted.
  it('shows no moderator area entry to a member (issue #34)', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expectMyUserRequest().flush(myUser({ userName: 'linus_t', role: 'Member' }));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('a.moderation-link')).toBeNull();
  });

  it('shows no moderator area entry while the acting user is unknown (issue #34)', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    // The who-am-I fetch is in flight and deliberately unanswered — the entry must not
    // flash in early.
    expectMyUserRequest();

    expect((fixture.nativeElement as HTMLElement).querySelector('a.moderation-link')).toBeNull();
  });
});
