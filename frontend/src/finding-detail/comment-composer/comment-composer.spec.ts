import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CommentComposer, COMMENT_MAX_LENGTH } from './comment-composer';

describe('CommentComposer', () => {
  let fixture: ComponentFixture<CommentComposer>;

  const create = async (
    inputs: { draft?: string; pending?: boolean; cancellable?: boolean } = {},
  ) => {
    fixture = TestBed.createComponent(CommentComposer);
    fixture.componentRef.setInput('draft', inputs.draft ?? '');
    if (inputs.pending !== undefined) fixture.componentRef.setInput('pending', inputs.pending);
    if (inputs.cancellable !== undefined) {
      fixture.componentRef.setInput('cancellable', inputs.cancellable);
    }
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const el = (): HTMLElement => fixture.nativeElement;
  const textarea = () => el().querySelector<HTMLTextAreaElement>('textarea.comment-text');
  const postButton = () => el().querySelector<HTMLButtonElement>('button.post-button');
  const cancelButton = () => el().querySelector<HTMLButtonElement>('button.cancel-button');
  const counter = () => el().querySelector<HTMLElement>('.char-counter');

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [CommentComposer] }).compileComponents();
  });

  it('offers a text area holding the draft and a post control', async () => {
    await create({ draft: 'A hot take.' });

    expect(textarea()).not.toBeNull();
    expect(textarea()!.value).toBe('A hot take.');
    expect(postButton()).not.toBeNull();
    expect(postButton()!.textContent).toContain('Post');
  });

  it('reports every edit through draftChange', async () => {
    await create();
    const edits: string[] = [];
    fixture.componentInstance.draftChange.subscribe((text) => edits.push(text));

    textarea()!.value = 'A hot';
    textarea()!.dispatchEvent(new Event('input'));

    expect(edits).toEqual(['A hot']);
  });

  it('disables post while the draft is empty', async () => {
    await create({ draft: '' });

    expect(postButton()!.disabled).toBe(true);
  });

  it('disables post while the draft is whitespace-only — spaces are not a comment', async () => {
    await create({ draft: '  \n\t ' });

    expect(postButton()!.disabled).toBe(true);
  });

  it('enables post once the draft has real text', async () => {
    await create({ draft: 'A hot take.' });

    expect(postButton()!.disabled).toBe(false);
  });

  it('always shows a character counter with the count and the cap', async () => {
    await create({ draft: 'abc' });

    expect(counter()).not.toBeNull();
    expect(counter()!.textContent).toContain('3');
    expect(counter()!.textContent).toContain('5000');
  });

  it('over the cap the counter reads as an error and post disables', async () => {
    await create({ draft: 'x'.repeat(COMMENT_MAX_LENGTH + 1) });

    expect(counter()!.classList.contains('over-limit')).toBe(true);
    expect(postButton()!.disabled).toBe(true);
  });

  it('exactly the cap is fine — no error state, post enabled', async () => {
    await create({ draft: 'x'.repeat(COMMENT_MAX_LENGTH) });

    expect(counter()!.classList.contains('over-limit')).toBe(false);
    expect(postButton()!.disabled).toBe(false);
  });

  it('disables the whole composer while a post is in flight', async () => {
    await create({ draft: 'A hot take.', pending: true });

    expect(textarea()!.disabled).toBe(true);
    expect(postButton()!.disabled).toBe(true);
  });

  it('emits post when the post control is activated', async () => {
    await create({ draft: 'A hot take.' });
    let posts = 0;
    fixture.componentInstance.post.subscribe(() => (posts += 1));

    postButton()!.click();

    expect(posts).toBe(1);
  });

  it('offers no cancel by default — the top-level composer is permanent', async () => {
    await create({ draft: 'A hot take.' });

    expect(cancelButton()).toBeNull();
  });

  it('offers a cancel when cancellable, and emits cancel on activation', async () => {
    await create({ draft: 'A hot take.', cancellable: true });
    let cancels = 0;
    fixture.componentInstance.cancel.subscribe(() => (cancels += 1));

    expect(cancelButton()).not.toBeNull();
    expect(cancelButton()!.textContent).toContain('Cancel');
    cancelButton()!.click();

    expect(cancels).toBe(1);
  });
});
