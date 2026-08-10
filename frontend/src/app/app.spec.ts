import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
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
});
