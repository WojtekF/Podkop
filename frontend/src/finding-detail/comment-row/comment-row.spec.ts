import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { CommentDto, CommentVoteDirection } from '../finding-comments.service';
import { commentThreads } from '../finding-detail.fixtures';
import { CommentRow } from './comment-row';

describe('CommentRow', () => {
  let fixture: ComponentFixture<CommentRow>;

  // The best thread's top-level comment: grace_hopper, 12 up / 2 down.
  const { replies: _replies, ...comment } = commentThreads()[0];

  const createRow = async (c: CommentDto) => {
    fixture = TestBed.createComponent(CommentRow);
    fixture.componentRef.setInput('comment', c);
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const element = (): HTMLElement => fixture.nativeElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CommentRow],
      // jsdom runs no reduced-motion preference, so Material would animate any overlay on
      // real timers whenStable() never awaits; disabling animations keeps menus observable.
      providers: [{ provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } }],
    }).compileComponents();
  });

  it('is one voice in the discussion: a root element marked `comment`', async () => {
    await createRow(comment);

    expect(element().querySelector('.comment')).not.toBeNull();
  });

  it('shows the author, the text, and both vote counts separately', async () => {
    await createRow(comment);

    expect(element().querySelector('.author')?.textContent).toContain('grace_hopper');
    expect(element().querySelector('.text')?.textContent).toContain('Best take in the thread.');
    expect(element().querySelector('.upvote-count')?.textContent).toContain('12');
    expect(element().querySelector('.downvote-count')?.textContent).toContain('2');
  });

  it("shows the comment's age, not a calendar date", async () => {
    await createRow(comment);

    const age = element().querySelector('.age');
    expect(age?.textContent?.trim()).toBeTruthy();
    expect(age?.textContent).not.toContain('2026');
  });

  describe('vote controls (issue #18)', () => {
    const upButton = () => element().querySelector<HTMLButtonElement>('button.upvote-button');
    const downButton = () => element().querySelector<HTMLButtonElement>('button.downvote-button');

    it('offers an upvote and a downvote control', async () => {
      await createRow(comment);

      expect(upButton()).not.toBeNull();
      expect(downButton()).not.toBeNull();
    });

    it('reports the direction the reader clicked', async () => {
      await createRow({ ...comment, myVote: null });

      const emitted: CommentVoteDirection[] = [];
      fixture.componentInstance.vote.subscribe((direction) => emitted.push(direction));

      upButton()!.click();
      downButton()!.click();
      expect(emitted).toEqual(['up', 'down']);
    });

    it("highlights the reader's current up vote — and only it", async () => {
      await createRow({ ...comment, myVote: 'up' });

      expect(upButton()!.classList.contains('voted')).toBe(true);
      expect(downButton()!.classList.contains('voted')).toBe(false);
    });

    it("highlights the reader's current down vote — and only it", async () => {
      await createRow({ ...comment, myVote: 'down' });

      expect(upButton()!.classList.contains('voted')).toBe(false);
      expect(downButton()!.classList.contains('voted')).toBe(true);
    });

    it('shows no highlight when the reader has not voted', async () => {
      await createRow({ ...comment, myVote: null });

      // The controls are there — neither carries the highlight.
      expect(upButton()).not.toBeNull();
      expect(element().querySelector('.voted')).toBeNull();
    });

    it("disables both controls on the reader's own comment", async () => {
      // The stub user (current-user.ts) authored it — scores can't be self-inflated.
      await createRow({ ...comment, author: 'ada_lovelace', myVote: null });

      expect(upButton()!.disabled).toBe(true);
      expect(downButton()!.disabled).toBe(true);
    });

    it('disables both controls while a vote request for this comment is in flight', async () => {
      await createRow({ ...comment, myVote: null });
      fixture.componentRef.setInput('votePending', true);
      await fixture.whenStable();
      fixture.detectChanges();

      expect(upButton()!.disabled).toBe(true);
      expect(downButton()!.disabled).toBe(true);
    });

    it("leaves the controls enabled on someone else's comment with no request in flight", async () => {
      await createRow({ ...comment, myVote: null });

      expect(upButton()!.disabled).toBe(false);
      expect(downButton()!.disabled).toBe(false);
    });
  });

  describe('reply control (issue #17)', () => {
    const replyButton = () => element().querySelector<HTMLButtonElement>('button.reply-button');

    it('offers a reply control', async () => {
      await createRow(comment);

      expect(replyButton()).not.toBeNull();
    });

    it('reports that the reader wants to answer this comment', async () => {
      await createRow(comment);
      let replies = 0;
      fixture.componentInstance.reply.subscribe(() => (replies += 1));

      replyButton()!.click();

      expect(replies).toBe(1);
    });

    it("stays live on the reader's own comment — replying to yourself is allowed", async () => {
      // Only voting on own content is forbidden; follow-ups are not.
      await createRow({ ...comment, author: 'ada_lovelace' });

      expect(replyButton()!.disabled).toBe(false);
    });

    it('stays live while a vote request is in flight — votes and replies are separate', async () => {
      await createRow({ ...comment, myVote: null });
      fixture.componentRef.setInput('votePending', true);
      await fixture.whenStable();
      fixture.detectChanges();

      expect(replyButton()!.disabled).toBe(false);
    });
  });

  describe('report action (issue #33)', () => {
    const menuButton = () =>
      element().querySelector<HTMLButtonElement>('button.comment-menu-button');
    // The opened menu may render in an overlay outside the row — search the whole document.
    const reportItem = () => document.querySelector<HTMLButtonElement>('button.report-menu-item');

    const openMenu = async () => {
      menuButton()!.click();
      await fixture.whenStable();
      fixture.detectChanges();
    };

    afterEach(() => {
      // An overlay-hosted menu outlives its fixture — torn down so specs stay independent.
      document.querySelectorAll('.cdk-overlay-container').forEach((el) => el.remove());
    });

    it("offers the actions menu on someone else's comment", async () => {
      await createRow(comment);

      expect(menuButton()).not.toBeNull();
      expect(menuButton()!.getAttribute('aria-label')).toBe('Comment actions');
    });

    it("shows no actions menu on the reader's own comment — self-reports are refused", async () => {
      // Guard against a trivially-empty template: someone else's comment carries the menu…
      await createRow(comment);
      expect(menuButton()).not.toBeNull();

      // …while the stub user's (current-user.ts) own comment shows none at all.
      await createRow({ ...comment, author: 'ada_lovelace' });
      expect(menuButton()).toBeNull();
    });

    it('the opened menu offers Report, live while the comment is not yet reported', async () => {
      await createRow(comment);

      await openMenu();

      expect(reportItem()).not.toBeNull();
      expect(reportItem()!.textContent).toContain('Report');
      expect(reportItem()!.disabled).toBe(false);
    });

    it('choosing Report reports that the reader wants to report this comment', async () => {
      await createRow(comment);
      let reports = 0;
      fixture.componentInstance.report.subscribe(() => (reports += 1));

      await openMenu();
      reportItem()!.click();

      expect(reports).toBe(1);
    });

    it("an already-reported comment's entry reads Reported and is disabled", async () => {
      await createRow(comment);
      fixture.componentRef.setInput('reportedByMe', true);
      await fixture.whenStable();
      fixture.detectChanges();

      await openMenu();

      expect(reportItem()!.textContent).toContain('Reported');
      expect(reportItem()!.disabled).toBe(true);
    });
  });
});
