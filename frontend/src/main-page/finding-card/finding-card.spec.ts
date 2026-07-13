import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FindingSummary } from '../main-page-feed.service';
import { FindingCard } from './finding-card';

describe('FindingCard', () => {
  let fixture: ComponentFixture<FindingCard>;

  const summary: FindingSummary = {
    id: '0d4f9a3e-1111-4222-8333-444455556666',
    title: 'A remarkable finding',
    description: 'Worth digging.',
    sourceUrl: 'https://blog.example.org/posts/42',
    domain: 'blog.example.org',
    thumbnailUrl: 'https://example.com/thumb.jpg',
    author: 'grace_hopper',
    tags: ['dotnet', 'webdev'],
    digCount: 123,
    commentCount: 7,
    promotedAt: '2026-07-08T09:30:00Z',
  };

  const createCard = async (finding: FindingSummary) => {
    fixture = TestBed.createComponent(FindingCard);
    fixture.componentRef.setInput('finding', finding);
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const element = (): HTMLElement => fixture.nativeElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FindingCard],
    }).compileComponents();
  });

  it('links the title to the Source in a new tab, with the domain beside it', async () => {
    await createCard(summary);

    const link = element().querySelector<HTMLAnchorElement>('.source-link');
    expect(link?.textContent).toContain('A remarkable finding');
    expect(link?.getAttribute('href')).toBe('https://blog.example.org/posts/42');
    expect(link?.getAttribute('target')).toBe('_blank');
    expect(link?.getAttribute('rel')).toBe('noopener noreferrer');
    expect(element().querySelector('.domain')?.textContent).toContain('blog.example.org');
  });

  it('shows the footer facts: author, promotedAt, comment count, tags', async () => {
    await createCard(summary);

    const meta = element().querySelector('.meta');
    expect(meta?.textContent).toContain('grace_hopper');
    expect(meta?.textContent).toContain('Jul 8, 2026'); // date:'medium'
    expect(meta?.textContent).toContain('7 comments');
    expect(meta?.textContent).toContain('#dotnet');
    expect(meta?.textContent).toContain('#webdev');
  });

  it('has a Dig button with the dig count — and no Bury control', async () => {
    await createCard(summary);

    const digButton = element().querySelector('.dig-button');
    expect(digButton).not.toBeNull();
    expect(element().querySelector('.dig-count')?.textContent).toContain('123');

    const buttons = Array.from(element().querySelectorAll('button'));
    const buryish = buttons.filter(
      (b) =>
        (b.textContent ?? '').toLowerCase().includes('bury') ||
        (b.getAttribute('aria-label') ?? '').toLowerCase().includes('bury'),
    );
    expect(buryish).toEqual([]);
  });

  it('emits dig when the Dig button is clicked', async () => {
    await createCard(summary);
    let emitted = 0;
    fixture.componentInstance.dig.subscribe(() => emitted++);

    element().querySelector<HTMLButtonElement>('.dig-button')?.click();

    expect(emitted).toBe(1);
  });

  it('renders the thumbnail when the finding has one', async () => {
    await createCard(summary);

    const img = element().querySelector<HTMLImageElement>('img.thumbnail');
    expect(img?.getAttribute('src')).toBe('https://example.com/thumb.jpg');
    expect(element().querySelector('.thumbnail-placeholder')).toBeNull();
  });

  it('renders a neutral placeholder of the same shape when the thumbnail is null', async () => {
    await createCard({ ...summary, thumbnailUrl: null });

    expect(element().querySelector('img.thumbnail')).toBeNull();
    expect(element().querySelector('.thumbnail-placeholder')).not.toBeNull();
  });
});
