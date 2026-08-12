import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { CommentThreadDto } from '../finding-comments.service';
import { commentThreads } from '../finding-detail.fixtures';
import { CommentThread, CommentVote, ReplyRequest } from './comment-thread';

describe('CommentThread', () => {
  let fixture: ComponentFixture<CommentThread>;

  const withReplies = (): CommentThreadDto => commentThreads()[0];
  const withoutReplies = (): CommentThreadDto => commentThreads()[1];

  const createThread = async (thread: CommentThreadDto) => {
    fixture = TestBed.createComponent(CommentThread);
    fixture.componentRef.setInput('thread', thread);
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const element = (): HTMLElement => fixture.nativeElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CommentThread],
      // jsdom runs no reduced-motion preference, so Material would animate any overlay on
      // real timers whenStable() never awaits; disabling animations keeps menus observable.
      providers: [{ provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } }],
    }).compileComponents();
  });

  it('renders its top-level comment and each reply as comment rows', async () => {
    await createThread(withReplies());

    expect(element().querySelectorAll('.comment').length).toBe(3);
  });

  it('keeps the top-level comment outside the replies container', async () => {
    await createThread(withReplies());

    const topRow = element().querySelector('.comment');
    expect(topRow).not.toBeNull();
    expect(topRow!.closest('.replies')).toBeNull();
    expect(topRow!.querySelector('.author')?.textContent).toContain('grace_hopper');
  });

  it('nests the replies exactly one level down, in the order the thread delivers them', async () => {
    await createThread(withReplies());

    const replyAuthors = Array.from(element().querySelectorAll('.replies .comment .author')).map(
      (author) => author.textContent?.trim(),
    );
    expect(replyAuthors).toEqual(['linus_t', 'ada_lovelace']);

    // One level deep only: no replies container hides inside another.
    expect(element().querySelectorAll('.replies .replies').length).toBe(0);
  });

  it('a thread with no replies renders no reply rows', async () => {
    await createThread(withoutReplies());

    expect(element().querySelectorAll('.comment').length).toBe(1);
    expect(element().querySelectorAll('.replies .comment').length).toBe(0);
  });

  describe('voting (issue #18)', () => {
    const rows = () => element().querySelectorAll('.comment');
    const upButtonOf = (row: Element) =>
      row.querySelector<HTMLButtonElement>('button.upvote-button');

    it("forwards a row's vote tagged with the comment it belongs to", async () => {
      await createThread(withReplies());

      const emitted: CommentVote[] = [];
      fixture.componentInstance.vote.subscribe((vote) => emitted.push(vote));

      // The first reply belongs to linus_t — not the reader — so its controls are live.
      const replyRow = element().querySelectorAll('.replies .comment')[0];
      upButtonOf(replyRow)!.click();

      expect(emitted).toEqual([{ commentId: withReplies().replies[0].id, direction: 'up' }]);
    });

    it("marks only the pending comment's row as pending", async () => {
      await createThread(withReplies());
      fixture.componentRef.setInput('pendingVoteIds', [withReplies().replies[0].id]);
      await fixture.whenStable();
      fixture.detectChanges();

      const replyRow = element().querySelectorAll('.replies .comment')[0];
      expect(upButtonOf(replyRow)!.disabled).toBe(true);

      // The top-level comment has no request in flight (and grace_hopper is not the
      // reader), so its controls stay live.
      expect(upButtonOf(rows()[0])!.disabled).toBe(false);
    });
  });

  describe('replying (issue #17)', () => {
    const replyButtonOf = (row: Element) =>
      row.querySelector<HTMLButtonElement>('button.reply-button');

    it('a reply request from the top-level row targets this thread, no @name to append', async () => {
      await createThread(withReplies());
      const emitted: ReplyRequest[] = [];
      fixture.componentInstance.reply.subscribe((request) => emitted.push(request));

      const topRow = element().querySelector('.comment')!;
      replyButtonOf(topRow)!.click();

      expect(emitted).toEqual([{ threadId: withReplies().id, appendAuthor: null }]);
    });

    it("a reply request from a reply row targets the same thread, carrying that reply's author", async () => {
      // Threads are one level deep: answering a reply lands under the same top-level
      // comment, with the answered author's @name travelling along for the draft.
      await createThread(withReplies());
      const emitted: ReplyRequest[] = [];
      fixture.componentInstance.reply.subscribe((request) => emitted.push(request));

      const replyRow = element().querySelectorAll('.replies .comment')[0];
      replyButtonOf(replyRow)!.click();

      expect(emitted).toEqual([
        { threadId: withReplies().id, appendAuthor: withReplies().replies[0].author },
      ]);
    });

    it('hosts no composer while its composer input is null', async () => {
      await createThread(withReplies());

      expect(element().querySelector('app-comment-composer')).toBeNull();
    });

    it('hosts the inline composer when its composer input is set', async () => {
      await createThread(withReplies());
      fixture.componentRef.setInput('composer', { draft: '', pending: false });
      await fixture.whenStable();
      fixture.detectChanges();

      expect(element().querySelector('app-comment-composer')).not.toBeNull();
    });
  });

  describe('reporting (issue #33)', () => {
    const menuButtonOf = (row: Element) =>
      row.querySelector<HTMLButtonElement>('button.comment-menu-button');
    // The opened menu may render in an overlay outside the row — search the whole document.
    const reportItem = () => document.querySelector<HTMLButtonElement>('button.report-menu-item');

    const openMenuOf = async (row: Element) => {
      menuButtonOf(row)!.click();
      await fixture.whenStable();
      fixture.detectChanges();
    };

    afterEach(() => {
      // An overlay-hosted menu outlives its fixture — torn down so specs stay independent.
      document.querySelectorAll('.cdk-overlay-container').forEach((el) => el.remove());
    });

    it("forwards the top-level row's report request as this thread's comment id", async () => {
      await createThread(withReplies());
      const emitted: string[] = [];
      fixture.componentInstance.report.subscribe((commentId) => emitted.push(commentId));

      const topRow = element().querySelector('.comment')!;
      await openMenuOf(topRow);
      reportItem()!.click();

      expect(emitted).toEqual([withReplies().id]);
    });

    it("forwards a reply row's report request as the reply's own id, never the thread's", async () => {
      await createThread(withReplies());
      const emitted: string[] = [];
      fixture.componentInstance.report.subscribe((commentId) => emitted.push(commentId));

      // The first reply belongs to linus_t — not the reader — so its menu is offered.
      const replyRow = element().querySelectorAll('.replies .comment')[0];
      await openMenuOf(replyRow);
      reportItem()!.click();

      expect(emitted).toEqual([withReplies().replies[0].id]);
    });

    it('marks exactly the rows named in reportedCommentIds as reported', async () => {
      await createThread(withReplies());
      fixture.componentRef.setInput('reportedCommentIds', [withReplies().replies[0].id]);
      await fixture.whenStable();
      fixture.detectChanges();

      const replyRow = element().querySelectorAll('.replies .comment')[0];
      await openMenuOf(replyRow);
      expect(reportItem()!.disabled).toBe(true);
      expect(reportItem()!.textContent).toContain('Reported');
      document.querySelectorAll('.cdk-overlay-container').forEach((el) => el.remove());

      // The top-level comment is not among the reported ids — its entry stays live.
      const topRow = element().querySelector('.comment')!;
      await openMenuOf(topRow);
      expect(reportItem()!.disabled).toBe(false);
    });
  });
});
