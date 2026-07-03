import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PostCard } from './post-card';

describe('PostCard', () => {
  let component: PostCard;
  let fixture: ComponentFixture<PostCard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PostCard],
    }).compileComponents();

    fixture = TestBed.createComponent(PostCard);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('mainPost', {
      id: 1,
      title: 'Title',
      content: 'Content',
      image: '',
      createdAt: '2026-01-01T00:00:00Z',
      tags: ['tag'],
      author: 'Author',
      commentCount: 0,
      upvoteCount: 0,
      domain: 'example.com',
    });
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
