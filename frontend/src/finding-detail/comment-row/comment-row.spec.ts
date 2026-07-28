import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CommentDto } from '../finding-comments.service';
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

  it('offers no vote controls — counts are display-only until the voting ticket', async () => {
    await createRow(comment);

    expect(element().querySelectorAll('button').length).toBe(0);
  });
});
