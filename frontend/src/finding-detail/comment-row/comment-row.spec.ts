import { ComponentFixture, TestBed } from '@angular/core/testing';
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
    await TestBed.configureTestingModule({ imports: [CommentRow] }).compileComponents();
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
});
