import { PrivacyPolicyDto, StatuteDto } from './documents.service';

// Shared test data for the documents specs (pages, service). The server already orders
// sections, points, and paragraphs — the frontend renders them as-is and never re-sorts.

export const statute = (overrides: Partial<StatuteDto> = {}): StatuteDto => ({
  version: 2,
  effectiveFrom: '2026-06-01T00:00:00Z',
  sections: [
    {
      number: 1,
      title: 'Purpose of the service',
      points: [
        {
          id: 'aaaa0000-0000-4000-8000-000000000001',
          number: 1,
          text: 'Podkop is a community for sharing and judging findings.',
          isReportable: false,
        },
      ],
    },
    {
      number: 2,
      title: 'Rules of conduct',
      points: [
        {
          id: 'aaaa0000-0000-4000-8000-000000000002',
          number: 1,
          text: 'Do not post spam.',
          isReportable: true,
        },
        {
          id: 'aaaa0000-0000-4000-8000-000000000003',
          number: 2,
          text: 'Do not post hateful content.',
          isReportable: true,
        },
      ],
    },
    {
      number: 3,
      title: 'Consequences',
      points: [
        {
          id: 'aaaa0000-0000-4000-8000-000000000004',
          number: 1,
          text: 'Moderators may remove content, redact it, or ban the author.',
          isReportable: false,
        },
      ],
    },
  ],
  ...overrides,
});

export const privacyPolicy = (overrides: Partial<PrivacyPolicyDto> = {}): PrivacyPolicyDto => ({
  version: 1,
  effectiveFrom: '2026-05-01T00:00:00Z',
  sections: [
    {
      number: 1,
      title: 'Data we process',
      paragraphs: [
        'We store the findings, comments, and votes you submit.',
        'We do not track you across other sites.',
      ],
    },
    {
      number: 2,
      title: 'Your rights',
      paragraphs: ['You may request the erasure of your account.'],
    },
  ],
  ...overrides,
});
