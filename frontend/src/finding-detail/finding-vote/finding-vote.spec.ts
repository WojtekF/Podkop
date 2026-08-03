import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BuryReason, FindingDetailDto } from '../finding-detail.service';
import { findingDetail } from '../finding-detail.fixtures';
import { FindingVote } from './finding-vote';

describe('FindingVote', () => {
  let fixture: ComponentFixture<FindingVote>;

  // A finding the stub user did not author, so its controls are live by default.
  const votable = (overrides: Partial<FindingDetailDto> = {}): FindingDetailDto =>
    findingDetail({ author: 'grace_hopper', myVote: null, ...overrides });

  const create = async (finding: FindingDetailDto = votable()) => {
    fixture = TestBed.createComponent(FindingVote);
    fixture.componentRef.setInput('finding', finding);
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const el = (): HTMLElement => fixture.nativeElement;
  const digButton = () => el().querySelector<HTMLButtonElement>('button.dig-button');
  const buryButton = () => el().querySelector<HTMLButtonElement>('button.bury-button');

  // The bury reasons may render in an overlay, so they are looked up on the whole document.
  const openBuryPicker = async () => {
    buryButton()!.click();
    await fixture.whenStable();
    fixture.detectChanges();
  };
  const reasonOptions = () => Array.from(document.querySelectorAll<HTMLElement>('.bury-reason'));

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [FindingVote] }).compileComponents();
  });

  afterEach(() => {
    // Clear any reason options an opened picker left behind so they don't bleed across tests.
    document.querySelectorAll('.bury-reason').forEach((node) => node.remove());
  });

  it('offers a dig control and a bury control', async () => {
    await create();

    expect(digButton()).not.toBeNull();
    expect(buryButton()).not.toBeNull();
  });

  it('shows the dig count on the dig control', async () => {
    await create(votable({ digCount: 123 }));

    expect(el().querySelector('.dig-count')?.textContent).toContain('123');
  });

  it('shows no numeric count on the bury control', async () => {
    // Only digs are public — the bury side stays count-free so pile-ons aren't encouraged.
    await create(votable({ digCount: 123 }));

    expect(buryButton()!.textContent).not.toMatch(/\d/);
  });

  it('reports a dig when the reader clicks dig', async () => {
    await create();
    let digs = 0;
    fixture.componentInstance.dig.subscribe(() => (digs += 1));

    digButton()!.click();

    expect(digs).toBe(1);
  });

  it('offers exactly the five bury reasons, labelled', async () => {
    await create();

    await openBuryPicker();

    expect(reasonOptions().map((option) => option.textContent?.trim())).toEqual([
      'Duplicate',
      'Spam',
      'False information',
      'Inappropriate content',
      'Unsuitable',
    ]);
  });

  it('reports the bury reason the reader chose', async () => {
    await create();
    const chosen: BuryReason[] = [];
    fixture.componentInstance.bury.subscribe((reason) => chosen.push(reason));

    await openBuryPicker();
    reasonOptions().find((option) => option.textContent?.trim() === 'Spam')!.click();

    expect(chosen).toEqual(['spam']);
  });

  it('withdraws an existing bury on click — no reason picker involved', async () => {
    // A reason is only ever chosen when a reason is needed. The reader already holds a bury,
    // so clicking the bury control undoes it directly, symmetric with clicking a highlighted
    // dig — the picker stays shut and no reason is reported.
    await create(votable({ myVote: 'bury' }));
    let withdrawals = 0;
    const chosen: BuryReason[] = [];
    fixture.componentInstance.withdrawBury.subscribe(() => (withdrawals += 1));
    fixture.componentInstance.bury.subscribe((reason) => chosen.push(reason));

    buryButton()!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(withdrawals).toBe(1);
    expect(chosen).toEqual([]);
    expect(reasonOptions()).toEqual([]);
  });

  it('still offers the reason picker when the reader currently holds a dig', async () => {
    // Only an existing bury short-circuits the picker: switching sides creates a fresh bury,
    // which needs its reason like any other.
    await create(votable({ myVote: 'dig' }));

    await openBuryPicker();

    expect(reasonOptions()).toHaveLength(5);
  });

  it('does not withdraw when the reader has no bury to withdraw', async () => {
    // Unvoted reader: the click opens the picker and nothing is withdrawn.
    await create(votable({ myVote: null }));
    let withdrawals = 0;
    fixture.componentInstance.withdrawBury.subscribe(() => (withdrawals += 1));

    await openBuryPicker();

    expect(withdrawals).toBe(0);
    expect(reasonOptions()).toHaveLength(5);
  });

  it("highlights the reader's current dig — and only it", async () => {
    await create(votable({ myVote: 'dig' }));

    expect(digButton()!.classList.contains('voted')).toBe(true);
    expect(buryButton()!.classList.contains('voted')).toBe(false);
  });

  it("highlights the reader's current bury — and only it", async () => {
    await create(votable({ myVote: 'bury' }));

    expect(buryButton()!.classList.contains('voted')).toBe(true);
    expect(digButton()!.classList.contains('voted')).toBe(false);
  });

  it('shows no highlight when the reader has not voted', async () => {
    await create(votable({ myVote: null }));

    // The controls are there — neither carries the highlight.
    expect(digButton()).not.toBeNull();
    expect(buryButton()).not.toBeNull();
    expect(el().querySelector('.voted')).toBeNull();
  });

  it("disables both controls on the reader's own finding", async () => {
    // The stub user (current-user.ts) authored it — scores can't be self-inflated.
    await create(findingDetail({ author: 'ada_lovelace', myVote: null }));

    expect(digButton()!.disabled).toBe(true);
    expect(buryButton()!.disabled).toBe(true);
  });

  it('disables both controls while a vote request is in flight', async () => {
    await create();
    fixture.componentRef.setInput('votePending', true);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(digButton()!.disabled).toBe(true);
    expect(buryButton()!.disabled).toBe(true);
  });

  it("leaves the controls enabled on someone else's finding with no request in flight", async () => {
    await create();

    expect(digButton()!.disabled).toBe(false);
    expect(buryButton()!.disabled).toBe(false);
  });
});
