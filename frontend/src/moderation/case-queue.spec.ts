import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { caseQueue } from './moderation.fixtures';
import { CaseQueue } from './case-queue';

@Component({ template: 'main page' })
class MainPageStub {}

// The route is registered without the moderator guard here on purpose: the guard has its own
// specs, and this page's behavior is the same whoever reached it — the API refusal shows as
// the error state.
describe('CaseQueue', () => {
  let harness: RouterTestingHarness;
  let httpMock: HttpTestingController;

  const element = (): HTMLElement => harness.routeNativeElement!;
  const expectQueueRequest = () =>
    httpMock.expectOne({ method: 'GET', url: '/api/moderation/cases' });

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: '', component: MainPageStub },
          { path: 'moderation', component: CaseQueue },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  it('landing on the route requests the case queue', async () => {
    await harness.navigateByUrl('/moderation', CaseQueue);

    expectQueueRequest();
  });

  it('shows a loading state until the queue arrives', async () => {
    await harness.navigateByUrl('/moderation', CaseQueue);

    const request = expectQueueRequest();
    harness.detectChanges();
    expect(element().querySelector('.queue-state.loading')).not.toBeNull();
    expect(element().querySelector('.case-card')).toBeNull();

    request.flush(caseQueue());
    harness.detectChanges();

    expect(element().querySelector('.queue-state.loading')).toBeNull();
    expect(element().querySelectorAll('.case-card')).toHaveLength(3);
  });

  it('renders one card per case, in the exact order served', async () => {
    await harness.navigateByUrl('/moderation', CaseQueue);
    expectQueueRequest().flush(caseQueue());
    harness.detectChanges();

    // The fixture's order is deliberately not count-sorted, not newest-first, and not
    // alphabetical (see moderation.fixtures.ts) — this sequence proves nothing re-sorted it.
    const previews = Array.from(element().querySelectorAll<HTMLElement>('.case-preview')).map(
      (preview) => preview.textContent?.trim(),
    );
    expect(previews).toEqual([
      'A finding under scrutiny',
      'A comment under scrutiny.',
      "The moderator's own finding",
    ]);
  });

  it('links every preview to the finding page the content lives on', async () => {
    await harness.navigateByUrl('/moderation', CaseQueue);
    expectQueueRequest().flush(caseQueue());
    harness.detectChanges();

    const hrefs = Array.from(element().querySelectorAll<HTMLElement>('a.case-preview')).map(
      (link) => link.getAttribute('href'),
    );
    // The comment case (second) links to its host finding — findingId, never targetId.
    expect(hrefs).toEqual([
      '/finding/f0000000-0000-4000-8000-000000000001',
      '/finding/f0000000-0000-4000-8000-000000000001',
      '/finding/a0000000-0000-4000-8000-000000000003',
    ]);
  });

  it('shows each case with its author and pending-report count', async () => {
    await harness.navigateByUrl('/moderation', CaseQueue);
    expectQueueRequest().flush(caseQueue());
    harness.detectChanges();

    const cards = Array.from(element().querySelectorAll<HTMLElement>('.case-card'));

    const authors = cards.map((card) => card.querySelector('.case-author')?.textContent);
    expect(authors[0]).toContain('margaret_h');
    expect(authors[1]).toContain('grace_hopper');
    expect(authors[2]).toContain('ada_lovelace');

    const counts = cards.map((card) => card.querySelector('.case-report-count')?.textContent);
    expect(counts[0]).toContain('1');
    expect(counts[1]).toContain('2');
    expect(counts[2]).toContain('1');
  });

  it("renders a case's reports oldest first with each pinned citation and wording", async () => {
    await harness.navigateByUrl('/moderation', CaseQueue);
    expectQueueRequest().flush(caseQueue());
    harness.detectChanges();

    const commentCase = element().querySelectorAll<HTMLElement>('.case-card')[1];
    const rows = Array.from(commentCase.querySelectorAll<HTMLElement>('.report-row'));
    expect(rows).toHaveLength(2);

    // The same "2.1" cited across an amendment reads as each pinned version worded it.
    rows.forEach((row) =>
      expect(row.querySelector('.report-point-citation')?.textContent).toContain('2.1'),
    );
    expect(rows[0].querySelector('.report-point-text')?.textContent).toContain(
      'Do not post spam. (v1)',
    );
    expect(rows[1].querySelector('.report-point-text')?.textContent).toContain(
      'Do not post spam. (v2)',
    );

    // The note shows only on the report that carries one; every row shows its filing time.
    expect(rows[0].querySelector('.report-note')?.textContent).toContain('Links a spam farm.');
    expect(rows[1].querySelector('.report-note')).toBeNull();
    rows.forEach((row) => expect(row.querySelector('.report-filed-at')).not.toBeNull());
  });

  it('shows the empty state when no cases are open', async () => {
    await harness.navigateByUrl('/moderation', CaseQueue);
    expectQueueRequest().flush([]);
    harness.detectChanges();

    expect(element().querySelector('.queue-empty')?.textContent).toContain('No open cases.');
    expect(element().querySelector('.case-card')).toBeNull();
  });

  it('shows the error state when the queue cannot be loaded', async () => {
    await harness.navigateByUrl('/moderation', CaseQueue);
    expectQueueRequest().flush('broken', { status: 500, statusText: 'Internal Server Error' });
    harness.detectChanges();

    expect(element().querySelector('.queue-state.error')?.textContent).toContain(
      "Couldn't load the case queue.",
    );
    expect(element().querySelector('.case-card')).toBeNull();
  });

  it('shows the error state on the moderators-only refusal too', async () => {
    await harness.navigateByUrl('/moderation', CaseQueue);
    expectQueueRequest().flush(
      { type: 'podkop:problem:moderators-only' },
      { status: 403, statusText: 'Forbidden' },
    );
    harness.detectChanges();

    expect(element().querySelector('.queue-state.error')?.textContent).toContain(
      "Couldn't load the case queue.",
    );
  });
});
