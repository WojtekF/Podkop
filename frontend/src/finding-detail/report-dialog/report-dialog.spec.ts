import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { statute } from '../../documents/documents.fixtures';
import { FileReportIntent } from '../finding-report.service';
import { REPORT_NOTE_MAX_LENGTH, ReportDialog } from './report-dialog';

describe('ReportDialog', () => {
  let fixture: ComponentFixture<ReportDialog>;
  let httpMock: HttpTestingController;

  // From the statute fixture: only section 2's points are reportable.
  const spamPointId = 'aaaa0000-0000-4000-8000-000000000002';
  const hatePointId = 'aaaa0000-0000-4000-8000-000000000003';

  const element = (): HTMLElement => fixture.nativeElement;
  const pointRows = () => element().querySelectorAll<HTMLElement>('.report-point');
  const noteArea = () => element().querySelector<HTMLTextAreaElement>('textarea.report-note');
  const submitButton = () =>
    element().querySelector<HTMLButtonElement>('button.submit-report-button');

  const expectStatuteRequest = () => httpMock.expectOne('/api/statute');

  const loadStatute = () => {
    expectStatuteRequest().flush(statute());
    fixture.detectChanges();
  };

  const selectPoint = (index: number) => {
    pointRows()[index].click();
    fixture.detectChanges();
  };

  const typeNote = (text: string) => {
    const area = noteArea()!;
    area.value = text;
    area.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ReportDialog);
    fixture.detectChanges();
  });

  it('requests the current Statute on creation', () => {
    const req = expectStatuteRequest();
    expect(req.request.method).toBe('GET');
  });

  it('shows a loading state until the Statute arrives', () => {
    expect(element().querySelector('.report-state.loading')).not.toBeNull();
    expect(pointRows().length).toBe(0);

    loadStatute();

    expect(element().querySelector('.report-state.loading')).toBeNull();
  });

  it('carries the dialog title', () => {
    loadStatute();

    expect(element().querySelector('.report-title')?.textContent).toContain('Report finding');
  });

  it('a failed Statute load shows an error whose Retry re-requests it', () => {
    expectStatuteRequest().flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    const error = element().querySelector('.report-state.error');
    expect(error?.textContent).toContain("Couldn't load the Statute.");

    element().querySelector<HTMLButtonElement>('button.retry-button')!.click();
    fixture.detectChanges();

    expectStatuteRequest();
  });

  it('offers only the reportable points of the current Statute, cited and in server order', () => {
    loadStatute();

    // The fixture's reportable points are exactly section 2's two conduct rules — the
    // purpose and consequences framing must not be offered.
    expect(pointRows().length).toBe(2);

    const citations = Array.from(pointRows()).map(
      (row) => row.querySelector('.point-citation')?.textContent?.trim(),
    );
    expect(citations).toEqual(['2.1', '2.2']);

    const texts = Array.from(pointRows()).map(
      (row) => row.querySelector('.point-text')?.textContent,
    );
    expect(texts[0]).toContain('Do not post spam.');
    expect(texts[1]).toContain('Do not post hateful content.');

    expect(element().textContent).not.toContain('Podkop is a community');
    expect(element().textContent).not.toContain('Moderators may remove');
  });

  it('submit sits disabled until a point is picked', () => {
    loadStatute();

    expect(submitButton()?.disabled).toBe(true);

    selectPoint(0);

    expect(submitButton()?.disabled).toBe(false);
  });

  it('picking a point marks exactly that row selected, and picking another moves the mark', () => {
    loadStatute();

    selectPoint(0);
    expect(pointRows()[0].classList.contains('selected')).toBe(true);
    expect(pointRows()[1].classList.contains('selected')).toBe(false);

    selectPoint(1);
    expect(pointRows()[0].classList.contains('selected')).toBe(false);
    expect(pointRows()[1].classList.contains('selected')).toBe(true);
  });

  it('counts the note characters against the 500 cap', () => {
    loadStatute();

    const counter = () => element().querySelector('.note-counter');
    expect(counter()).not.toBeNull();
    expect(counter()?.textContent).toContain('500');

    typeNote('x'.repeat(REPORT_NOTE_MAX_LENGTH));
    expect(counter()?.classList.contains('over-limit')).toBe(false);

    typeNote('x'.repeat(REPORT_NOTE_MAX_LENGTH + 1));
    expect(counter()?.classList.contains('over-limit')).toBe(true);
  });

  it('a note over the cap disables submit even with a point picked', () => {
    loadStatute();
    selectPoint(0);

    typeNote('x'.repeat(REPORT_NOTE_MAX_LENGTH + 1));
    expect(submitButton()?.disabled).toBe(true);

    typeNote('x'.repeat(REPORT_NOTE_MAX_LENGTH));
    expect(submitButton()?.disabled).toBe(false);
  });

  it('submitting emits the picked point with the note trimmed', () => {
    loadStatute();
    let emitted: FileReportIntent | undefined;
    fixture.componentInstance.fileReport.subscribe((intent) => (emitted = intent));

    selectPoint(1);
    typeNote('  Links a spam farm. \n');
    submitButton()!.click();

    expect(emitted).toEqual({ statutePointId: hatePointId, note: 'Links a spam farm.' });
  });

  it('submitting without a note emits an explicit null note', () => {
    loadStatute();
    let emitted: FileReportIntent | undefined;
    fixture.componentInstance.fileReport.subscribe((intent) => (emitted = intent));

    selectPoint(0);
    submitButton()!.click();

    expect(emitted).toEqual({ statutePointId: spamPointId, note: null });
  });

  it('a whitespace-only note also emits null — no note at all', () => {
    loadStatute();
    let emitted: FileReportIntent | undefined;
    fixture.componentInstance.fileReport.subscribe((intent) => (emitted = intent));

    selectPoint(0);
    typeNote('   \n ');
    submitButton()!.click();

    expect(emitted).toEqual({ statutePointId: spamPointId, note: null });
  });

  it('cancel emits the cancel output', () => {
    loadStatute();
    let cancelled = false;
    fixture.componentInstance.cancel.subscribe(() => (cancelled = true));

    element().querySelector<HTMLButtonElement>('button.cancel-report-button')!.click();

    expect(cancelled).toBe(true);
  });
});
