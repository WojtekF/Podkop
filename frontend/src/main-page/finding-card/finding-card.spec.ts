import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { FindingSummaryDto } from '../main-page-feed.service';
import { FindingCard } from './finding-card';

// A finding whose id the navigation affordances must route to.
const navSummary: FindingSummaryDto = {
  id: '0d4f9a3e-1111-4222-8333-444455556666',
  title: 'A remarkable finding',
  description: 'Worth reading in full.',
  sourceUrl: 'https://blog.example.org/posts/42',
  domain: 'blog.example.org',
  thumbnailUrl: null,
  author: 'grace_hopper',
  tags: ['dotnet'],
  digCount: 123,
  commentCount: 7,
  createdAt: '2026-07-08T06:00:00Z',
  promotedAt: '2026-07-08T09:30:00Z',
};

@Component({
  template: '<main-page-finding-card [finding]="finding" />',
  imports: [FindingCard],
})
class CardHost {
  readonly finding = navSummary;
}

@Component({ template: 'finding detail' })
class DetailStub {}

@Component({ template: 'tag page' })
class TagPageStub {}

describe('FindingCard', () => {
  let fixture: ComponentFixture<FindingCard>;

  const summary: FindingSummaryDto = {
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
    createdAt: '2026-07-08T06:00:00Z',
    promotedAt: '2026-07-08T09:30:00Z',
  };

  const createCard = async (finding: FindingSummaryDto) => {
    fixture = TestBed.createComponent(FindingCard);
    fixture.componentRef.setInput('finding', finding);
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const element = (): HTMLElement => fixture.nativeElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FindingCard],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('links the domain to the Source in a new tab; the title is not an external link', async () => {
    await createCard(summary);

    const link = element().querySelector<HTMLAnchorElement>('.source-link');
    expect(link?.textContent).toContain('blog.example.org');
    expect(link?.getAttribute('href')).toBe('https://blog.example.org/posts/42');
    expect(link?.getAttribute('target')).toBe('_blank');
    expect(link?.getAttribute('rel')).toBe('noopener noreferrer');
    expect(element().querySelector('.domain')?.textContent).toContain('blog.example.org');

    const titleLink = element().querySelector<HTMLElement>('.title-link');
    expect(titleLink?.textContent).toContain('A remarkable finding');
    expect(titleLink?.getAttribute('href')).not.toBe('https://blog.example.org/posts/42');
    expect(titleLink?.getAttribute('target')).toBeNull();
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

describe('FindingCard — navigation to the finding', () => {
  let harness: RouterTestingHarness;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: '', component: CardHost },
          { path: 'finding/:id', component: DetailStub },
          { path: 'tag/:name', component: TagPageStub },
        ]),
      ],
    });
    router = TestBed.inject(Router);
    harness = await RouterTestingHarness.create();
  });

  const renderCard = async (): Promise<HTMLElement> => {
    await harness.navigateByUrl('/', CardHost);
    return harness.routeNativeElement!;
  };

  it('clicking the description opens the finding page, scrolled to the top', async () => {
    const card = await renderCard();

    card.querySelector<HTMLElement>('.description')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe(`/finding/${navSummary.id}`);
  });

  it('clicking the comment count opens the finding page at the comments fragment', async () => {
    const card = await renderCard();

    card.querySelector<HTMLElement>('.comment-count')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe(`/finding/${navSummary.id}#comments`);
  });

  it('clicking the title opens the finding page, like the description', async () => {
    const card = await renderCard();

    card.querySelector<HTMLElement>('.title-link')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe(`/finding/${navSummary.id}`);
  });

  it('clicking a tag opens that tag page (issue #77)', async () => {
    const card = await renderCard();

    card.querySelector<HTMLElement>('.tag')?.click();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/tag/dotnet');
  });

  it('a tag chip links to the tag page by its bare name, hash and all', async () => {
    // The "#" is presentation; the route carries the canonical name the URL is built from.
    const card = await renderCard();

    const chip = card.querySelector<HTMLAnchorElement>('.tag');
    expect(chip?.textContent).toContain('#dotnet');
    expect(chip?.getAttribute('href')).toBe('/tag/dotnet');
  });

  it('keeps the domain pointing at the external Source, not the finding page', async () => {
    const card = await renderCard();

    const link = card.querySelector<HTMLAnchorElement>('.source-link');
    expect(link?.getAttribute('href')).toBe('https://blog.example.org/posts/42');
    expect(link?.getAttribute('target')).toBe('_blank');
  });
});
