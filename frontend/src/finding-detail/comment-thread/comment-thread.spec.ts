import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CommentThreadDto } from '../finding-comments.service';
import { commentThreads } from '../finding-detail.fixtures';
import { CommentThread } from './comment-thread';

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
    await TestBed.configureTestingModule({ imports: [CommentThread] }).compileComponents();
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

    const replyAuthors = Array.from(
      element().querySelectorAll('.replies .comment .author'),
    ).map((author) => author.textContent?.trim());
    expect(replyAuthors).toEqual(['linus_t', 'ada_lovelace']);

    // One level deep only: no replies container hides inside another.
    expect(element().querySelectorAll('.replies .replies').length).toBe(0);
  });

  it('a thread with no replies renders no reply rows', async () => {
    await createThread(withoutReplies());

    expect(element().querySelectorAll('.comment').length).toBe(1);
    expect(element().querySelectorAll('.replies .comment').length).toBe(0);
  });
});
