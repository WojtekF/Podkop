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

  // The shell's own CurrentUserStore instance starts the who-am-I fetch when the component is
  // created; these specs answer it with the role under test before reading the nav.
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

  // Both absence specs first prove the entry renders for a moderator, so the absence
  // assertion can never pass vacuously against a shell with no entry at all.
  it('shows no moderator area entry to a member (issue #34)', () => {
    const moderatorFixture = TestBed.createComponent(App);
    moderatorFixture.detectChanges();
    expectMyUserRequest().flush(myUser());
    moderatorFixture.detectChanges();
    expect(
      (moderatorFixture.nativeElement as HTMLElement).querySelector('a.moderation-link'),
    ).not.toBeNull();

    const memberFixture = TestBed.createComponent(App);
    memberFixture.detectChanges();
    expectMyUserRequest().flush(myUser({ userName: 'linus_t', role: 'Member' }));
    memberFixture.detectChanges();

    expect(
      (memberFixture.nativeElement as HTMLElement).querySelector('a.moderation-link'),
    ).toBeNull();
  });

  it('shows no moderator area entry while the acting user is unknown (issue #34)', () => {
    const moderatorFixture = TestBed.createComponent(App);
    moderatorFixture.detectChanges();
    expectMyUserRequest().flush(myUser());
    moderatorFixture.detectChanges();
    expect(
      (moderatorFixture.nativeElement as HTMLElement).querySelector('a.moderation-link'),
    ).not.toBeNull();

    const unknownFixture = TestBed.createComponent(App);
    unknownFixture.detectChanges();

    // The who-am-I fetch has not answered — the entry must not flash in early.
    expect(
      (unknownFixture.nativeElement as HTMLElement).querySelector('a.moderation-link'),
    ).toBeNull();
  });
});
